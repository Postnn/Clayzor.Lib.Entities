using System.Data.Common;
using Clayzor.Lib.DALC;
using Clayzor.Lib.Entities.Inventory;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Clayzor.Lib.Entities.TerritorialEntity;

/// <summary>Долговечное подтверждение команд над строковым ключом телефонного кода.</summary>
public static partial class TerritorialEntityData
{
    private const string PhoneInsertKind = "phone-code.insert";
    private const string PhoneUpdateKind = "phone-code.update";
    private const string PhoneDeleteKind = "phone-code.delete";

    /// <summary>Сохраняет код и атомарно фиксирует его строковый результат в серверном журнале.</summary>
    public static async Task<PhoneCodeEntityRecord?> SavePhoneAsync(DbManager db, string? principal, int employeeId,
        Guid attemptId, PhoneCodeEntityRecord? original, PhoneCodeEntityRecord draft, CancellationToken ct = default)
    {
        if (original is not null && !string.Equals(original.Code, draft.Code, StringComparison.Ordinal))
            throw new TerritorialEntityRuleException("Телефонный код существующей записи изменять нельзя.");
        CheckValidation(TerritorialEntityRules.Validate(draft));
        EnsurePhoneKey(draft.Code);
        return await ExecutePhoneWriteAsync(db, principal, employeeId, attemptId,
            original is null ? PhoneInsertKind : PhoneUpdateKind, original, draft, ct);
    }

    /// <summary>Удаляет только неизменённый телефонный код и атомарно подтверждает удалённый ключ.</summary>
    public static async Task DeletePhoneAsync(DbManager db, string? principal, int employeeId, Guid attemptId,
        PhoneCodeEntityRecord original, CancellationToken ct = default)
    {
        EnsurePhoneKey(original.Code);
        _ = await ExecutePhoneWriteAsync(db, principal, employeeId, attemptId, PhoneDeleteKind, original, null, ct);
    }

    /// <summary>Читает блокировку и подтверждённый строковый результат сотрудника без повторной записи.</summary>
    public static async Task<(bool HasPending, string? LastConfirmedCode)> ReadPhoneWriteStateAsync(DbManager db,
        string? principal, int employeeId, string? originalCode, CancellationToken ct)
    {
        if (originalCode is not null) EnsurePhoneKey(originalCode);
        var actor = await RequirePhoneWriterAsync(db, principal, employeeId, null, ct);
        var scope = PhoneWriteScope(originalCode);
        var latest = await InventoryOperationJournal.ReadLatestAsync(db, actor, scope, ct);
        var code = latest is { State: InventoryOperationState.Confirmed }
            ? await InventoryOperationJournal.ReadTextResultAsync(db, latest.AttemptId, ct) : null;
        if (originalCode is not null && !string.Equals(code, originalCode, StringComparison.Ordinal))
            code = null;
        return (await InventoryOperationJournal.IsScopePendingAsync(db, scope, ct), code);
    }

    /// <summary>Проверяет точный GUID, действующие права, область и строковый результат перед сверкой.</summary>
    public static async Task<InventoryOperationReceipt?> ReadPhoneWriteReceiptAsync(DbManager db, string? principal,
        int employeeId, string kind, Guid attemptId, string? originalCode, string submittedCode, CancellationToken ct)
    {
        if (kind is not (PhoneInsertKind or PhoneUpdateKind or PhoneDeleteKind) ||
            (kind == PhoneInsertKind) != (originalCode is null) ||
            originalCode is not null && !string.Equals(originalCode, submittedCode, StringComparison.Ordinal))
            return null;
        EnsurePhoneKey(submittedCode);
        var actor = await RequirePhoneWriterAsync(db, principal, employeeId, kind, ct);
        var attempt = PhoneAttempt(actor, kind, attemptId, originalCode, submittedCode);
        var receipt = await InventoryOperationJournal.ReadAttemptAsync(db, attempt, ct);
        if (receipt is { State: InventoryOperationState.Confirmed } &&
            !string.Equals(await InventoryOperationJournal.ReadTextResultAsync(db, attemptId, ct), submittedCode, StringComparison.Ordinal))
            return null;
        return receipt;
    }

    /// <summary>Переносит только проверенные ключи прежних попыток, сверяя точные серверные результаты.</summary>
    public static async Task ImportLegacyPhoneOperationsAsync(DbManager db, string? principal, int employeeId,
        string? visibleCode, IReadOnlyCollection<(Guid AttemptId, string Kind, string? Source, string Target)> evidence,
        CancellationToken ct)
    {
        if (evidence.Count == 0) return;
        if (visibleCode is not null) EnsurePhoneKey(visibleCode);
        var attempts = new List<InventoryOperationJournal.Attempt>(evidence.Count);
        var writers = new Dictionary<string, Employee>(StringComparer.Ordinal);
        foreach (var (attemptId, kind, source, target) in evidence)
        {
            EnsurePhoneKey(target);
            if (attemptId == Guid.Empty || !string.Equals(source, visibleCode, StringComparison.Ordinal) ||
                (visibleCode is null
                    ? kind != PhoneInsertKind
                    : kind is not (PhoneUpdateKind or PhoneDeleteKind) ||
                        !string.Equals(source, target, StringComparison.Ordinal)))
                throw new TerritorialEntityRuleException("Прежняя попытка относится к другому телефонному коду.");

            if (!writers.TryGetValue(kind, out var actor))
            {
                actor = await RequirePhoneWriterAsync(db, principal, employeeId, kind, ct);
                if (visibleCode is not null && !string.Equals((await ReadPhoneAsync(db, visibleCode, ct))?.Code,
                    visibleCode, StringComparison.Ordinal))
                    throw Denied();
                writers.Add(kind, actor);
            }

            var attempt = PhoneAttempt(actor, kind, attemptId, source, target);
            if (await InventoryOperationJournal.ReadAttemptAsync(db, attempt, ct) is { } previous)
            {
                if (previous.State == InventoryOperationState.Confirmed &&
                    !string.Equals(await InventoryOperationJournal.ReadTextResultAsync(db, attemptId, ct), target, StringComparison.Ordinal))
                    throw new InventoryOperationPendingException();
                continue;
            }
            attempts.Add(attempt);
        }
        if (attempts.Count != 0)
            await InventoryOperationJournal.ImportLegacyAsync(db, attempts, ct);
    }

    private static async Task<PhoneCodeEntityRecord?> ExecutePhoneWriteAsync(DbManager db, string? principal, int employeeId,
        Guid attemptId, string kind, PhoneCodeEntityRecord? original, PhoneCodeEntityRecord? draft, CancellationToken ct)
    {
        var deleting = kind == PhoneDeleteKind;
        var code = original?.Code ?? draft!.Code;
        var actor = await RequirePhoneWriterAsync(db, principal, employeeId, kind, ct);
        var attempt = PhoneAttempt(actor, kind, attemptId, original?.Code, code);
        if (await InventoryOperationJournal.ReadAttemptAsync(db, attempt, ct) is { } previous)
            return await ReadConfirmedPhoneAsync(previous);

        var key = new { Code = Ansi(code, 20) };
        await db.RunDbAsync(connection => CheckPhoneProposalAsync(connection, null, original, draft, key, ct));
        PhoneCodeEntityRecord? saved = null;
        var wrote = false;
        try
        {
            var receipt = await InventoryOperationJournal.RunSqlAsync<PhoneWriteRejectedException>(db, attempt,
                async (connection, transaction, token) =>
                {
                    try
                    {
                        var fresh = await Employee.GetCurrentWithRolesAsync(connection, transaction, principal, token);
                        EnsurePhoneActor(fresh, employeeId);
                        var rights = await connection.QuerySingleAsync<TerritorialEntityAccess>(
                            new CommandDefinition(TerritorialEntitySql.Access, transaction: transaction, cancellationToken: token));
                        if (!PhoneGrant(rights, kind)) throw Denied();
                        await CheckPhoneProposalAsync(connection, transaction, original, draft, key, token);
                        if (deleting)
                            await connection.ExecuteAsync(new CommandDefinition(TerritorialEntitySql.PhoneDelete, key,
                                transaction, cancellationToken: token));
                        else
                            await connection.ExecuteAsync(new CommandDefinition(
                                kind == PhoneInsertKind ? TerritorialEntitySql.PhoneInsert : TerritorialEntitySql.PhoneUpdate,
                                new { key.Code, draft!.TerritoryId, Length = (byte)draft.Length, draft.TypeId, Comment = Ansi(draft.Comment, 100) },
                                transaction, cancellationToken: token));
                        saved = await ReadSingleAsync<PhoneCodeEntityRecord>(connection, transaction,
                            string.Format(TerritorialEntitySql.PhoneRead, ""), key, token);
                        if (deleting ? saved is not null :
                            saved is null || saved with { ChangedBy = draft!.ChangedBy, ChangedByName = draft.ChangedByName, ChangedAt = draft.ChangedAt } != draft)
                            throw new TerritorialEntityRuleException(deleting
                                ? "База не подтвердила удаление телефонного кода."
                                : "База не подтвердила сохранение телефонного кода.");

                        await InventoryOperationJournal.WriteTextResultAsync(connection, transaction, attemptId, saved?.Code ?? code, token);
                        wrote = true;
                        return null;
                    }
                    catch (Exception error) when (error is TerritorialEntityRuleException or UnauthorizedAccessException ||
                        error is SqlException sql && sql.Number != -2 && !DbManager.IsConnectivityError(sql) && transaction.Connection is not null)
                    {
                        throw new PhoneWriteRejectedException(error);
                    }
                }, ct);
            return wrote ? saved : await ReadConfirmedPhoneAsync(receipt);
        }
        catch (PhoneWriteRejectedException rejected)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(rejected.InnerException!).Throw();
            throw;
        }
        catch (Exception error) when (error is not (TerritorialEntityRuleException or InventoryOperationPendingException
            or InventoryOperationReservationException or OperationCanceledException))
        {
            throw new InvalidOperationException("Исход записи телефонного кода требует серверной сверки.", error);
        }

        async Task<PhoneCodeEntityRecord?> ReadConfirmedPhoneAsync(InventoryOperationReceipt receipt)
        {
            if (receipt.State == InventoryOperationState.Rejected)
                throw new TerritorialEntityRuleException("Эта попытка была отклонена. Перечитайте телефонный код.");
            if (receipt.State != InventoryOperationState.Confirmed ||
                !string.Equals(await InventoryOperationJournal.ReadTextResultAsync(db, receipt.AttemptId, ct), code, StringComparison.Ordinal))
                throw new InventoryOperationPendingException();
            if (deleting) return null;
            try { return await ReadPhoneAsync(db, code, ct); }
            catch (Exception error) when (error is DbException or TimeoutException or OperationCanceledException)
            {
                // Ошибка перечитывания не меняет уже подтверждённый исход.
                return null;
            }
        }
    }

    private static async Task<bool> CheckPhoneProposalAsync(DbConnection connection, DbTransaction? transaction,
        PhoneCodeEntityRecord? original, PhoneCodeEntityRecord? draft, object key, CancellationToken ct)
    {
        var current = await connection.QuerySingleOrDefaultAsync<PhoneCodeEntityRecord>(new CommandDefinition(
            string.Format(TerritorialEntitySql.PhoneRead, transaction is null ? "" : RowLock), key, transaction, cancellationToken: ct));
        if (original is null && current is not null)
            throw new TerritorialEntityRuleException("Такой телефонный код уже существует.");
        if (current != original) throw Changed();
        if (draft is null) return true;
        var territory = await connection.QuerySingleAsync<int>(new CommandDefinition(TerritorialEntitySql.PhoneTerritoryCheck,
            new { draft.TerritoryId, Types = PhoneTerritoryTypes }, transaction, cancellationToken: ct));
        if (territory != 1)
            throw new TerritorialEntityRuleException("Выбранная территория отсутствует или не подходит для телефонного кода.");
        var phoneType = await connection.QuerySingleAsync<int>(new CommandDefinition(TerritorialEntitySql.PhoneTypeCheck,
            new { draft.TypeId }, transaction, cancellationToken: ct));
        if (phoneType != 1) throw new TerritorialEntityRuleException("Тип телефонной связи не найден.");
        return true;
    }

    private static async Task<Employee> RequirePhoneWriterAsync(DbManager db, string? principal, int employeeId, string? kind,
        CancellationToken ct)
    {
        var actor = await Employee.GetCurrentWithRolesAsync(db, principal, ct);
        EnsurePhoneActor(actor, employeeId);
        var access = await ReadAccessAsync(db, actor, ct);
        if (!PhoneGrant(access, kind)) throw Denied();
        return actor!;
    }

    private static void EnsurePhoneActor(Employee? actor, int employeeId)
    {
        if (employeeId <= 0 || actor?.Id != employeeId || !actor.HasRole(Роли.Администратор_тарифов_связи))
            throw Denied();
    }

    private static bool PhoneGrant(TerritorialEntityAccess access, string? kind) => kind switch
    {
        PhoneInsertKind => access.PhoneInsert,
        PhoneUpdateKind => access.PhoneUpdate,
        PhoneDeleteKind => access.PhoneDelete,
        _ => access.PhoneInsert || access.PhoneUpdate || access.PhoneDelete,
    };

    private static InventoryOperationJournal.Attempt PhoneAttempt(Employee actor, string kind, Guid id,
        string? originalCode, string submittedCode) =>
        InventoryOperationJournal.ForEmployee(actor, id, PhoneWriteScope(originalCode), kind,
            originalCode is null ? [PhoneWriteScope(submittedCode)] : null);

    private static string PhoneWriteScope(string? code) => code switch
    {
        null => "phone-code/new",
        "new" => "phone-code/%6Eew",
        _ => $"phone-code/{Uri.EscapeDataString(code)}",
    };

    private static void EnsurePhoneKey(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 20)
            throw new TerritorialEntityRuleException("Некорректный ключ телефонного кода.");
    }

    private sealed class PhoneWriteRejectedException(Exception error) : InvalidOperationException(error.Message, error);
}
