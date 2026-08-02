using System.Data;
using Clayzor.Lib.DALC;
using Clayzor.Lib.Entities.DynamicGrid;
using Dapper;

namespace Clayzor.Lib.Entities.Tree;

/// <summary>
/// Класс данных дерева: выполняет запросы уровней через <see cref="DynamicSql"/>.
/// DbManager не создаёт — получает параметром (правило слоя данных решения).
/// </summary>
public static class ClayTreeData
{
    /// <summary>Загружает один уровень: детей узла или корневой уровень.</summary>
    public static async Task<List<ClayTreeRow>> LoadLevelAsync(
        DbManager db, ClayTreeSource src, ClayTreeRow? parent, CancellationToken ct = default)
    {
        var isRoot = parent is null;
        var sql = ClayTreeSqlBuilder.BuildLevelSql(src, isRoot);
        var dp = BuildParams(src, parent);
        var rows = await DynamicSql.QueryRowsAsync(db, sql, dp, ct: ct);
        return rows.Select(MapRow).ToList();
    }

    /// <summary>Загружает один узел по идентификатору; null — узла нет.</summary>
    public static async Task<ClayTreeRow?> LoadNodeAsync(
        DbManager db, ClayTreeSource src, object? id, CancellationToken ct = default)
    {
        if (id is null) return null;
        var sql = ClayTreeSqlBuilder.BuildNodeSql(src);
        var dp = new DynamicParameters();
        dp.Add(ClayTreeSqlBuilder.ParentParam, id);
        var rows = await DynamicSql.QueryRowsAsync(db, sql, dp, ct: ct);
        return rows.Select(MapRow).FirstOrDefault();
    }

    private static DynamicParameters BuildParams(ClayTreeSource src, ClayTreeRow? parent)
    {
        var dp = new DynamicParameters();
        if (parent is null)
        {
            if (src.RootId is not null)
            {
                dp.Add(ClayTreeSqlBuilder.ParentParam, src.RootId);
            }
            else if (src.Mode == ClayTreeHierarchyMode.ParentKey && src.Schema.RootParentValue is not null)
            {
                dp.Add(ClayTreeSqlBuilder.RootParentParam, src.Schema.RootParentValue);
            }
        }
        else
        {
            dp.Add(ClayTreeSqlBuilder.ParentParam, parent.Id);
            if (src.Mode == ClayTreeHierarchyMode.NestedSet)
            {
                dp.Add(ClayTreeSqlBuilder.LeftParam, parent.Left);
                dp.Add(ClayTreeSqlBuilder.RightParam, parent.Right);
                if (src.Schema.LevelColumn is not null)
                    dp.Add(ClayTreeSqlBuilder.LevelParam, parent.Level);

                if (src.PageSize is not null)
                {
                    dp.Add(ClayTreeSqlBuilder.PageSizeParam, src.PageSize.Value);
                    if (src.Cursor is not null)
                        dp.Add(ClayTreeSqlBuilder.CursorParam, src.Cursor.Value);
                }
            }
        }

        // Параметры ExtraWhere (дефолтный фильтр): добавляются в каждый запрос уровня
        if (src.ExtraWhereParams is { Count: > 0 })
        {
            foreach (var (name, value) in src.ExtraWhereParams)
                dp.Add(name, value);
        }

        return dp;
    }

    /// <summary>
    /// Загружает набор узлов в режиме фильтра: совпадения + все их предки с флагами.
    /// </summary>
    /// <param name="db">Менеджер БД.</param>
    /// <param name="src">Источник данных дерева.</param>
    /// <param name="whereClause">Фрагмент WHERE из ClayCompositeSqlBuilder (без слова WHERE).</param>
    /// <param name="dp">Параметры Dapper (метод НЕ добавляет @max — вызывающий должен добавить).</param>
    /// <param name="max">Максимальное число совпадений.</param>
    /// <param name="ct">Токен отмены.</param>
    public static async Task<List<ClayTreeRow>> LoadFilteredAsync(
        DbManager db, ClayTreeSource src, string whereClause, DynamicParameters dp, int max, CancellationToken ct = default)
    {
        if (!dp.ParameterNames.Contains(ClayTreeSqlBuilder.MaxParam))
            dp.Add(ClayTreeSqlBuilder.MaxParam, max);

        var sql = ClayTreeSqlBuilder.BuildFilterSql(src, whereClause, max);
        var rawRows = await DynamicSql.QueryRowsAsync(db, sql, dp, ct: ct);
        return rawRows.Select(rawRow =>
        {
            var r = MapRow(rawRow);
            r.IsMatch          = Convert.ToInt32(rawRow.GetValueOrDefault(ClayTreeSqlBuilder.AliasIsMatch) ?? 0) == 1;
            r.HasMatchChildren = Convert.ToInt32(rawRow.GetValueOrDefault(ClayTreeSqlBuilder.AliasHasMatchChildren) ?? 0) == 1;
            return r;
        }).ToList();
    }

    private static ClayTreeRow MapRow(IReadOnlyDictionary<string, object?> row)
    {
        var r = new ClayTreeRow
        {
            Id          = row.GetValueOrDefault(ClayTreeSqlBuilder.AliasId),
            Text        = row.GetValueOrDefault(ClayTreeSqlBuilder.AliasText)?.ToString() ?? "",
            HasChildren = Convert.ToInt32(row.GetValueOrDefault(ClayTreeSqlBuilder.AliasHasChildren) ?? 0) == 1,
        };

        if (row.TryGetValue(ClayTreeSqlBuilder.AliasParent, out var p)) r.ParentId = p;
        if (row.TryGetValue(ClayTreeSqlBuilder.AliasLeft, out var l) && l is not null && l is not DBNull)
            r.Left = Convert.ToInt64(l);
        if (row.TryGetValue(ClayTreeSqlBuilder.AliasRight, out var ri) && ri is not null && ri is not DBNull)
            r.Right = Convert.ToInt64(ri);
        if (row.TryGetValue(ClayTreeSqlBuilder.AliasLevel, out var lv) && lv is not null && lv is not DBNull)
            r.Level = Convert.ToInt32(lv);

        // Raw: ключи, не начинающиеся с '_'
        var raw = new Dictionary<string, object?>();
        foreach (var kv in row)
        {
            if (!kv.Key.StartsWith("_"))
                raw[kv.Key] = kv.Value;
        }
        r.Raw = raw;

        return r;
    }
}
