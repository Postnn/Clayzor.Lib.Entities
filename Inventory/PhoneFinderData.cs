using System.Globalization;
using System.Text;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Пять независимых аргументов функции допуска телефонных номеров.</summary>
public sealed record FinderTelContext(int? LocationId, int? EmployeeId, int? EquipmentTypeId, int? PhoneIdBase, int? EquipmentId);

/// <summary>Пять независимых аргументов функции подбора оборудования для телефонного номера.</summary>
public sealed record FinderEquipmentContext(int? PhoneTypeId, int? StationId, int? SocketId, int? EmployeeId, int? PhoneId);

/// <summary>Полная строка функции выбора оборудования.</summary>
public sealed class FinderEquipmentRow
{
    public int EquipmentId { get; init; }
    public int? ConnectionEquipmentId { get; init; }
    public string? SerialNumber { get; init; }
    public string? Model { get; init; }
    public string? LocationPath { get; init; }
    public PhoneEquipmentKey Key => new(EquipmentId, ConnectionEquipmentId);
}

/// <summary>Полная строка функции выбора телефонного номера.</summary>
public sealed class FinderTelRow
{
    public int Id { get; init; }
    public string? ShortNumber { get; init; }
    public string? FullNumber { get; init; }
    public int? PhoneTypeId { get; init; }
    public string? Subscriber { get; init; }
    public int? ConnectionEquipmentId { get; init; }
}

/// <summary>Название и иконка типа номера.</summary>
public sealed record FinderTelType(int Id, string Name, string? Icon);

/// <summary>Одна страница и полное число строк после фильтров.</summary>
public sealed record PhoneFinderPage<TRow>(IReadOnlyList<TRow> Rows, int Total);

/// <summary>Строгие чтения для двух окон выбора и авторитетное восстановление результатов.</summary>
public static class PhoneFinderData
{
    /// <summary>Общий запрос FinderTel с уже выбранными номерами, исключёнными параметрами.</summary>
    public static (string Sql, DynamicParameters Parameters) BuildTelQuery(FinderTelContext context, IEnumerable<int> excludedIds)
    {
        var parameters = new DynamicParameters(context);
        var sql = new StringBuilder(SQLQueries.SELECT_FinderTel);
        PhoneNumberQuery.AddExcludedIds(sql, parameters, excludedIds);
        return (sql.ToString(), parameters);
    }

    /// <summary>Читает только номер, который всё ещё доступен в данном контексте.</summary>
    public static async Task<PhoneNumberLookup?> ResolveTelAsync(DbManager db, FinderTelContext context, IEnumerable<int> excludedIds,
        int id, CancellationToken ct)
    {
        if (id <= 0 || excludedIds.Contains(id)) return null;
        var parameters = new DynamicParameters(context);
        parameters.Add("SelectedPhoneId", id);
        parameters.Add("Search", "");
        parameters.Add("LimitPlusOne", 1);
        var sql = new StringBuilder(SQLQueries.SELECT_ТелефонныеНомераДляВыбора);
        PhoneNumberQuery.AddSelectedId(sql);
        return (await db.QueryRequiredAsync<PhoneNumberLookup>(sql.ToString(), parameters, ct)).SingleOrDefault();
    }

    /// <summary>Возвращает текущий справочник типов номеров.</summary>
    public static Task<IEnumerable<FinderTelType>> ReadTypesAsync(DbManager db, CancellationToken ct) =>
        db.QueryRequiredAsync<FinderTelType>(SQLQueries.SELECT_FinderTelTypes, cancellationToken: ct);

    /// <summary>Общий запрос Finder с исключением по коду самого аппарата.</summary>
    public static (string Sql, DynamicParameters Parameters) BuildEquipmentQuery(FinderEquipmentContext context, IEnumerable<int> excludedIds)
    {
        var parameters = new DynamicParameters(context);
        var sql = new StringBuilder(SQLQueries.SELECT_FinderEquipment);
        ExcludeEquipment(sql, parameters, excludedIds);
        return (sql.ToString(), parameters);
    }

    /// <summary>Применяет одни исключения ко всем ветвям компактного и расширенного поиска.</summary>
    public static void ExcludeEquipment(StringBuilder sql, DynamicParameters parameters, IEnumerable<int> excludedIds)
    {
        var ids = excludedIds.Distinct().ToArray();
        if (ids.Length == 0) return;
        var predicates = new List<string>(ids.Length);
        for (var index = 0; index < ids.Length; index++)
        {
            var name = $"@ExcludedEquipment{index}";
            parameters.Add(name, ids[index]);
            predicates.Add($"AND F.КодОборудования <> {name}");
        }
        PhoneNumberQuery.AddPredicate(sql, string.Join(Environment.NewLine, predicates));
    }

    /// <summary>Восстанавливает точную пару аппарата и подключения, если она по-прежнему допустима.</summary>
    public static async Task<PhoneEquipmentLookup?> ResolveEquipmentAsync(DbManager db, FinderEquipmentContext context,
        IEnumerable<int> excludedIds, PhoneEquipmentKey key, CancellationToken ct)
    {
        if (key.EquipmentId <= 0 || excludedIds.Contains(key.EquipmentId)) return null;
        var parameters = new DynamicParameters(context);
        parameters.Add("Search", "");
        parameters.Add("LimitPlusOne", 1);
        parameters.Add("SelectedEquipmentId", key.EquipmentId);
        parameters.Add("SelectedConnectionId", key.ConnectionEquipmentId);
        var sql = new StringBuilder(SQLQueries.SELECT_ТелефонОборудованиеДляВыбора);
        PhoneNumberQuery.AddPredicate(sql, "AND F.КодОборудования=@SelectedEquipmentId AND ISNULL(F.КодОборудованияПодключения,0)=ISNULL(@SelectedConnectionId,0)");
        return (await db.QueryRequiredAsync<PhoneEquipmentLookup>(sql.ToString(), parameters, ct)).SingleOrDefault();
    }

    /// <summary>Читает страницу из доверенного запроса с параметрами составного фильтра и контекста.</summary>
    public static Task<PhoneFinderPage<TRow>> ReadPageAsync<TRow>(DbManager db, string source, string? where, string order,
        DynamicParameters parameters, int page, int size, CancellationToken ct)
    {
        if (page < 1 || size is < 1 or > 999) throw new ArgumentOutOfRangeException(nameof(page));
        var filtered = Filter(source, where);
        var sql = string.Format(CultureInfo.InvariantCulture, SQLQueries.SELECT_FinderPage, filtered, order);
        parameters.Add("FirstRow", (page - 1L) * size + 1);
        parameters.Add("LastRow", (long)page * size);
        return db.RunDbAsync(async connection =>
        {
            using var result = await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
            var total = await result.ReadSingleAsync<int>();
            var rows = (await result.ReadAsync<TRow>()).AsList();
            return new PhoneFinderPage<TRow>(rows, total);
        });
    }

    /// <summary>Дозированная выборка значений колонки, уже проверенной по схеме.</summary>
    public static Task<IReadOnlyList<object?>> ReadDistinctAsync(DbManager db, string source, string? where,
        string column, DynamicParameters parameters, int limit, CancellationToken ct)
    {
        var sql = string.Format(CultureInfo.InvariantCulture, SQLQueries.SELECT_FinderDistinct, Filter(source, where), $"[{column}]");
        parameters.Add("Limit", limit);
        return db.RunDbAsync<IReadOnlyList<object?>>(async connection =>
        {
            await using var reader = await connection.ExecuteReaderAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
            var values = new List<object?>();
            while (await reader.ReadAsync(ct)) values.Add(reader.IsDBNull(0) ? null : reader.GetValue(0));
            return values;
        });
    }

    private static string Filter(string source, string? where) =>
        string.IsNullOrEmpty(where) ? source : $"SELECT * FROM ({source}) candidates WHERE {where}";
}
