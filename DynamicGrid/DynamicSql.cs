using System.Collections.ObjectModel;
using System.Data;
using Dapper;
using Clayzor.Lib.DALC;

namespace Clayzor.Lib.Entities.DynamicGrid;

/// <summary>
/// Помощники выполнения динамического (произвольного) SQL.
/// Единственная точка доступа к БД для слоя Controls — он вызывает эти методы,
/// передавая инжектированный <see cref="DbManager"/>.
/// </summary>
public static class DynamicSql
{
    /// <summary>
    /// Выполняет SELECT и возвращает строки как словари колонка→значение.
    /// </summary>
    public static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryRowsAsync(
        DbManager db, string sql, object? param = null, CancellationToken ct = default)
    {
        var cmd = new CommandDefinition(sql, param, cancellationToken: ct);
        var rows = await db.RunAsync(c => c.QueryAsync(cmd));
        return rows
            .Select(r => (IReadOnlyDictionary<string, object?>)
                new ReadOnlyDictionary<string, object?>((IDictionary<string, object?>)r))
            .ToList();
    }

    /// <summary>
    /// Постраничный SELECT (ROW_NUMBER, SQL Server 2008 R2).
    /// Паттерн идентичен <see cref="Entity.GetPagedAsync{T}"/>.
    /// </summary>
    public static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryPagedRowsAsync(
        DbManager db, string selectSql, string? where, string? orderBy,
        object? param, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var filteredSql = where is not null
            ? $"SELECT * FROM ({selectSql}) _q WHERE {where}"
            : $"SELECT * FROM ({selectSql}) _q";

        var orderByClause = orderBy ?? "(SELECT 0)";
        var sql = $"SELECT * FROM (SELECT _src.*, ROW_NUMBER() OVER (ORDER BY {orderByClause}) AS _rn"
                + $" FROM ({filteredSql}) _src) _p WHERE _rn BETWEEN @__start AND @__end";

        var parameters = new DynamicParameters();
        if (param is not null)
            parameters.AddDynamicParams(param);
        parameters.Add("__start", (pageNumber - 1) * pageSize + 1);
        parameters.Add("__end", pageNumber * pageSize);

        var cmd = new CommandDefinition(sql, parameters, cancellationToken: ct);
        var rows = await db.RunAsync(c => c.QueryAsync(cmd));
        return rows
            .Select(r => (IReadOnlyDictionary<string, object?>)
                new ReadOnlyDictionary<string, object?>((IDictionary<string, object?>)r))
            .ToList();
    }

    /// <summary>
    /// Возвращает общее количество записей для постраничного запроса.
    /// Паттерн идентичен <see cref="Entity.GetCountAsync{T}"/>.
    /// </summary>
    public static async Task<int> QueryCountAsync(
        DbManager db, string selectSql, string? where, object? param = null, CancellationToken ct = default)
    {
        var filteredSql = where is not null
            ? $"SELECT * FROM ({selectSql}) _q WHERE {where}"
            : $"SELECT * FROM ({selectSql}) _q";
        var sql = $"SELECT COUNT(*) FROM ({filteredSql}) AS _cnt";

        var cmd = new CommandDefinition(sql, param, cancellationToken: ct);
        return await db.RunAsync(c => c.ExecuteScalarAsync<int>(cmd));
    }

    /// <summary>
    /// Выполняет SQL и возвращает пары (значение, текст) из первых двух колонок.
    /// Используется для справочников типа «Список» (Тип 5).
    /// </summary>
    public static async Task<IReadOnlyList<(object? Value, string? Text)>> QueryPairsAsync(
        DbManager db, string sql, object? param = null, CancellationToken ct = default)
    {
        var cmd = new CommandDefinition(sql, param, cancellationToken: ct);
        var rows = await db.RunAsync(c => c.QueryAsync<(object?, string?)>(cmd));
        return rows.Select(r => (r.Item1, r.Item2)).ToList();
    }

    /// <summary>
    /// Выполняет SQL и возвращает тройки (значение, тултип, href иконки) из первых трёх колонок.
    /// Используется для типа «Пиктограмма» (Тип 9).
    /// </summary>
    public static async Task<IReadOnlyList<(object? Value, string? Text, string? Icon)>> QueryTriplesAsync(
        DbManager db, string sql, object? param = null, CancellationToken ct = default)
    {
        var cmd = new CommandDefinition(sql, param, cancellationToken: ct);
        var rows = await db.RunAsync(c => c.QueryAsync<(object?, string?, string?)>(cmd));
        return rows.Select(r => (r.Item1, r.Item2, r.Item3)).ToList();
    }

    /// <summary>
    /// Выполняет не-запрос (DELETE/INSERT/UPDATE) и возвращает количество затронутых строк.
    /// </summary>
    public static async Task<int> ExecuteAsync(
        DbManager db, string sql, object? param = null, CancellationToken ct = default)
    {
        var cmd = new CommandDefinition(sql, param, commandType: CommandType.Text, cancellationToken: ct);
        return await db.RunAsync(c => c.ExecuteAsync(cmd));
    }
}
