using System.Data;
using System.Data.Common;
using System.Globalization;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Подтверждённые сервером счётчики собственной открытой операции Tel.</summary>
internal sealed record TelWorkflowSummary(Guid AttemptId, bool SqlConfirmed, int PreStepCount, int ConfirmedPreSteps,
    int? PostStepCount, int ConfirmedPostSteps, int DispatchingSteps);

/// <summary>Долговечный порядок SQL и внешних шагов АТС для сохранения или удаления телефонного номера.</summary>
internal static class TelAtsWorkflow
{
    internal static Task<TelWorkflowSummary?> ReadActiveAsync(DbManager db, string? principal, int employeeId,
        int phoneId, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(phoneId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(employeeId);
        return db.RunDbAsync(async connection =>
        {
            var actor = TelSqlJournal.EnsureActor(await Employee.GetCurrentWithRolesAsync(connection, null, principal, ct), employeeId);
            return await connection.QuerySingleOrDefaultAsync<TelWorkflowSummary>(new CommandDefinition(
                SQLQueries.SELECT_ClayzorTelWorkflowActiveSummary, new { Scope = Scope(phoneId), EmployeeId = actor.Id },
                commandType: CommandType.Text, cancellationToken: ct));
        });
    }

    private sealed record WorkflowOwner(int EmployeeId, int OriginalPhoneId, bool Deleting);

    internal static async Task<bool> HasExactAttemptAsync(DbConnection connection, DbTransaction transaction,
        Guid attemptId, int employeeId, int originalPhoneId, bool deleting, CancellationToken ct)
    {
        var owner = await connection.QuerySingleOrDefaultAsync<WorkflowOwner>(new CommandDefinition(
            SQLQueries.SELECT_ClayzorTelWorkflowExactForUpdate, new { AttemptId = attemptId }, transaction,
            commandType: CommandType.Text, cancellationToken: ct));
        if (owner is null) return false;
        if (owner.EmployeeId != employeeId || owner.OriginalPhoneId != originalPhoneId || owner.Deleting != deleting)
            throw new InventoryOperationPendingException();
        return true;
    }

    internal static Task BeginAsync(DbManager db, string? principal, int employeeId, Guid attemptId,
        int phoneId, bool deleting, IReadOnlyList<string> fingerprints, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(phoneId);
        var steps = ValidatePlan(fingerprints);
        if (phoneId == 0 && (deleting || steps.Length != 0))
            throw new ArgumentException("Для нового номера недоступны удаление и команды до записи.");

        var conflictKey = Scope(phoneId);
        return InTransactionAsync(db, principal, employeeId, attemptId, async (connection, transaction, actor) =>
        {
            if (await InventoryOperationJournal.IsScopePendingForUpdateAsync(connection, transaction, conflictKey, attemptId, ct))
                throw new InventoryOperationPendingException();
            RequireAccepted(await ExecuteAsync(connection, transaction, SQLQueries.INSERT_ClayzorTelWorkflow,
                new { AttemptId = attemptId, EmployeeId = actor.Id, OriginalPhoneId = phoneId,
                    Deleting = deleting, PreStepCount = steps.Length, ConflictKey = conflictKey }, ct));
            await PrepareStepsAsync(connection, transaction, attemptId, actor.Id, beforeSql: true, steps, ct);
            return true;
        }, ct);
    }

    internal static Task MarkSqlReadyAsync(DbManager db, string? principal, int employeeId, Guid attemptId,
        IReadOnlyList<string> fingerprints, CancellationToken ct)
    {
        var steps = ValidatePlan(fingerprints);
        return InTransactionAsync(db, principal, employeeId, attemptId, async (connection, transaction, actor) =>
        {
            RequireAccepted(await ExecuteAsync(connection, transaction, SQLQueries.UPDATE_ClayzorTelWorkflowSqlReady,
                new { AttemptId = attemptId, EmployeeId = actor.Id, PostStepCount = steps.Length }, ct));
            await PrepareStepsAsync(connection, transaction, attemptId, actor.Id, beforeSql: false, steps, ct);
            return true;
        }, ct);
    }

    internal static Task<bool> BeginStepAsync(DbManager db, string? principal, int employeeId, Guid attemptId,
        int phoneId, bool beforeSql, int stepIndex, string fingerprint, CancellationToken ct) =>
        TransitionAsync(db, principal, employeeId, attemptId, SQLQueries.UPDATE_ClayzorTelWorkflowStepDispatched,
            StepArguments(attemptId, employeeId, phoneId, beforeSql, stepIndex, fingerprint),
            status => status switch
            {
                1 => true,
                2 => false,
                _ => throw new InventoryOperationPendingException(),
            }, ct);

    internal static Task ConfirmStepAsync(DbManager db, string? principal, int employeeId, Guid attemptId,
        int phoneId, bool beforeSql, int stepIndex, string fingerprint, CancellationToken ct) =>
        TransitionAsync(db, principal, employeeId, attemptId, SQLQueries.UPDATE_ClayzorTelWorkflowStepConfirmed,
            StepArguments(attemptId, employeeId, phoneId, beforeSql, stepIndex, fingerprint), RequireAccepted, ct);

    internal static Task CompleteAsync(DbManager db, string? principal, int employeeId, Guid attemptId, CancellationToken ct) =>
        TransitionAsync(db, principal, employeeId, attemptId, SQLQueries.UPDATE_ClayzorTelWorkflowCompleted,
            new { AttemptId = attemptId, EmployeeId = employeeId }, RequireAccepted, ct);

    internal static Task<bool> TryAbandonAsync(DbManager db, string? principal, int employeeId, Guid attemptId,
        CancellationToken ct) =>
        TransitionAsync(db, principal, employeeId, attemptId, SQLQueries.UPDATE_ClayzorTelWorkflowAbandoned,
            new { AttemptId = attemptId, EmployeeId = employeeId }, status => status switch
            {
                1 => true,
                0 => false,
                _ => throw new InventoryOperationPendingException(),
            }, ct);

    internal static async Task ConfirmSqlAsync(DbConnection connection, DbTransaction transaction, Guid attemptId,
        int employeeId, int originalPhoneId, int resultPhoneId, CancellationToken ct, bool deleting = false)
    {
        ValidateIdentity(employeeId, attemptId);
        ArgumentOutOfRangeException.ThrowIfNegative(originalPhoneId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(resultPhoneId);
        if (originalPhoneId > 0 && originalPhoneId != resultPhoneId)
            throw new ArgumentException("Исходный и принятый номера не совпадают.");

        RequireAccepted(await ExecuteAsync(connection, transaction, SQLQueries.UPDATE_ClayzorTelWorkflowSqlConfirmed,
            new { AttemptId = attemptId, EmployeeId = employeeId, OriginalPhoneId = originalPhoneId,
                ResultPhoneId = resultPhoneId, ResultConflictKey = Scope(resultPhoneId), Deleting = deleting }, ct));
    }

    internal static async Task RequireSqlReadyAsync(DbConnection connection, DbTransaction transaction, Guid attemptId,
        int employeeId, int originalPhoneId, bool deleting, CancellationToken ct) =>
        RequireAccepted(await ExecuteAsync(connection, transaction, SQLQueries.SELECT_ClayzorTelWorkflowSqlReady,
            new { AttemptId = attemptId, EmployeeId = employeeId, OriginalPhoneId = originalPhoneId, Deleting = deleting }, ct));

    private static object StepArguments(Guid attemptId, int employeeId, int phoneId, bool beforeSql, int stepIndex, string fingerprint)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(phoneId);
        ArgumentOutOfRangeException.ThrowIfNegative(stepIndex);
        return new { AttemptId = attemptId, EmployeeId = employeeId, PhoneId = phoneId, BeforeSql = beforeSql,
            StepIndex = stepIndex, Fingerprint = ValidateFingerprint(fingerprint) };
    }

    private static string ValidateFingerprint(string fingerprint)
    {
        if (fingerprint is not { Length: 64 } || fingerprint.Any(c => !char.IsAsciiHexDigit(c)))
            throw new ArgumentException("Требуется SHA-256 команды АТС.", nameof(fingerprint));
        return fingerprint.ToLowerInvariant();
    }

    private static string[] ValidatePlan(IReadOnlyList<string> fingerprints)
    {
        ArgumentNullException.ThrowIfNull(fingerprints);
        return fingerprints.Select(ValidateFingerprint).ToArray();
    }

    private static async Task PrepareStepsAsync(DbConnection connection, DbTransaction transaction, Guid attemptId,
        int employeeId, bool beforeSql, IReadOnlyList<string> steps, CancellationToken ct)
    {
        for (var i = 0; i < steps.Count; i++)
            RequireAccepted(await ExecuteAsync(connection, transaction, SQLQueries.INSERT_ClayzorTelWorkflowPreparedStep,
                new { AttemptId = attemptId, EmployeeId = employeeId, BeforeSql = beforeSql, StepIndex = i, Fingerprint = steps[i] }, ct));
    }

    private static Task<T> TransitionAsync<T>(DbManager db, string? principal, int employeeId, Guid attemptId,
        string sql, object arguments, Func<int, T> result, CancellationToken ct) =>
        InTransactionAsync(db, principal, employeeId, attemptId, async (connection, transaction, _) =>
            result(await ExecuteAsync(connection, transaction, sql, arguments, ct)), ct);

    private static Task<T> InTransactionAsync<T>(DbManager db, string? principal, int employeeId, Guid attemptId,
        Func<DbConnection, DbTransaction, Employee, Task<T>> action, CancellationToken ct)
    {
        ValidateIdentity(employeeId, attemptId);
        return db.RunDbAsync(async connection =>
        {
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var actor = TelSqlJournal.EnsureActor(await Employee.GetCurrentWithRolesAsync(connection, transaction, principal, ct),
                employeeId);

            var result = await action(connection, transaction, actor);
            await transaction.CommitAsync(CancellationToken.None);
            return result;
        });
    }

    private static string Scope(int phoneId) => phoneId == 0
        ? "tel/new" : "tel/" + phoneId.ToString(CultureInfo.InvariantCulture);

    private static void ValidateIdentity(int employeeId, Guid attemptId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(employeeId);
        if (attemptId == Guid.Empty)
            throw new ArgumentException("Требуется идентификатор операции Tel.", nameof(attemptId));
    }

    private static bool RequireAccepted(int status) =>
        status == 1 ? true : throw new InventoryOperationPendingException();

    private static Task<int> ExecuteAsync(DbConnection connection, DbTransaction transaction, string sql, object arguments,
        CancellationToken ct) =>
        connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, arguments, transaction,
            commandType: CommandType.Text, cancellationToken: ct));
}
