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
    /// Бросает <see cref="InvalidOperationException"/>, если результат длиннее 20 символов
    /// (ограничение колонки <c>Параметр varchar(20)</c> — ошибка программиста, не пользователя).
    /// </summary>
    public static string BuildParamName(string prefix, int gridId)
    {
        var name = prefix + gridId;
        if (name.Length > 20)
            throw new InvalidOperationException(
                $"Имя параметра \"{name}\" длиннее 20 символов (varchar(20)). " +
                $"Уменьшите префикс \"{prefix}\" или идентификатор запроса.");
        return name;
    }

    /// <summary>
    /// Строит SQL SELECT для чтения параметров пользователя.
    /// WHERE [ClientId] = @clid AND [SharedId] = @shid AND [Name] IN (@n0, @n1, …).
    /// </summary>
    public static string BuildLoadSql(string table, ClayGridSchemaMap s, int nameCount, int sharedId = 0)
    {
        var sc = s.UserParams;
        var inParams = string.Join(", ", Enumerable.Range(0, nameCount).Select(i => $"@n{i}"));
        return $"SELECT [{sc.Name}],[{sc.Value}] FROM [{table}] WHERE [{sc.ClientId}] = @clid AND [{sc.SharedId}] = @shid AND [{sc.Name}] IN ({inParams})";
    }

    /// <summary>
    /// Строит SQL INSERT для сохранения параметра пользователя.
    /// НИКАКОГО UPDATE/MERGE — upsert делает триггер БД.
    /// </summary>
    public static string BuildInsertSql(string table, ClayGridSchemaMap s)
    {
        var sc = s.UserParams;
        return $"INSERT INTO [{table}] ([{sc.ClientId}],[{sc.Name}],[{sc.Value}],[{sc.SharedId}]) VALUES (@clid,@name,@value,@shid)";
    }

    /// <summary>
    /// Читает параметры пользователя по точным именам.
    /// Возвращает словарь (Параметр → Значение). Отсутствующие параметры → "".
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string>> LoadAsync(
        DbManager db, int clientId, IReadOnlyList<string> paramNames,
        string table, ClayGridSchemaMap s, int sharedId = 0, CancellationToken ct = default)
    {
        if (paramNames.Count == 0)
            return new Dictionary<string, string>();

        var sql = BuildLoadSql(table, s, paramNames.Count, sharedId);
        var dp  = new DynamicParameters();
        dp.Add("clid", clientId);
        dp.Add("shid", sharedId);
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
    /// При повторном вызове с тем же clientId+name+sharedId триггер БД делает UPDATE.
    /// </summary>
    public static Task SaveAsync(
        DbManager db, int clientId, string name, string value,
        string table, ClayGridSchemaMap s, int sharedId = 0, CancellationToken ct = default)
    {
        var sql = BuildInsertSql(table, s);
        return DynamicSql.ExecuteAsync(db, sql, new { clid = clientId, name, value, shid = sharedId }, ct);
    }

    /// <summary>
    /// Сохраняет НЕСКОЛЬКО параметров одним батч-запросом.
    /// При повторном вызове с тем же clientId+name+sharedId триггер БД делает UPDATE.
    /// </summary>
    public static async Task SaveManyAsync(
        DbManager db, int clientId, IReadOnlyList<(string Name, string Value)> items,
        string table, ClayGridSchemaMap s, int sharedId = 0, CancellationToken ct = default)
    {
        if (items.Count == 0) return;
        var sql = BuildInsertSql(table, s);
        var rows = items.Select(it => new
        {
            clid = clientId,
            name = it.Name,
            value = (object?)it.Value ?? DBNull.Value,
            shid = sharedId,
        }).ToArray();
        await DynamicSql.ExecuteAsync(db, sql, rows, ct);
    }
}
