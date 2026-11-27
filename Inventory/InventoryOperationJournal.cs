using System.Data;
using System.Data.Common;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Состояние долговечной попытки изменения Inventory.</summary>
public enum InventoryOperationState : byte
{
    /// <summary>Попытка зарезервирована. Отсутствие подтверждения не разрешает повтор.</summary>
    Reserved = 0,
    /// <summary>Зафиксировано начало отправки внешней команды.</summary>
    Dispatching = 1,
    /// <summary>Исход отправленной команды не установлен.</summary>
    Unknown = 2,
    /// <summary>Исход подтверждён сервером.</summary>
    Confirmed = 3,
    /// <summary>Подтверждён отказ без эффекта попытки.</summary>
    Rejected = 4,
}

/// <summary>Идентификатор и подтверждённое серверное состояние своей попытки.</summary>
public sealed record InventoryOperationReceipt(Guid AttemptId, InventoryOperationState State, int? ResultEntityId);

/// <summary>Новая отправка запрещена до выяснения исхода прежнего действия.</summary>
public sealed class InventoryOperationPendingException : InvalidOperationException
{
    /// <summary>Сообщает, что сервер не разрешил продолжить до сверки незавершённой попытки.</summary>
    public InventoryOperationPendingException() : this(blockedBeforeReservation: false) { }

    internal InventoryOperationPendingException(bool blockedBeforeReservation)
        : base("Есть незавершённая попытка по этой записи. Перед повтором проверьте её исход.") =>
        BlockedBeforeReservation = blockedBeforeReservation;

    internal bool BlockedBeforeReservation { get; }
}

/// <summary>Резервирование не подтверждено, поэтому бизнес-команда не была отправлена.</summary>
public sealed class InventoryOperationReservationException(Exception inner)
    : InvalidOperationException("Начало попытки не подтверждено. Запись не отправлена.", inner);

internal sealed class InventoryOperationPreparationException()
    : InvalidOperationException("Не удалось проверить исходные данные. Запись не отправлена.");

internal static class InventoryOperationJournal
{
    internal sealed record Attempt(Guid AttemptId, int EmployeeId, string ConflictKey, string CommandKind,
        IReadOnlyList<string>? OtherScopes);

    private sealed record StoredAttempt(Guid AttemptId, int EmployeeId, string ConflictKey, string CommandKind,
        byte State, int? ResultEntityId)
    {
        internal InventoryOperationReceipt Receipt => new(AttemptId, (InventoryOperationState)State, ResultEntityId);
    }

    internal static Attempt ForEmployee(Employee employee, Guid attemptId, string conflictKey, string commandKind,
        IReadOnlyCollection<string>? otherScopes = null)
    {
        if (employee.Id <= 0 || attemptId == Guid.Empty ||
            string.IsNullOrWhiteSpace(conflictKey) || conflictKey.Length > 256 ||
            string.IsNullOrWhiteSpace(commandKind) || commandKind.Length > 64 ||
            otherScopes?.Any(scope => string.IsNullOrWhiteSpace(scope) || scope.Length > 256) == true)
            throw new ArgumentException("Недопустимый контекст попытки Inventory.");

        var normalizedScopes = otherScopes?.Where(scope => scope != conflictKey).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray();
        return new Attempt(attemptId, employee.Id, conflictKey, commandKind, normalizedScopes);
    }

    internal static Task<InventoryOperationReceipt> RunSqlAsync<TRejected>(
        DbManager db, Attempt attempt,
        Func<DbConnection, DbTransaction, CancellationToken, Task<int?>> write,
        CancellationToken ct, Func<DbConnection, DbTransaction, CancellationToken, Task>? prepare = null)
        where TRejected : Exception =>
        RunSqlCoreAsync<TRejected>(db, attempt, write, ct, createRedactedFailure: null, prepare);

    internal static Task<InventoryOperationReceipt> RunProtectedSqlAsync<TRejected>(
        DbManager db, Attempt attempt,
        Func<DbConnection, DbTransaction, CancellationToken, Task<int?>> write,
        Func<Exception> createRedactedFailure, CancellationToken ct,
        Func<DbConnection, DbTransaction, CancellationToken, Task>? prepare = null) where TRejected : Exception
    {
        ArgumentNullException.ThrowIfNull(createRedactedFailure);
        return RunSqlCoreAsync<TRejected>(db, attempt, write, ct, createRedactedFailure, prepare);
    }

    private static async Task<InventoryOperationReceipt> RunSqlCoreAsync<TRejected>(
        DbManager db, Attempt attempt,
        Func<DbConnection, DbTransaction, CancellationToken, Task<int?>> write,
        CancellationToken ct, Func<Exception>? createRedactedFailure,
        Func<DbConnection, DbTransaction, CancellationToken, Task>? prepare) where TRejected : Exception
    {
        if (await ReserveForDispatchAsync(db, attempt, ct) is { } previous)
            return previous;

        var outcome = await db.RunDbAsync(async connection =>
        {
            try
            {
                await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                Task ConfirmAsync(int? id) => TransitionAsync(connection, transaction, attempt,
                    InventoryOperationState.Reserved, InventoryOperationState.Confirmed, id, ct);
                int? resultId;
                try
                {
                    if (prepare is not null && !await TryPrepareSqlWriteAsync(prepare, connection, transaction, ct))
                    {
                        await transaction.RollbackAsync(CancellationToken.None);
                        return (Receipt: (InventoryOperationReceipt?)null, Rejection: (Exception?)new InventoryOperationPreparationException());
                    }
                    resultId = await write(connection, transaction, ct);
                    if (createRedactedFailure is null)
                        await ConfirmAsync(resultId);
                }
                catch (TRejected error) when (createRedactedFailure is null || error.GetType() == typeof(TRejected) &&
                    error.InnerException is null && error is not (DbException or OperationCanceledException))
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return (Receipt: (InventoryOperationReceipt?)null, Rejection: (Exception?)error);
                }

                if (createRedactedFailure is not null)
                    await ConfirmAsync(resultId);
                await transaction.CommitAsync(CancellationToken.None);
                return (Receipt: (InventoryOperationReceipt?)new InventoryOperationReceipt(attempt.AttemptId,
                    InventoryOperationState.Confirmed, resultId), Rejection: (Exception?)null);
            }
            catch (Exception) when (createRedactedFailure is not null)
            {
                // Включая откат и освобождение транзакции. SQL-обработчик не получает исходное исключение.
                throw createRedactedFailure();
            }
        });

        if (outcome.Rejection is { } rejection)
        {
            await TransitionOutsideAsync(db, attempt, InventoryOperationState.Reserved,
                InventoryOperationState.Rejected, null, CancellationToken.None);
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(rejection).Throw();
        }
        return outcome.Receipt ?? throw new InvalidOperationException("Журнал не подтвердил результат записи.");
    }

    private static async Task<bool> TryPrepareSqlWriteAsync(Func<DbConnection, DbTransaction, CancellationToken, Task> prepare,
        DbConnection connection, DbTransaction transaction, CancellationToken ct)
    {
        try
        {
            await prepare(connection, transaction, ct);
            return true;
        }
        catch (Exception error) when (error is DbException or TimeoutException or OperationCanceledException)
        {
            return false;
        }
    }

    internal static async Task<InventoryOperationReceipt> RunExternalAsync<TRejected>(DbManager db, Attempt attempt,
        Func<CancellationToken, Task<int?>> send, CancellationToken ct) where TRejected : Exception
    {
        if (await ReserveForDispatchAsync(db, attempt, ct) is { } previous)
            return previous;

        await TransitionOutsideAsync(db, attempt, InventoryOperationState.Reserved, InventoryOperationState.Dispatching, null, ct);
        int? result;
        try
        {
            result = await send(ct);
        }
        catch (TRejected)
        {
            await TransitionOutsideAsync(db, attempt, InventoryOperationState.Dispatching,
                InventoryOperationState.Rejected, null, CancellationToken.None);
            throw;
        }

        // Отсутствие подтверждения оставляет Dispatching открытым. Новая попытка не повторяет команду.
        await TransitionOutsideAsync(db, attempt, InventoryOperationState.Dispatching,
            InventoryOperationState.Confirmed, result, CancellationToken.None);
        return new InventoryOperationReceipt(attempt.AttemptId, InventoryOperationState.Confirmed, result);
    }

    internal static async Task<bool> IsScopePendingAsync(DbManager db, string conflictKey, CancellationToken ct) =>
        (await db.QueryRequiredAsync<bool>(SQLQueries.SELECT_ClayzorOperationJournalOpenSlot,
            new { ConflictKey = conflictKey, OpenSlot = Guid.Empty }, ct)).Single();

    internal static Task<bool> IsScopePendingForUpdateAsync(DbConnection connection, DbTransaction transaction,
        string conflictKey, Guid attemptId, CancellationToken ct) =>
        connection.QuerySingleAsync<bool>(Command(SQLQueries.SELECT_ClayzorOperationJournalOpenSlotForUpdate,
            new { ConflictKey = conflictKey, AttemptId = attemptId, OpenSlot = Guid.Empty }, transaction, ct));

    private static async Task<InventoryOperationReceipt?> ReserveForDispatchAsync(DbManager db, Attempt attempt, CancellationToken ct)
    {
        InventoryOperationReceipt? previous;
        try
        {
            previous = await ReserveAsync(db, attempt, ct);
        }
        catch (Exception error) when (error is not InventoryOperationPendingException and not OperationCanceledException)
        {
            throw new InventoryOperationReservationException(error);
        }
        if (previous is not null && previous.State is not (InventoryOperationState.Confirmed or InventoryOperationState.Rejected))
            throw new InventoryOperationPendingException();
        return previous;
    }

    internal static async Task<InventoryOperationReceipt?> ReadLatestAsync(DbManager db, Employee employee,
        string conflictKey, CancellationToken ct)
    {
        var row = await db.RunDbAsync(connection => connection.QuerySingleOrDefaultAsync<StoredAttempt>(
            Command(SQLQueries.SELECT_ClayzorOperationJournalLatest,
                new { EmployeeId = employee.Id, ConflictKey = conflictKey }, null, ct)));
        return row?.Receipt;
    }

    internal static async Task<InventoryOperationReceipt?> ReadLatestAsync(DbManager db, Employee employee,
        string conflictKey, string commandKind, CancellationToken ct)
    {
        var row = await db.RunDbAsync(connection => connection.QuerySingleOrDefaultAsync<StoredAttempt>(
            Command(SQLQueries.SELECT_ClayzorOperationJournalLatestByKind,
                new { EmployeeId = employee.Id, ConflictKey = conflictKey, CommandKind = commandKind }, null, ct)));
        if (row is null)
            return null;
        if (row.AttemptId == Guid.Empty || row.EmployeeId != employee.Id || row.ConflictKey != conflictKey ||
            row.CommandKind != commandKind || row.State > (byte)InventoryOperationState.Rejected)
            throw new InvalidOperationException("Не удалось подтвердить запись серверного журнала.");
        return row.Receipt;
    }

    internal static async Task<InventoryOperationReceipt?> ReadLatestAsync(DbManager db, Employee employee,
        string conflictKey, IReadOnlyCollection<string> commandKinds, CancellationToken ct)
    {
        var row = await db.RunDbAsync(connection => connection.QuerySingleOrDefaultAsync<StoredAttempt>(
            Command(SQLQueries.SELECT_ClayzorOperationJournalLatestByKinds,
                new { EmployeeId = employee.Id, ConflictKey = conflictKey, CommandKinds = commandKinds }, null, ct)));
        if (row is null)
            return null;
        if (row.AttemptId == Guid.Empty || row.EmployeeId != employee.Id || row.ConflictKey != conflictKey ||
            !commandKinds.Contains(row.CommandKind, StringComparer.Ordinal) || row.State > (byte)InventoryOperationState.Rejected)
            throw new InvalidOperationException("Не удалось подтвердить запись серверного журнала.");
        return row.Receipt;
    }

    internal static Task<InventoryOperationReceipt?> ReadAttemptAsync(DbManager db, Attempt attempt, CancellationToken ct) =>
        db.RunDbAsync(connection => ReadAttemptInTransactionAsync(connection, null, attempt, ct));

    internal static async Task WriteTextResultAsync(DbConnection connection, DbTransaction transaction, Guid attemptId,
        string resultKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(resultKey) || resultKey.Length > 128)
            throw new InvalidOperationException("База не подтвердила ключ результата.");
        var written = await connection.ExecuteScalarAsync<int>(Command(SQLQueries.INSERT_ClayzorOperationTextReceipt,
            new { AttemptId = attemptId, ResultKey = resultKey }, transaction, ct));
        if (written != 1)
            throw new InvalidOperationException("Не удалось связать ключ с текущей попыткой.");
    }

    internal static Task<string?> ReadTextResultAsync(DbManager db, Guid attemptId, CancellationToken ct) =>
        db.RunDbAsync(connection => ReadTextResultInTransactionAsync(connection, null, attemptId, ct));

    internal static Task<string?> ReadTextResultInTransactionAsync(DbConnection connection, DbTransaction? transaction,
        Guid attemptId, CancellationToken ct) =>
        connection.QuerySingleOrDefaultAsync<string>(Command(SQLQueries.SELECT_ClayzorOperationTextReceipt,
            new { AttemptId = attemptId }, transaction, ct));

    internal static async Task<InventoryOperationReceipt?> ReadAttemptInTransactionAsync(DbConnection connection,
        DbTransaction? transaction, Attempt attempt, CancellationToken ct)
    {
        var row = await connection.QuerySingleOrDefaultAsync<StoredAttempt>(
            Command(SQLQueries.SELECT_ClayzorOperationJournalAttemptForUpdate, new { attempt.AttemptId }, transaction, ct));
        if (row is null) return null;
        await EnsureSameAttemptAsync(connection, transaction, row, attempt, ct);
        return row.Receipt;
    }

    internal static async Task<InventoryOperationReceipt?> ReadAttemptContainingScopesAsync(DbConnection connection,
        DbTransaction? transaction, Attempt attempt, CancellationToken ct)
    {
        // Сервер может зарезервировать дополнительные связанные области при подготовке операции.
        var receipt = await ReadAttemptInTransactionAsync(connection, transaction, attempt with { OtherScopes = null }, ct);
        if (receipt is null) return null;
        var stored = (await connection.QueryAsync<string>(Command(SQLQueries.SELECT_ClayzorOperationScope,
            new { attempt.AttemptId }, transaction, ct))).ToHashSet(StringComparer.Ordinal);
        if (attempt.OtherScopes?.Any(scope => !stored.Contains(scope)) == true)
            throw new InventoryOperationPendingException();
        return receipt;
    }

    internal static Task<IEnumerable<string>> ReadOpenScopesAsync(DbManager db, string commandKind, CancellationToken ct) =>
        db.RunDbAsync(connection => connection.QueryAsync<string>(
            Command(SQLQueries.SELECT_ClayzorOperationJournalOpenScopes,
                new { CommandKind = commandKind, OpenSlot = Guid.Empty }, null, ct)));

    internal static Task ImportLegacyAsync(DbManager db, IReadOnlyCollection<Attempt> attempts, CancellationToken ct) =>
        db.RunDbAsync(async connection =>
        {
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            foreach (var attempt in attempts)
                await ImportLegacyAttemptAsync(connection, transaction, attempt, ct);

            await transaction.CommitAsync(CancellationToken.None);
            return true;
        });

    internal static async Task ImportLegacyAttemptAsync(DbConnection connection, DbTransaction transaction, Attempt attempt,
        CancellationToken ct, bool retainEvenIfJournalExists = false)
    {
        var sqlAttempt = retainEvenIfJournalExists ? attempt with { OtherScopes = null } : attempt;
        var existing = await ReadAttemptInTransactionAsync(connection, transaction, sqlAttempt, ct);
        if (existing is not null && !retainEvenIfJournalExists)
            return;

        var owned = await connection.QuerySingleAsync<bool>(
            Command(SQLQueries.INSERT_ClayzorLegacyOperationEvidence, attempt, transaction, ct));
        if (!owned)
            throw new InventoryOperationPendingException();

        foreach (var scope in attempt.OtherScopes ?? [])
        {
            var inserted = await connection.ExecuteScalarAsync<int>(Command(SQLQueries.INSERT_ClayzorLegacyOperationScope,
                new { attempt.AttemptId, ConflictKey = scope }, transaction, ct));
            if (inserted != 1)
                throw new InventoryOperationPendingException();
        }
    }

    private static Task<InventoryOperationReceipt?> ReserveAsync(DbManager db, Attempt attempt, CancellationToken ct) =>
        db.RunDbAsync<InventoryOperationReceipt?>(async connection =>
        {
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var previous = await connection.QuerySingleOrDefaultAsync<StoredAttempt>(
                Command(SQLQueries.SELECT_ClayzorOperationJournalAttemptForUpdate, new { attempt.AttemptId }, transaction, ct));
            if (previous is not null)
            {
                await EnsureSameAttemptAsync(connection, transaction, previous, attempt, ct);
                await transaction.CommitAsync(CancellationToken.None);
                return previous.Receipt;
            }

            var scopes = (attempt.OtherScopes ?? []).Append(attempt.ConflictKey).Order(StringComparer.Ordinal);
            foreach (var scope in scopes)
            {
                if (await IsScopePendingForUpdateAsync(connection, transaction, scope, attempt.AttemptId, ct))
                    throw new InventoryOperationPendingException(blockedBeforeReservation: true);
            }

            var inserted = await connection.ExecuteAsync(Command(SQLQueries.INSERT_ClayzorOperationJournalAttempt,
                new { attempt.AttemptId, attempt.EmployeeId, attempt.ConflictKey, attempt.CommandKind, OpenSlot = Guid.Empty },
                transaction, ct));
            // SQL Server может не вернуть число строк при SET NOCOUNT ON. Следующий переход всё равно требует точную строку попытки.
            if (inserted is not (1 or -1))
                throw new InvalidOperationException("Не удалось зарезервировать попытку.");

            foreach (var scope in attempt.OtherScopes ?? [])
            {
                var affected = await connection.ExecuteScalarAsync<int>(Command(SQLQueries.INSERT_ClayzorOperationScope,
                    new { attempt.AttemptId, ConflictKey = scope, OpenSlot = Guid.Empty }, transaction, ct));
                if (affected != 1)
                    throw new InvalidOperationException("Не удалось зарезервировать связанную запись.");
            }
            await transaction.CommitAsync(CancellationToken.None);
            return null;
        });

    private static async Task EnsureSameAttemptAsync(DbConnection connection, DbTransaction? transaction,
        StoredAttempt previous, Attempt attempt, CancellationToken ct)
    {
        if (previous.EmployeeId != attempt.EmployeeId || previous.ConflictKey != attempt.ConflictKey ||
            previous.CommandKind != attempt.CommandKind)
            throw new InventoryOperationPendingException();
        if (attempt.OtherScopes is not { } expected)
            return;

        var stored = await connection.QueryAsync<string>(Command(SQLQueries.SELECT_ClayzorOperationScope,
            new { attempt.AttemptId }, transaction, ct));
        if (!stored.Order(StringComparer.Ordinal).SequenceEqual(expected, StringComparer.Ordinal))
            throw new InventoryOperationPendingException();
    }

    private static Task<int> TransitionOutsideAsync(DbManager db, Attempt attempt,
        InventoryOperationState expected, InventoryOperationState next, int? resultId, CancellationToken ct) =>
        db.RunDbAsync(async connection =>
        {
            if (attempt.OtherScopes is not { Count: > 0 } || !IsTerminal(next))
                return await TransitionAsync(connection, null, attempt, expected, next, resultId, ct);

            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var affected = await TransitionAsync(connection, transaction, attempt, expected, next, resultId, ct);
            await transaction.CommitAsync(CancellationToken.None);
            return affected;
        });

    private static bool IsTerminal(InventoryOperationState state) =>
        state is InventoryOperationState.Confirmed or InventoryOperationState.Rejected;

    private static async Task<int> TransitionAsync(DbConnection connection, DbTransaction? transaction,
        Attempt attempt, InventoryOperationState expected, InventoryOperationState next, int? resultId,
        CancellationToken ct)
    {
        var affected = await connection.ExecuteScalarAsync<int>(Command(SQLQueries.UPDATE_ClayzorOperationJournalState,
            new
            {
                attempt.AttemptId, attempt.EmployeeId, attempt.ConflictKey, attempt.CommandKind,
                ExpectedState = (byte)expected, NextState = (byte)next,
                Settled = IsTerminal(next),
                ResultEntityId = resultId, OpenSlot = Guid.Empty,
            },
            transaction, ct));
        if (affected != 1)
            throw new InvalidOperationException("Не удалось связать исход с текущей попыткой.");

        if (attempt.OtherScopes is { Count: > 0 } linked && IsTerminal(next))
        {
            var released = await connection.ExecuteScalarAsync<int>(Command(SQLQueries.UPDATE_ClayzorOperationScope,
                new { attempt.AttemptId, OpenSlot = Guid.Empty }, transaction, ct));
            if (released != linked.Count)
                throw new InvalidOperationException("Не удалось освободить все связанные записи.");
        }
        return affected;
    }

    private static CommandDefinition Command(string sql, object parameters, DbTransaction? transaction, CancellationToken ct) =>
        new(sql, parameters, transaction, commandType: CommandType.Text, cancellationToken: ct);
}
