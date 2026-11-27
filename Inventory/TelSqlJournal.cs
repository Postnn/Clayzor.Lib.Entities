using System.Data;
using System.Globalization;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

internal sealed class TelSqlRejectedException(string message, bool versionConflict = false) : InvalidOperationException(message)
{
    internal bool VersionConflict { get; } = versionConflict;
}

internal static class TelSqlJournal
{
    internal const int VersionConflictStatus = -1;
    internal const Роли WriteRole = Роли.Специалист_по_инвентаризации_оборудования;

    internal static async Task<InventoryOperationReceipt?> ReadReceiptAsync(DbManager db, string? principal,
        int employeeId, int phoneId, bool deleting, Guid attemptId, CancellationToken ct)
    {
        var actor = await ReadActorAsync(db, principal, employeeId, ct);
        return await InventoryOperationJournal.ReadAttemptAsync(db, Attempt(actor, phoneId, deleting, attemptId), ct);
    }

    internal static async Task<bool> ReadPendingAsync(DbManager db, string? principal, int employeeId, int phoneId, CancellationToken ct)
    {
        await ReadActorAsync(db, principal, employeeId, ct);
        return await InventoryOperationJournal.IsScopePendingAsync(db, Scope(phoneId), ct);
    }

    internal static Task ImportLegacyOperationsAsync(DbManager db, string? principal, int employeeId, int phoneId,
        IReadOnlyCollection<(Guid AttemptId, bool Deleting)> evidence, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(phoneId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(employeeId);
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.Count == 0) return Task.CompletedTask;
        var requests = evidence.Distinct().OrderBy(item => item.AttemptId).ToArray();
        if (requests.Any(item => item.AttemptId == Guid.Empty || phoneId == 0 && item.Deleting))
            throw new ArgumentException("Не удалось подтвердить контекст прежней попытки Tel.", nameof(evidence));

        return db.RunDbAsync(async connection =>
        {
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var actor = EnsureActor(await Employee.GetCurrentWithRolesAsync(connection, transaction, principal, ct),
                employeeId);
            foreach (var (attemptId, deleting) in requests)
            {
                if (await TelAtsWorkflow.HasExactAttemptAsync(connection, transaction, attemptId, actor.Id, phoneId, deleting, ct))
                    continue;

                var attempt = Attempt(actor, phoneId, deleting, attemptId);
                if (phoneId == 0 &&
                    await InventoryOperationJournal.ReadAttemptInTransactionAsync(connection, transaction, attempt, ct) is
                        { State: InventoryOperationState.Confirmed } receipt)
                {
                    if (receipt.ResultEntityId is not > 0)
                        throw new InventoryOperationPendingException();
                    attempt = attempt with { OtherScopes = [Scope(receipt.ResultEntityId.Value)] };
                }

                // Подтверждение SQL не устанавливает исход прежних команд АТС.
                await InventoryOperationJournal.ImportLegacyAttemptAsync(connection, transaction, attempt, ct,
                    retainEvenIfJournalExists: true);
            }
            await transaction.CommitAsync(CancellationToken.None);
            return true;
        });
    }

    internal static async Task ValidateDeleteAsync(DbManager db, string? principal, int employeeId, Phone original, CancellationToken ct)
    {
        await ReadActorAsync(db, principal, employeeId, ct);
        await CheckVersionAsync(db, original.Id, original.ChangedAtUtc, ct);
        await db.RunDbAsync(async connection =>
        {
            var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(SQLQueries.SELECT_ТелефонЗависимыеПриУдалении,
                new { original.Id }, commandType: CommandType.Text, cancellationToken: ct));
            CheckDependents(count);
            return count;
        });
    }

    internal static Task<InventoryOperationReceipt> SaveAsync(DbManager db, string? principal, int employeeId,
        int phoneId, DateTime expected, Guid attemptId, string sql, object parameters, Action? onDispatch,
        CancellationToken ct, bool workflowBound = false) =>
        RunAsync(db, principal, employeeId, phoneId, expected, attemptId, deleting: false, sql, parameters, onDispatch, ct, workflowBound);

    internal static Task<InventoryOperationReceipt> DeleteAsync(DbManager db, string? principal, int employeeId,
        Phone original, Guid attemptId, Action? onDispatch, CancellationToken ct, bool workflowBound = false)
    {
        var parameters = new DynamicParameters(new { original.Id, VersionConflictStatus });
        parameters.Add("ExpectedChangedAt", original.ChangedAtUtc, DbType.DateTime);
        var sql = SQLQueries.CHECK_ТелефонИсходнаяВерсияДляСохраненияEnlisted +
            SQLQueries.DELETE_ТелефонДляФормы + ";\n" + SQLQueries.CHECK_ТелефонОбновленаОднаСтрокаEnlisted + "\nSELECT @Id;";
        return RunAsync(db, principal, employeeId, original.Id, original.ChangedAtUtc, attemptId,
            deleting: true, sql, parameters, onDispatch, ct, workflowBound);
    }

    private static async Task<InventoryOperationReceipt> RunAsync(DbManager db, string? principal, int employeeId,
        int phoneId, DateTime expected, Guid attemptId, bool deleting, string sql, object parameters, Action? onDispatch,
        CancellationToken ct, bool workflowBound)
    {
        var actor = await ReadActorAsync(db, principal, employeeId, ct);
        var attempt = Attempt(actor, phoneId, deleting, attemptId);
        if (await InventoryOperationJournal.ReadAttemptAsync(db, attempt, ct) is { } previous)
            return CheckReceipt(previous, phoneId);
        if (phoneId > 0)
            await CheckVersionAsync(db, phoneId, expected, ct);

        var receipt = await InventoryOperationJournal.RunSqlAsync<TelSqlRejectedException>(db, attempt,
            async (connection, transaction, token) =>
            {
                EnsureActor(await Employee.GetCurrentWithRolesAsync(connection, transaction, principal, token), employeeId);
                if (workflowBound)
                    await TelAtsWorkflow.RequireSqlReadyAsync(connection, transaction, attemptId, employeeId, phoneId, deleting, token);
                if (deleting)
                {
                    var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(SQLQueries.SELECT_ТелефонЗависимыеПриУдалении,
                        new { Id = phoneId }, transaction, commandType: CommandType.Text, cancellationToken: token));
                    CheckDependents(count);
                }
                token.ThrowIfCancellationRequested();
                onDispatch?.Invoke();
                var id = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(sql, parameters, transaction,
                    commandType: CommandType.Text, cancellationToken: token));
                if (id == VersionConflictStatus)
                    throw VersionConflict();
                if (id is not > 0 || phoneId > 0 && id != phoneId)
                    throw new InvalidOperationException("База данных не подтвердила идентификатор телефонного номера.");
                if (workflowBound)
                    await TelAtsWorkflow.ConfirmSqlAsync(connection, transaction, attemptId, employeeId, phoneId, id.Value,
                        token, deleting);
                return id;
            }, ct);
        return CheckReceipt(receipt, phoneId);
    }

    private static InventoryOperationReceipt CheckReceipt(InventoryOperationReceipt receipt, int phoneId)
    {
        if (receipt.State == InventoryOperationState.Rejected)
            throw new TelSqlRejectedException("Запись ранее отклонена. Перечитайте телефонный номер.");
        if (receipt.State != InventoryOperationState.Confirmed)
            throw new InventoryOperationPendingException();
        if (receipt.ResultEntityId is not > 0 || phoneId > 0 && receipt.ResultEntityId != phoneId)
            throw new InvalidOperationException("Журнал не подтвердил идентификатор телефонного номера.");
        return receipt;
    }

    private static async Task CheckVersionAsync(DbManager db, int phoneId, DateTime expected, CancellationToken ct)
    {
        var rows = await db.QueryRequiredAsync<Phone>(SQLQueries.SELECT_ТелефонДляФормы, new { Id = phoneId }, ct);
        var current = rows.SingleOrDefault();
        if (current?.Id != phoneId || current.ChangedAtUtc != expected)
            throw VersionConflict();
    }

    private static TelSqlRejectedException VersionConflict() =>
        new("Телефонный номер уже изменён или удалён. Проверьте исходную запись и свой ввод.", versionConflict: true);

    private static void CheckDependents(int count)
    {
        if (count > 0)
            throw new TelSqlRejectedException("Номер нельзя удалить: от него зависят ведомые или отображаемые номера.");
    }

    private static async Task<Employee> ReadActorAsync(DbManager db, string? principal, int employeeId, CancellationToken ct) =>
        EnsureActor(await Employee.GetCurrentWithRolesAsync(db, principal, ct), employeeId);

    internal static Employee EnsureActor(Employee? actor, int employeeId) =>
        actor is not null && actor.Id == employeeId && actor.HasRole(WriteRole) ? actor :
            throw new TelSqlRejectedException("Не удалось подтвердить сотрудника и его право на изменение телефонного номера (роль 43).");

    private static InventoryOperationJournal.Attempt Attempt(Employee actor, int phoneId, bool deleting, Guid id) =>
        InventoryOperationJournal.ForEmployee(actor, id, Scope(phoneId), deleting ? "tel.sql.delete"
            : phoneId > 0 ? "tel.sql.update" : "tel.sql.insert");

    private static string Scope(int phoneId) => phoneId > 0 ? "tel/" + phoneId.ToString(CultureInfo.InvariantCulture) : "tel/new";
}
