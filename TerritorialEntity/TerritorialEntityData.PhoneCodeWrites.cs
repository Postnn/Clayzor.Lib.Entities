using System.Data;
using System.Data.Common;
using Clayzor.Lib.DALC;
using Clayzor.Lib.Entities.Inventory;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Clayzor.Lib.Entities.TerritorialEntity;

/// <summary>Транзакционные команды над строковым ключом телефонного кода.</summary>
public static partial class TerritorialEntityData
{
    private const string PhoneInsertKind = "phone-code.insert";
    private const string PhoneUpdateKind = "phone-code.update";
    private const string PhoneDeleteKind = "phone-code.delete";

    /// <summary>Проверяет и сохраняет телефонный код.</summary>
    public static async Task<PhoneCodeEntityRecord?> SavePhoneAsync(DbManager db, string? principal, int employeeId,
        PhoneCodeEntityRecord? original, PhoneCodeEntityRecord draft, CancellationToken ct = default)
    {
        if (original is not null && !string.Equals(original.Code, draft.Code, StringComparison.Ordinal))
            throw new TerritorialEntityRuleException("Телефонный код существующей записи изменять нельзя.");
        CheckValidation(TerritorialEntityRules.Validate(draft));
        EnsurePhoneKey(draft.Code);
        return await ExecutePhoneWriteAsync(db, principal, employeeId,
            original is null ? PhoneInsertKind : PhoneUpdateKind, original, draft, ct);
    }

    /// <summary>Удаляет только неизменённый телефонный код.</summary>
    public static async Task DeletePhoneAsync(DbManager db, string? principal, int employeeId,
        PhoneCodeEntityRecord original, CancellationToken ct = default)
    {
        EnsurePhoneKey(original.Code);
        _ = await ExecutePhoneWriteAsync(db, principal, employeeId, PhoneDeleteKind, original, null, ct);
    }

    private static async Task<PhoneCodeEntityRecord?> ExecutePhoneWriteAsync(DbManager db, string? principal, int employeeId,
        string kind, PhoneCodeEntityRecord? original, PhoneCodeEntityRecord? draft, CancellationToken ct)
    {
        var deleting = kind == PhoneDeleteKind;
        var code = original?.Code ?? draft!.Code;
        await RequirePhoneWriterAsync(db, principal, employeeId, kind, ct);
        var key = new { Code = Ansi(code, 20) };
        await db.RunDbAsync(connection => CheckPhoneProposalAsync(connection, null, original, draft, key, ct));
        try
        {
            return await db.RunDbAsync(async connection =>
            {
                await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                try
                {
                    var fresh = await Employee.GetCurrentWithRolesAsync(connection, transaction, principal, ct);
                    EnsurePhoneActor(fresh, employeeId);
                    var rights = await connection.QuerySingleAsync<TerritorialEntityAccess>(
                        new CommandDefinition(TerritorialEntitySql.Access, transaction: transaction, cancellationToken: ct));
                    if (!PhoneGrant(rights, kind)) throw Denied();
                    await CheckPhoneProposalAsync(connection, transaction, original, draft, key, ct);
                    if (deleting)
                        await connection.ExecuteAsync(new CommandDefinition(TerritorialEntitySql.PhoneDelete, key,
                            transaction, cancellationToken: ct));
                    else
                        await connection.ExecuteAsync(new CommandDefinition(
                            kind == PhoneInsertKind ? TerritorialEntitySql.PhoneInsert : TerritorialEntitySql.PhoneUpdate,
                            new { key.Code, draft!.TerritoryId, Length = (byte)draft.Length, draft.TypeId, Comment = Ansi(draft.Comment, 100) },
                            transaction, cancellationToken: ct));
                    var saved = await ReadSingleAsync<PhoneCodeEntityRecord>(connection, transaction,
                        string.Format(TerritorialEntitySql.PhoneRead, ""), key, ct);
                    if (deleting ? saved is not null :
                        saved is null || saved with { ChangedBy = draft!.ChangedBy, ChangedByName = draft.ChangedByName, ChangedAt = draft.ChangedAt } != draft)
                        throw new TerritorialEntityRuleException(deleting
                            ? "База не подтвердила удаление телефонного кода."
                            : "База не подтвердила сохранение телефонного кода.");

                    await transaction.CommitAsync(CancellationToken.None);
                    return saved;
                }
                catch (Exception error) when (error is TerritorialEntityRuleException or UnauthorizedAccessException ||
                    error is SqlException sql && sql.Number != -2 && !DbManager.IsConnectivityError(sql) && transaction.Connection is not null)
                {
                    throw new PhoneWriteRejectedException(error);
                }
            });
        }
        catch (PhoneWriteRejectedException rejected)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(rejected.InnerException!).Throw();
            throw;
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

    private static void EnsurePhoneKey(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 20)
            throw new TerritorialEntityRuleException("Некорректный ключ телефонного кода.");
    }

    private sealed class PhoneWriteRejectedException(Exception error) : InvalidOperationException(error.Message, error);
}
