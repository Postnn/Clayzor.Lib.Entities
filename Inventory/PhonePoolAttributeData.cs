using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using Clayzor.Lib.DALC;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Ключ значения атрибута. Тип хранится в подтверждённой кодировке varchar(50).</summary>
public sealed record PhonePoolAttributeKey(int PoolId, string Type)
{
    private const char UnitSeparator = '\u001f';
    private static readonly Encoding SqlEncoding = CodePagesEncodingProvider.Instance.GetEncoding(
        1251, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)!;

    /// <summary>Разбирает значение, уже декодированное ASP.NET как параметр query.</summary>
    public static PhonePoolAttributeKey? Parse(string? decodedId)
    {
        if (string.IsNullOrEmpty(decodedId)) return null;
        var separator = decodedId.Contains(UnitSeparator) ? UnitSeparator : ':';
        var parts = decodedId.Split(separator);
        if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var poolId))
            return null;
        var result = new PhonePoolAttributeKey(poolId, parts[1]);
        return result.IsValid ? result : null;
    }

    /// <summary>Проверяет форму и кодировку обеих частей ключа.</summary>
    public bool IsValid => PoolId > 0 && !string.IsNullOrWhiteSpace(Type) &&
        !Type.Any(char.IsControl) && FitsSql(Type, 50);

    /// <summary>Значение параметра id с сохранением типов, содержащих двоеточие.</summary>
    public string RouteId => PoolId.ToString(CultureInfo.InvariantCulture) + (Type.Contains(':') ? UnitSeparator : ':') + Type;

    /// <summary>Проверяет точное сохранение значения в SQL без замены символов и обрезки.</summary>
    public static string? ValidateValue(string? value) => value is null || value.Contains('\0') || !FitsSql(value, 150)
        ? "Значение должно содержать не более 150 символов, поддерживаемых кодировкой поля." : null;

    private static bool FitsSql(string value, int max)
    {
        if (value.Length > max) return false;
        try { return SqlEncoding.GetByteCount(value) <= max; }
        catch (EncoderFallbackException) { return false; }
    }
}

/// <summary>Значение существующей пары и его версия по аудиту.</summary>
public sealed record PhonePoolAttributeValue(string Value, int ChangedBy, DateTime ChangedAt, string? ChangedByName);

/// <summary>Разрешения текущего сотрудника на операции представления.</summary>
public sealed record PhonePoolAttributeRights(bool CanRead, bool CanInsert, bool CanUpdate, bool CanDelete);

/// <summary>Данные родителей и необязательное значение пары.</summary>
public sealed record PhonePoolAttributeCard(PhonePoolAttributeKey Key, string PoolName, string Description, PhonePoolAttributeValue? Attribute);

/// <summary>Подтверждённый отказ сохранения или чтения родительских данных.</summary>
public sealed class PhonePoolAttributeRejectedException(string message, Exception? inner = null) : InvalidOperationException(message, inner);

/// <summary>Исход операции нельзя подтвердить без сверки.</summary>
public sealed class PhonePoolAttributeUnknownException(Exception inner) : InvalidOperationException("Исход операции требует сверки.", inner);

/// <summary>Чтение и атомарные операции значения атрибута существующего пула.</summary>
public static class PhonePoolAttributeData
{
    private const string SaveKind = "phone-pool-attribute.save";
    private const string DeleteKind = "phone-pool-attribute.delete";

    /// <summary>Читает карточку и разрешения при явном доступе к представлению.</summary>
    public static Task<(PhonePoolAttributeCard Card, PhonePoolAttributeRights Rights)> ReadAsync(
        DbManager db, Employee actor, PhonePoolAttributeKey key, CancellationToken ct) =>
        db.RunDbAsync(async connection =>
        {
            var rights = await ReadRightsAsync(connection, null, actor, ct);
            if (!rights.CanRead)
                throw new UnauthorizedAccessException("Нет доступа к атрибутам телефонных пулов.");
            return (await ReadCardAsync(connection, null, key, ct), rights);
        });

    /// <summary>Сохраняет новую или существующую пару по исходной версии и возвращает проверенный снимок.</summary>
    public static Task<PhonePoolAttributeCard> SaveAsync(DbManager db, string? principal, int expectedEmployeeId,
        PhonePoolAttributeKey key, PhonePoolAttributeValue? original, string value, CancellationToken ct,
        Guid? attemptId = null)
    {
        if (PhonePoolAttributeKey.ValidateValue(value) is { } error)
            throw new PhonePoolAttributeRejectedException(error);
        return WriteAsync(db, principal, expectedEmployeeId, key, original, value, delete: false, ct, attemptId ?? Guid.NewGuid());
    }

    /// <summary>Удаляет только существующую пару с совпадающим исходным снимком.</summary>
    public static Task<PhonePoolAttributeCard> DeleteAsync(DbManager db, string? principal, int expectedEmployeeId,
        PhonePoolAttributeKey key, PhonePoolAttributeValue original, CancellationToken ct, Guid? attemptId = null)
    {
        ArgumentNullException.ThrowIfNull(original);
        return WriteAsync(db, principal, expectedEmployeeId, key, original, null, delete: true, ct, attemptId ?? Guid.NewGuid());
    }

    /// <summary>Читает карточку и состояние открытого слота только после проверки доступа к паре.</summary>
    public static async Task<(PhonePoolAttributeCard Card, PhonePoolAttributeRights Rights, bool WritePending)> ReadEditorAsync(
        DbManager db, Employee actor, PhonePoolAttributeKey key,
        CancellationToken ct)
    {
        var (card, rights) = await ReadAsync(db, actor, key, ct);
        var scope = ConflictKey(card.Key);
        var saves = await InventoryOperationJournal.ReadOpenScopesAsync(db, SaveKind, ct);
        if (saves.Contains(scope, StringComparer.Ordinal)) return (card, rights, true);
        var deletes = await InventoryOperationJournal.ReadOpenScopesAsync(db, DeleteKind, ct);
        return (card, rights, deletes.Contains(scope, StringComparer.Ordinal));
    }

    /// <summary>Импортирует неподтверждённые попытки владельца после свежей проверки доступа к канонической паре.</summary>
    public static async Task ImportLegacyOperationsAsync(DbManager db, string? principal, int expectedEmployeeId,
        PhonePoolAttributeKey key, IReadOnlyCollection<(Guid AttemptId, string Kind)> evidence, CancellationToken ct)
    {
        if (evidence.Count == 0) return;
        var actor = RequireActor(await Employee.GetCurrentWithRolesAsync(db, principal, ct), expectedEmployeeId);
        var (card, rights) = await ReadAsync(db, actor, key, ct);
        if (card.Key.RouteId != key.RouteId || !(rights.CanInsert || rights.CanUpdate || rights.CanDelete))
            throw new PhonePoolAttributeRejectedException("Не удалось подтвердить доступ к журналу этой пары.");

        var attempts = new List<InventoryOperationJournal.Attempt>(evidence.Count);
        foreach (var (attemptId, kind) in evidence.Distinct().OrderBy(item => item.AttemptId))
        {
            if (attemptId == Guid.Empty)
                throw new PhonePoolAttributeRejectedException("Прежняя попытка изменения атрибута требует сверки.");
            var commandKind = kind switch
            {
                "phone-pool-attribute.insert" or "phone-pool-attribute.update" => SaveKind,
                "phone-pool-attribute.delete" => DeleteKind,
                _ => throw new PhonePoolAttributeRejectedException("Прежняя попытка изменения атрибута требует сверки."),
            };
            attempts.Add(InventoryOperationJournal.ForEmployee(actor, attemptId, ConflictKey(card.Key), commandKind));
        }

        await InventoryOperationJournal.ImportLegacyAsync(db, attempts, ct);
    }


    /// <summary>Сверяет точную попытку сотрудника по доступной канонической паре и виду команды.</summary>
    public static async Task<InventoryOperationReceipt?> ReadWriteReceiptAsync(DbManager db, string? principal,
        int expectedEmployeeId, PhonePoolAttributeKey key, Guid attemptId, bool delete, CancellationToken ct)
    {
        var actor = await Employee.GetCurrentWithRolesAsync(db, principal, ct);
        if (actor is null || actor.Id != expectedEmployeeId)
            return null;
        var (card, _) = await ReadAsync(db, actor, key, ct);
        var attempt = InventoryOperationJournal.ForEmployee(actor, attemptId, ConflictKey(card.Key), delete ? DeleteKind : SaveKind);
        try
        {
            var receipt = await InventoryOperationJournal.ReadAttemptAsync(db, attempt, ct);
            return receipt is { State: InventoryOperationState.Confirmed } && receipt.ResultEntityId != card.Key.PoolId
                ? null : receipt;
        }
        catch (InventoryOperationPendingException)
        {
            return null;
        }
    }

    private static async Task<PhonePoolAttributeCard> WriteAsync(DbManager db, string? principal, int expectedEmployeeId,
        PhonePoolAttributeKey key, PhonePoolAttributeValue? original, string? value, bool delete, CancellationToken ct, Guid attemptId)
    {
        Employee actor;
        PhonePoolAttributeCard opened;
        InventoryOperationJournal.Attempt attempt;
        try
        {
            if (!key.IsValid) throw new PhonePoolAttributeRejectedException("Некорректный ключ атрибута.");
            actor = RequireActor(await Employee.GetCurrentWithRolesAsync(db, principal, ct), expectedEmployeeId);
            opened = await db.RunDbAsync(connection => ReadForWriteAsync(connection, null, actor, key, original, delete, ct));
            attempt = InventoryOperationJournal.ForEmployee(actor, attemptId, ConflictKey(opened.Key), delete ? DeleteKind : SaveKind);
            var previous = await InventoryOperationJournal.ReadAttemptAsync(db, attempt, ct);
            if (previous is { State: InventoryOperationState.Confirmed }) return opened;
            if (previous is { State: InventoryOperationState.Rejected })
                throw new PhonePoolAttributeRejectedException("Запись ранее отклонена. Перечитайте карточку.");
            if (previous is not null) throw new InventoryOperationPendingException();
            EnsureOriginal(opened, original);
        }
        catch (Exception e) when (e is not PhonePoolAttributeRejectedException and not InventoryOperationPendingException)
        {
            throw new PhonePoolAttributeRejectedException("Проверка сотрудника не завершилась. Запись не начата.", e);
        }

        PhonePoolAttributeCard? saved = null;
        try
        {
            var receipt = await InventoryOperationJournal.RunSqlAsync<PhonePoolAttributeRejectedException>(
                db, attempt, WriteValueAsync, ct);
            if (receipt.State == InventoryOperationState.Rejected)
                throw new PhonePoolAttributeRejectedException("Запись ранее отклонена. Перечитайте карточку.");
            return saved ?? opened;
        }
        catch (Exception e) when (e is not PhonePoolAttributeRejectedException and not InventoryOperationPendingException)
        {
            throw new PhonePoolAttributeUnknownException(e);
        }

        async Task<int?> WriteValueAsync(DbConnection connection, DbTransaction transaction, CancellationToken token)
        {
            try
            {
                var current = RequireActor(await Employee.GetCurrentWithRolesAsync(connection, transaction, principal, token),
                    expectedEmployeeId);
                var card = await ReadForWriteAsync(connection, transaction, current, key, original, delete, token);
                if (card.Key != opened.Key)
                    throw new PhonePoolAttributeRejectedException("Ключ атрибута изменился. Перечитайте карточку.");
                EnsureOriginal(card, original);
                var sql = delete ? SQLQueries.DELETE_АтрибутПула
                    : original is null ? SQLQueries.INSERT_АтрибутПула : SQLQueries.UPDATE_АтрибутПула;
                var affected = await connection.QuerySingleAsync<int>(Command(sql,
                    new { poolId = card.Key.PoolId, type = card.Key.Type, value }, transaction, token));
                if (affected != 1)
                    throw new PhonePoolAttributeRejectedException("База не подтвердила изменение одной записи атрибута.");
                saved = await ReadCardAsync(connection, transaction, card.Key, token);
                if (delete ? saved.Attribute is not null : saved.Attribute is null || saved.Attribute.Value != value)
                    throw new PhonePoolAttributeRejectedException("База не подтвердила результат операции с атрибутом.");
                return card.Key.PoolId;
            }
            catch (SqlException e) when (e.Number != -2 && !DbManager.IsConnectivityError(e))
            {
                throw new PhonePoolAttributeRejectedException("База отклонила операцию с атрибутом.", e);
            }
        }
    }

    private static string ConflictKey(PhonePoolAttributeKey key) =>
        "phone-pool-attribute/" + key.PoolId.ToString(CultureInfo.InvariantCulture) + "/" + key.Type;

    private static Employee RequireActor(Employee? actor, int expectedEmployeeId) =>
        actor is not null && actor.Id == expectedEmployeeId ? actor
            : throw new PhonePoolAttributeRejectedException("Не удалось подтвердить текущего сотрудника.");

    private static async Task<PhonePoolAttributeCard> ReadForWriteAsync(DbConnection connection, DbTransaction? transaction,
        Employee actor, PhonePoolAttributeKey key, PhonePoolAttributeValue? original, bool delete, CancellationToken ct)
    {
        var rights = await ReadRightsAsync(connection, transaction, actor, ct);
        if (!rights.CanRead || !(delete ? rights.CanDelete : original is null ? rights.CanInsert : rights.CanUpdate))
            throw new PhonePoolAttributeRejectedException("Нет разрешения на изменение атрибута.");
        return await ReadCardAsync(connection, transaction, key, ct);
    }

    private static void EnsureOriginal(PhonePoolAttributeCard card, PhonePoolAttributeValue? original)
    {
        if (!MatchesOriginal(card.Attribute, original))
            throw new PhonePoolAttributeRejectedException(card.Attribute is null
                ? "Запись уже удалена. Перечитайте карточку."
                : "Значение уже создано или изменено. Перечитайте карточку.");
    }

    // Имя автора является подписью справочника и может измениться независимо от пары.
    private static bool MatchesOriginal(PhonePoolAttributeValue? value, PhonePoolAttributeValue? original) =>
        value is null || original is null ? value is null && original is null
            : value.Value == original.Value && value.ChangedBy == original.ChangedBy && value.ChangedAt == original.ChangedAt;

    private static Task<PhonePoolAttributeRights> ReadRightsAsync(DbConnection connection, DbTransaction? transaction,
        Employee actor, CancellationToken ct) => actor.HasRole(Роли.Дежурный_инженер)
        ? connection.QuerySingleAsync<PhonePoolAttributeRights>(Command(SQLQueries.SELECT_АтрибутПулаПрава, null, transaction, ct))
        : Task.FromResult(new PhonePoolAttributeRights(false, false, false, false));

    private static async Task<PhonePoolAttributeCard> ReadCardAsync(DbConnection connection, DbTransaction? transaction,
        PhonePoolAttributeKey key, CancellationToken ct)
    {
        if (!key.IsValid) throw new PhonePoolAttributeRejectedException("Некорректный ключ атрибута.");
        using var results = await connection.QueryMultipleAsync(Command(string.Format(CultureInfo.InvariantCulture,
            SQLQueries.SELECT_АтрибутПулаКарточка, transaction is null ? "" : "WITH (UPDLOCK, HOLDLOCK)"),
            new { poolId = key.PoolId, type = key.Type }, transaction, ct));
        var pool = await results.ReadSingleOrDefaultAsync<string>()
            ?? throw new PhonePoolAttributeRejectedException("Пул телефона не найден или недоступен.");
        var type = await results.ReadSingleOrDefaultAsync<AttributeType>()
            ?? throw new PhonePoolAttributeRejectedException("Тип атрибута не найден или недоступен.");
        var attribute = await results.ReadSingleOrDefaultAsync<PhonePoolAttributeValue>();
        return new PhonePoolAttributeCard(new PhonePoolAttributeKey(key.PoolId, type.Type), pool, type.Description, attribute);
    }

    private sealed record AttributeType(string Type, string Description);

    private static CommandDefinition Command(string sql, object? args, DbTransaction? transaction, CancellationToken ct) =>
        new(sql, args, transaction, commandType: CommandType.Text, cancellationToken: ct);
}

