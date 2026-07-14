using Dapper;
using Clayzor.Lib.DALC;

namespace Clayzor.Lib.Entities.DynamicGrid;

/// <summary>
/// Класс данных для чтения и сохранения пользовательских параметров динамического грида.
/// Сохранение — ТОЛЬКО INSERT; upsert делает триггер БД (см. G0).
/// </summary>
public static class ClayGridUserParamsData
{
    /// <summary>
    /// Строит имя параметра: префикс + gridId. ЧИСТАЯ функция.
    /// </summary>
    public static string BuildParamName(string prefix, int gridId) => prefix + gridId;

    /// <summary>
    /// Строит SQL SELECT для чтения параметров пользователя.
    /// WHERE [ClientId] = @clid AND [Name] IN (@n0, @n1, …).
    /// </summary>
    public static string BuildLoadSql(string table, ClayGridSchemaMap s, int nameCount)
    {
        var sc = s.UserParams;
        var inParams = string.Join(", ", Enumerable.Range(0, nameCount).Select(i => $"@n{i}"));
        return $"SELECT [{sc.Name}],[{sc.Value}] FROM [{table}] WHERE [{sc.ClientId}] = @clid AND [{sc.Name}] IN ({inParams})";
    }

    /// <summary>
    /// Строит SQL INSERT для сохранения параметра пользователя.
    /// НИКАКОГО UPDATE/MERGE — upsert делает триггер БД.
    /// </summary>
    public static string BuildInsertSql(string table, ClayGridSchemaMap s)
    {
        var sc = s.UserParams;
        return $"INSERT INTO [{table}] ([{sc.ClientId}],[{sc.Name}],[{sc.Value}]) VALUES (@clid,@name,@value)";
    }

    /// <summary>
    /// Читает параметры пользователя по точным именам.
    /// Возвращает словарь (Параметр → Значение). Отсутствующие параметры → "".
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string>> LoadAsync(
        DbManager db, int clientId, IReadOnlyList<string> paramNames,
        string table, ClayGridSchemaMap s, CancellationToken ct = default)
    {
        if (paramNames.Count == 0)
            return new Dictionary<string, string>();

        var sql = BuildLoadSql(table, s, paramNames.Count);
        var dp  = new DynamicParameters();
        dp.Add("clid", clientId);
        for (int i = 0; i < paramNames.Count; i++)
            dp.Add($"n{i}", paramNames[i]);

        var rows = await DynamicSql.QueryRowsAsync(db, sql, dp, ct);
        var sc   = s.UserParams;
        var result = new Dictionary<string, string>();
        foreach (var row in rows)
        {
            var name  = row[sc.Name]?.ToString() ?? "";
            var value = row[sc.Value]?.ToString() ?? "";
            result[name] = value;
        }
        return result;
    }

    /// <summary>
    /// Сохраняет ОДИН параметр пользователя через INSERT.
    /// При повторном вызове с тем же clientId+name триггер БД делает UPDATE.
    /// </summary>
    public static Task SaveAsync(
        DbManager db, int clientId, string name, string value,
        string table, ClayGridSchemaMap s, CancellationToken ct = default)
    {
        var sql = BuildInsertSql(table, s);
        return DynamicSql.ExecuteAsync(db, sql, new { clid = clientId, name, value }, ct);
    }
}
