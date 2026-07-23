using Clayzor.Lib.DALC;

namespace Clayzor.Lib.Entities.DynamicGrid;

/// <summary>
/// Класс данных для чтения определения динамического грида и его колонок из БД.
/// Содержит чистые функции (BuildGridSql, BuildColumnsSql, MapDefinition, MapColumn)
/// для тестирования без БД, и методы с <see cref="DbManager"/> для реальных запросов.
/// </summary>
public static class ClayGridDefinitionData
{
    /// <summary>
    /// Строит SQL SELECT для чтения одной строки определения грида по @gridId.
    /// Имена колонок берутся из <paramref name="s"/> и оборачиваются в [].
    /// </summary>
    public static string BuildGridSql(string settingsTable, ClayGridSchemaMap s)
    {
        var sc = s.Settings;
        return $"SELECT [{sc.GridId}],[{sc.Title}],[{sc.Icon}],[{sc.Sql}],[{sc.Id}],[{sc.IdName}],[{sc.EditForm}],[{sc.NewForm}],[{sc.SqlDelete}] FROM [{settingsTable}] WHERE [{sc.GridId}] = @gridId";
    }

    /// <summary>
    /// Строит SQL SELECT для чтения колонок грида по @gridId с сортировкой по Порядок.
    /// </summary>
    public static string BuildColumnsSql(string columnsTable, ClayGridSchemaMap s)
    {
        var c = s.Columns;
        return $"SELECT [{c.ColumnId}],[{c.GridId}],[{c.Column}],[{c.Header}],[{c.UrlKey}],[{c.Order}],[{c.Format}],[{c.Type}] FROM [{columnsTable}] WHERE [{c.GridId}] = @gridId ORDER BY [{c.Order}], [{c.ColumnId}]";
    }

    /// <summary>
    /// Строит SQL SELECT для чтения колонок грида, включая опциональную колонку
    /// <see cref="ColumnCols.QuickSearch"/> (УчаствуетВБыстромПоиске tinyint).
    /// </summary>
    public static string BuildColumnsSqlWithQuickSearch(string columnsTable, ClayGridSchemaMap s)
    {
        var c = s.Columns;
        return $"SELECT [{c.ColumnId}],[{c.GridId}],[{c.Column}],[{c.Header}],[{c.UrlKey}],[{c.Order}],[{c.Format}],[{c.Type}],[{c.QuickSearch}] FROM [{columnsTable}] WHERE [{c.GridId}] = @gridId ORDER BY [{c.Order}], [{c.ColumnId}]";
    }

    /// <summary>
    /// Проверяет наличие опциональной колонки быстрого поиска в таблице определений колонок.
    /// Использует COL_LENGTH (совместимо с SQL Server 2008 R2) — без try/catch.
    /// </summary>
    public static async Task<bool> CheckQuickSearchSupportAsync(
        DbManager db, string columnsTable, ClayGridSchemaMap s, CancellationToken ct = default)
    {
        var sql = "SELECT CASE WHEN COL_LENGTH(@table, @column) IS NULL THEN 0 ELSE 1 END";
        var rows = await DynamicSql.QueryRowsAsync(db, sql, new
        {
            table = columnsTable,
            column = s.Columns.QuickSearch
        }, ct);
        return rows.Count > 0 && Convert.ToInt32(rows[0].Values.First()) == 1;
    }

    /// <summary>
    /// Загружает определение грида и проверяет поддержку быстрого поиска.
    /// </summary>
    public static async Task<ClayGridDefinition?> LoadGridWithQuickSearchAsync(
        DbManager db, int gridId, string settingsTable, string columnsTable,
        ClayGridSchemaMap schema, CancellationToken ct = default)
    {
        var def = await LoadGridAsync(db, gridId, settingsTable, schema, ct);
        if (def == null) return null;

        var supportsQS = await CheckQuickSearchSupportAsync(db, columnsTable, schema, ct);
        return def with { SupportsQuickSearch = supportsQS };
    }

    /// <summary>
    /// Маппит строку-словарь (результат <see cref="DynamicSql.QueryRowsAsync"/>) в <see cref="ClayGridDefinition"/>.
    /// Ключи словаря — имена колонок из <paramref name="s"/>.
    /// </summary>
    public static ClayGridDefinition MapDefinition(IReadOnlyDictionary<string, object?> row, ClayGridSchemaMap s)
    {
        var sc = s.Settings;
        return new ClayGridDefinition(
            GridId:        GetInt32(row, sc.GridId),
            Title:         GetStringOrNull(row, sc.Title),
            IconUrl:       GetStringOrNull(row, sc.Icon),
            Sql:           GetString(row, sc.Sql),
            IdColumn:      GetStringOrNull(row, sc.Id),
            IdNameColumn:  GetStringOrNull(row, sc.IdName),
            EditForm:      GetStringOrNull(row, sc.EditForm),
            NewForm:       GetStringOrNull(row, sc.NewForm),
            SqlDelete:     GetStringOrNull(row, sc.SqlDelete));
    }

    /// <summary>
    /// Маппит строку-словарь в <see cref="ClayColumnDefinition"/>.
    /// Порядок может быть NULL или 0 — не отбрасывается (видимость решается в G4).
    /// Если <paramref name="supportsQuickSearch"/> — читает флаг из колонки
    /// <see cref="ColumnCols.QuickSearch"/> (1→true, всё остальное→false).
    /// </summary>
    public static ClayColumnDefinition MapColumn(
        IReadOnlyDictionary<string, object?> row, ClayGridSchemaMap s,
        bool supportsQuickSearch = false)
    {
        var c = s.Columns;
        var quickSearch = supportsQuickSearch && GetQuickSearchFlag(row, c.QuickSearch);
        return new ClayColumnDefinition(
            ColumnId:    GetInt32(row, c.ColumnId),
            GridId:      GetInt32(row, c.GridId),
            Column:      GetString(row, c.Column),
            Header:      GetStringOrNull(row, c.Header),
            UrlKey:      GetStringOrNull(row, c.UrlKey),
            Order:       GetInt32OrNull(row, c.Order),
            Format:      GetStringOrNull(row, c.Format),
            Type:        GetInt32OrDefault(row, c.Type),
            QuickSearch: quickSearch);
    }

    /// <summary>
    /// Загружает определение грида из БД. Возвращает null, если грид не найден.
    /// </summary>
    public static async Task<ClayGridDefinition?> LoadGridAsync(
        DbManager db, int gridId, string settingsTable,
        ClayGridSchemaMap schema, CancellationToken ct = default)
    {
        var sql  = BuildGridSql(settingsTable, schema);
        var rows = await DynamicSql.QueryRowsAsync(db, sql, new { gridId }, ct);
        return rows.Count == 0 ? null : MapDefinition(rows[0], schema);
    }

    /// <summary>
    /// Загружает все колонки грида из БД. Если <paramref name="supportsQuickSearch"/>,
    /// использует версию SQL с колонкой <see cref="ColumnCols.QuickSearch"/>.
    /// </summary>
    public static async Task<IReadOnlyList<ClayColumnDefinition>> LoadColumnsAsync(
        DbManager db, int gridId, string columnsTable,
        ClayGridSchemaMap schema, bool supportsQuickSearch = false,
        CancellationToken ct = default)
    {
        var sql  = supportsQuickSearch
            ? BuildColumnsSqlWithQuickSearch(columnsTable, schema)
            : BuildColumnsSql(columnsTable, schema);
        var rows = await DynamicSql.QueryRowsAsync(db, sql, new { gridId }, ct);
        return rows.Select(r => MapColumn(r, schema, supportsQuickSearch)).ToList();
    }

    private static int GetInt32(IReadOnlyDictionary<string, object?> row, string key)
    {
        var val = row[key];
        return val is int i ? i : Convert.ToInt32(val);
    }

    private static int? GetInt32OrNull(IReadOnlyDictionary<string, object?> row, string key)
    {
        var val = row.GetValueOrDefault(key);
        if (val is null or DBNull)
            return null;
        return val is int i ? i : Convert.ToInt32(val);
    }

    private static int GetInt32OrDefault(IReadOnlyDictionary<string, object?> row, string key)
    {
        var val = row.GetValueOrDefault(key);
        if (val is null or DBNull)
            return 0;
        return val is int i ? i : Convert.ToInt32(val);
    }

    private static string GetString(IReadOnlyDictionary<string, object?> row, string key)
    {
        return row[key]?.ToString() ?? string.Empty;
    }

    private static string? GetStringOrNull(IReadOnlyDictionary<string, object?> row, string key)
    {
        var val = row.GetValueOrDefault(key);
        return val is null or DBNull ? null : val.ToString();
    }

    /// <summary>
    /// Преобразует значение tinyint в bool для флага быстрого поиска.
    /// Правила: 1 → true; 0, NULL, любое другое число → false.
    /// </summary>
    private static bool GetQuickSearchFlag(IReadOnlyDictionary<string, object?> row, string key)
    {
        var val = row.GetValueOrDefault(key);
        if (val is null or DBNull)
            return false;
        return val is int i && i == 1;
    }
}
