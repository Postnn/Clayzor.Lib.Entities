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
        PhonePoolAttributeKey key, PhonePoolAttributeValue? original, string value, CancellationToken ct)
    {
        if (PhonePoolAttributeKey.ValidateValue(value) is { } error)
            throw new PhonePoolAttributeRejectedException(error);
        return WriteAsync(db, principal, expectedEmployeeId, key, original, value, delete: false, ct);
    }

    /// <summary>Удаляет только существующую пару с совпадающим исходным снимком.</summary>
    public static Task<PhonePoolAttributeCard> DeleteAsync(DbManager db, string? principal, int expectedEmployeeId,
        PhonePoolAttributeKey key, PhonePoolAttributeValue original, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(original);
        return WriteAsync(db, principal, expectedEmployeeId, key, original, null, delete: true, ct);
    }

    private static async Task<PhonePoolAttributeCard> WriteAsync(DbManager db, string? principal, int expectedEmployeeId,
        PhonePoolAttributeKey key, PhonePoolAttributeValue? original, string? value, bool delete, CancellationToken ct)
    {
        Employee actor;
        try
        {
            if (!key.IsValid) throw new PhonePoolAttributeRejectedException("Некорректный ключ атрибута.");
            actor = await Employee.GetCurrentWithRolesAsync(db, principal, ct) is { } current && current.Id == expectedEmployeeId
                ? current : throw new PhonePoolAttributeRejectedException("Не удалось подтвердить текущего сотрудника.");
        }
        catch (Exception e) when (e is not PhonePoolAttributeRejectedException)
        {
            throw new PhonePoolAttributeRejectedException("Проверка сотрудника не завершилась. Запись не начата.", e);
        }

        return await db.RunDbAsync(async connection =>
        {
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var committing = false;
            try
            {
                var rights = await ReadRightsAsync(connection, transaction, actor, ct);
                if (!rights.CanRead || !(delete ? rights.CanDelete : original is null ? rights.CanInsert : rights.CanUpdate))
                    throw new PhonePoolAttributeRejectedException("Нет разрешения на изменение атрибута.");

                var card = await ReadCardAsync(connection, transaction, key, ct);
                if (!MatchesOriginal(card.Attribute, original))
                    throw new PhonePoolAttributeRejectedException(card.Attribute is null
                        ? "Запись уже удалена. Перечитайте карточку."
                        : "Значение уже создано или изменено. Перечитайте карточку.");

                var sql = delete ? SQLQueries.DELETE_АтрибутПула
                    : original is null ? SQLQueries.INSERT_АтрибутПула : SQLQueries.UPDATE_АтрибутПула;
                var affected = await connection.QuerySingleAsync<int>(Command(sql,
                    new { poolId = card.Key.PoolId, type = card.Key.Type, value }, transaction, ct));
                if (affected != 1)
                    throw new PhonePoolAttributeRejectedException("База не подтвердила изменение одной записи атрибута.");
                var saved = await ReadCardAsync(connection, transaction, card.Key, ct);
                if (delete ? saved.Attribute is not null : saved.Attribute is null || saved.Attribute.Value != value)
                    throw new PhonePoolAttributeRejectedException("База не подтвердила результат операции с атрибутом.");
                committing = true;
                await transaction.CommitAsync(CancellationToken.None);
                return saved;
            }
            catch (Exception e)
            {
                if (committing || e is SqlException sql && (sql.Number == -2 || DbManager.IsConnectivityError(sql)))
                    throw new PhonePoolAttributeUnknownException(e);
                try
                {
                    if (transaction.Connection is not null) await transaction.RollbackAsync(CancellationToken.None);
                }
                catch (Exception rollback)
                {
                    throw new PhonePoolAttributeUnknownException(rollback);
                }
                if (e is PhonePoolAttributeRejectedException) throw;
                throw new PhonePoolAttributeRejectedException("База отклонила операцию с атрибутом.", e);
            }
        });
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

