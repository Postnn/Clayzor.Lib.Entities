using System.Data;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

internal sealed class TelSqlRejectedException(string message, bool versionConflict = false) : InvalidOperationException(message)
{
    internal bool VersionConflict { get; } = versionConflict;
}

internal static class TelSqlWrite
{
    internal const int VersionConflictStatus = -1;
    internal const Роли WriteRole = Роли.Специалист_по_инвентаризации_оборудования;

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

    internal static Task<int> SaveAsync(DbManager db, string? principal, int employeeId,
        int phoneId, DateTime expected, Guid attemptId, string sql, object parameters, Action? onDispatch,
        CancellationToken ct) =>
        RunAsync(db, principal, employeeId, phoneId, expected, attemptId, deleting: false, sql, parameters, onDispatch, ct);

    internal static Task<int> DeleteAsync(DbManager db, string? principal, int employeeId,
        Phone original, Guid attemptId, Action? onDispatch, CancellationToken ct)
    {
        var parameters = new DynamicParameters(new { original.Id, VersionConflictStatus });
        parameters.Add("ExpectedChangedAt", original.ChangedAtUtc, DbType.DateTime);
        var sql = SQLQueries.CHECK_ТелефонИсходнаяВерсияДляСохраненияEnlisted +
            SQLQueries.DELETE_ТелефонДляФормы + ";\n" + SQLQueries.CHECK_ТелефонОбновленаОднаСтрокаEnlisted + "\nSELECT @Id;";
        return RunAsync(db, principal, employeeId, original.Id, original.ChangedAtUtc, attemptId,
            deleting: true, sql, parameters, onDispatch, ct);
    }

    private static async Task<int> RunAsync(DbManager db, string? principal, int employeeId,
        int phoneId, DateTime expected, Guid attemptId, bool deleting, string sql, object parameters, Action? onDispatch,
        CancellationToken ct)
    {
        await ReadActorAsync(db, principal, employeeId, ct);
        if (phoneId > 0)
            await CheckVersionAsync(db, phoneId, expected, ct);

        return await db.RunDbAsync(async connection =>
        {
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            EnsureActor(await Employee.GetCurrentWithRolesAsync(connection, transaction, principal, ct), employeeId);
            if (deleting)
            {
                var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(SQLQueries.SELECT_ТелефонЗависимыеПриУдалении,
                    new { Id = phoneId }, transaction, commandType: CommandType.Text, cancellationToken: ct));
                CheckDependents(count);
            }

            ct.ThrowIfCancellationRequested();
            onDispatch?.Invoke();
            var id = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(sql, parameters, transaction,
                commandType: CommandType.Text, cancellationToken: ct));
            if (id == VersionConflictStatus)
                throw VersionConflict();
            if (id is not > 0 || phoneId > 0 && id != phoneId)
                throw new InvalidOperationException("База данных не подтвердила идентификатор телефонного номера.");
            await transaction.CommitAsync(ct);
            return id.Value;
        });
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

}
