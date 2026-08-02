using System.Text;

namespace Clayzor.Lib.Entities.Tree;

/// <summary>
/// Построение SQL для ленивой загрузки одного уровня дерева.
/// Все методы — чистые функции (тестируются без БД).
/// </summary>
public static class ClayTreeSqlBuilder
{
    /// <summary>Псевдоним выходной колонки идентификатора: <c>[_id]</c>.</summary>
    public const string AliasId = "_id";
    /// <summary>Псевдоним выходной колонки текста: <c>[_text]</c>.</summary>
    public const string AliasText = "_text";
    /// <summary>Псевдоним выходной колонки родителя: <c>[_parent]</c>.</summary>
    public const string AliasParent = "_parent";
    /// <summary>Псевдоним выходной колонки левого ключа: <c>[_left]</c>.</summary>
    public const string AliasLeft = "_left";
    /// <summary>Псевдоним выходной колонки правого ключа: <c>[_right]</c>.</summary>
    public const string AliasRight = "_right";
    /// <summary>Псевдоним выходной колонки уровня: <c>[_level]</c>.</summary>
    public const string AliasLevel = "_level";
    /// <summary>Псевдоним выходной колонки «есть дети»: <c>[_haschildren]</c>.</summary>
    public const string AliasHasChildren = "_haschildren";
    /// <summary>Псевдоним выходной колонки «узел совпал с фильтром»: <c>[_ismatch]</c>.</summary>
    public const string AliasIsMatch = "_ismatch";
    /// <summary>Псевдоним выходной колонки «есть совпавшие потомки»: <c>[_hasmatchchildren]</c>.</summary>
    public const string AliasHasMatchChildren = "_hasmatchchildren";

    /// <summary>Имя параметра идентификатора родителя.</summary>
    public const string ParentParam = "parentId";
    /// <summary>Имя параметра левого ключа родителя (режим NestedSet).</summary>
    public const string LeftParam = "left";
    /// <summary>Имя параметра правого ключа родителя (режим NestedSet).</summary>
    public const string RightParam = "right";
    /// <summary>Имя параметра уровня родителя (режим NestedSet).</summary>
    public const string LevelParam = "level";
    /// <summary>Имя параметра «значение ссылки на корень» (режим ParentKey).</summary>
    public const string RootParentParam = "rootParent";
    /// <summary>Имя параметра размера порции для кейсет-пагинации.</summary>
    public const string PageSizeParam = "pageSize";
    /// <summary>Имя параметра курсора для кейсет-пагинации (значение L последней загруженной ноды).</summary>
    public const string CursorParam = "cursor";
    /// <summary>Имя параметра максимального числа совпадений в режиме фильтра.</summary>
    public const string MaxParam = "max";

    /// <summary>SQL для загрузки одного уровня. <c>isRoot</c> = true — корневой уровень.</summary>
    public static string BuildLevelSql(ClayTreeSource src, bool isRoot)
    {
        src.Schema.Validate(src.Mode);

        var orderBy = BuildOrderBy(src);
        return src.Mode switch
        {
            ClayTreeHierarchyMode.NestedSet => BuildNestedSetSql(src, isRoot, orderBy),
            ClayTreeHierarchyMode.ParentKey => BuildParentKeySql(src, isRoot, orderBy),
            _ => throw new InvalidOperationException($"Неизвестный режим иерархии: {src.Mode}"),
        };
    }

    /// <summary>SQL для загрузки одного узла по идентификатору.</summary>
    public static string BuildNodeSql(ClayTreeSource src)
    {
        src.Schema.Validate(src.Mode);

        var selectList = BuildSelectList(src);
        return $"SELECT {selectList} FROM ({src.SelectSql}) s WHERE s.[{src.Schema.IdColumn}] = @{ParentParam}";
    }

    private static string BuildNestedSetSql(ClayTreeSource src, bool isRoot, string orderBy)
    {
        var selectList = BuildSelectList(src);
        var sb = new StringBuilder();

        // TOP — только для не-корневого NestedSet с пагинацией
        if (!isRoot && src.PageSize is not null)
            sb.Append("SELECT TOP (@").Append(PageSizeParam).Append(" + 1) ").Append(selectList);
        else
            sb.Append("SELECT ").Append(selectList);

        sb.Append(" FROM (").Append(src.SelectSql).Append(") s");

        if (isRoot)
        {
            if (src.RootId is not null)
            {
                sb.Append(" WHERE s.[").Append(src.Schema.IdColumn).Append("] = @").Append(ParentParam);
            }
            else if (src.Schema.LevelColumn is not null)
            {
                sb.Append(" WHERE s.[").Append(src.Schema.LevelColumn)
                  .Append("] = (SELECT MIN(m.[").Append(src.Schema.LevelColumn)
                  .Append("]) FROM (").Append(src.SelectSql).Append(") m)");
            }
            else
            {
                // Без LevelColumn: корни — узлы, не содержащиеся ни в каком другом
                sb.Append(" WHERE NOT EXISTS (SELECT 1 FROM (").Append(src.SelectSql)
                  .Append(") p WHERE p.[").Append(src.Schema.LeftColumn)
                  .Append("] < s.[").Append(src.Schema.LeftColumn)
                  .Append("] AND p.[").Append(src.Schema.RightColumn)
                  .Append("] > s.[").Append(src.Schema.RightColumn).Append("])");
            }
        }
        else
        {
            sb.Append(" WHERE s.[").Append(src.Schema.LeftColumn).Append("] > @").Append(LeftParam);
            sb.Append(" AND s.[").Append(src.Schema.RightColumn).Append("] < @").Append(RightParam);
            if (src.Schema.LevelColumn is not null)
            {
                sb.Append(" AND s.[").Append(src.Schema.LevelColumn).Append("] = @").Append(LevelParam).Append(" + 1");
            }
            else
            {
                // Колонки уровня нет: прямой ребёнок — узел внутри диапазона,
                // для которого нет промежуточного предка внутри того же диапазона.
                sb.Append(" AND NOT EXISTS (SELECT 1 FROM (").Append(src.SelectSql)
                  .Append(") m WHERE m.[").Append(src.Schema.LeftColumn).Append("] > @").Append(LeftParam)
                  .Append(" AND m.[").Append(src.Schema.RightColumn).Append("] < @").Append(RightParam)
                  .Append(" AND m.[").Append(src.Schema.LeftColumn).Append("] < s.[").Append(src.Schema.LeftColumn)
                  .Append("] AND m.[").Append(src.Schema.RightColumn).Append("] > s.[").Append(src.Schema.RightColumn).Append("])");
            }

            if (src.PageSize is not null && src.Cursor is not null)
            {
                sb.Append(" AND s.[").Append(src.Schema.LeftColumn).Append("] > @").Append(CursorParam);
            }
        }

        AppendExtraWhere(sb, src);
        sb.Append(" ORDER BY ").Append(orderBy);
        return sb.ToString();
    }

    private static string BuildParentKeySql(ClayTreeSource src, bool isRoot, string orderBy)
    {
        var selectList = BuildSelectList(src);
        var sb = new StringBuilder();
        sb.Append("SELECT ").Append(selectList);
        sb.Append(" FROM (").Append(src.SelectSql).Append(") s");

        if (isRoot)
        {
            sb.Append(" WHERE ");
            if (src.RootId is not null)
            {
                sb.Append("s.[").Append(src.Schema.IdColumn).Append("] = @").Append(ParentParam);
            }
            else if (src.Schema.RootParentValue is null)
            {
                sb.Append("s.[").Append(src.Schema.ParentColumn).Append("] IS NULL");
            }
            else
            {
                sb.Append("s.[").Append(src.Schema.ParentColumn).Append("] = @").Append(RootParentParam);
            }
        }
        else
        {
            sb.Append(" WHERE s.[").Append(src.Schema.ParentColumn).Append("] = @").Append(ParentParam);
        }

        AppendExtraWhere(sb, src);
        sb.Append(" ORDER BY ").Append(orderBy);
        return sb.ToString();
    }

    private static void AppendExtraWhere(StringBuilder sb, ClayTreeSource src)
    {
        if (!string.IsNullOrWhiteSpace(src.ExtraWhere))
            sb.Append(" AND (").Append(src.ExtraWhere).Append(")");
    }

    private static string BuildSelectList(ClayTreeSource src)
    {
        var sb = new StringBuilder();
        sb.Append("s.[").Append(src.Schema.IdColumn).Append("] AS [").Append(AliasId).Append("], ");
        sb.Append("s.[").Append(src.Schema.TextColumn).Append("] AS [").Append(AliasText).Append("]");

        if (src.Mode == ClayTreeHierarchyMode.ParentKey)
        {
            sb.Append(", s.[").Append(src.Schema.ParentColumn).Append("] AS [").Append(AliasParent).Append("]");
        }
        else if (src.Mode == ClayTreeHierarchyMode.NestedSet && src.Schema.ParentColumn is not null)
        {
            sb.Append(", s.[").Append(src.Schema.ParentColumn).Append("] AS [").Append(AliasParent).Append("]");
        }

        if (src.Mode == ClayTreeHierarchyMode.NestedSet)
        {
            sb.Append(", s.[").Append(src.Schema.LeftColumn).Append("] AS [").Append(AliasLeft).Append("]");
            sb.Append(", s.[").Append(src.Schema.RightColumn).Append("] AS [").Append(AliasRight).Append("]");
            if (src.Schema.LevelColumn is not null)
                sb.Append(", s.[").Append(src.Schema.LevelColumn).Append("] AS [").Append(AliasLevel).Append("]");
        }

        // HasChildren
        if (src.Mode == ClayTreeHierarchyMode.NestedSet)
        {
            sb.Append(", CASE WHEN s.[").Append(src.Schema.RightColumn).Append("] - s.[").Append(src.Schema.LeftColumn).Append("] > 1 THEN 1 ELSE 0 END AS [").Append(AliasHasChildren).Append("]");
        }
        else
        {
            sb.Append(", CASE WHEN EXISTS (SELECT 1 FROM (").Append(src.SelectSql).Append(") c WHERE c.[").Append(src.Schema.ParentColumn).Append("] = s.[").Append(src.Schema.IdColumn).Append("]) THEN 1 ELSE 0 END AS [").Append(AliasHasChildren).Append("]");
        }

        // Extra columns
        foreach (var col in src.Schema.ExtraColumns)
        {
            sb.Append(", s.[").Append(col).Append("]");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Строит SQL-запрос режима фильтра: находит совпадения (TOP @max + 1), добирает всех
    /// предков и проставляет флаги <c>[_ismatch]</c> / <c>[_hasmatchchildren]</c>.
    /// </summary>
    /// <param name="src">Источник данных дерева.</param>
    /// <param name="whereClause">Фрагмент WHERE из <c>ClayCompositeSqlBuilder</c> (без слова WHERE).</param>
    /// <param name="max">Максимальное число совпадений (MaxFilterRecords).</param>
    public static string BuildFilterSql(ClayTreeSource src, string whereClause, int max)
    {
        src.Schema.Validate(src.Mode);
        return src.Mode switch
        {
            ClayTreeHierarchyMode.NestedSet => BuildNestedSetFilterSql(src, whereClause, max),
            ClayTreeHierarchyMode.ParentKey => BuildParentKeyFilterSql(src, whereClause, max),
            _ => throw new InvalidOperationException($"Неизвестный режим иерархии: {src.Mode}"),
        };
    }

    private static string BuildNestedSetFilterSql(ClayTreeSource src, string whereClause, int max)
    {
        var id = $"[{src.Schema.IdColumn}]";
        var text = $"[{src.Schema.TextColumn}]";
        var left = $"[{src.Schema.LeftColumn}]";
        var right = $"[{src.Schema.RightColumn}]";
        var parent = src.Schema.ParentColumn is not null ? $"[{src.Schema.ParentColumn}]" : null;
        var orderBy = BuildOrderBy(src);

        var sb = new StringBuilder();
        sb.Append("WITH Src AS (SELECT * FROM (").Append(src.SelectSql).Append(") x),");
        sb.Append("Matches AS (SELECT TOP (@").Append(MaxParam).Append(" + 1) s.").Append(left).Append(" AS L, s.").Append(right).Append(" AS R, s.").Append(id).Append(" AS Id FROM Src s WHERE ").Append(whereClause).Append(" ORDER BY s.").Append(left).Append(")");
        sb.Append("SELECT s.").Append(id).Append(" AS [").Append(AliasId).Append("], ");
        sb.Append("s.").Append(text).Append(" AS [").Append(AliasText).Append("]");
        if (parent is not null)
            sb.Append(", s.").Append(parent).Append(" AS [").Append(AliasParent).Append("]");
        sb.Append(", s.").Append(left).Append(" AS [").Append(AliasLeft).Append("]");
        sb.Append(", s.").Append(right).Append(" AS [").Append(AliasRight).Append("]");
        if (src.Schema.LevelColumn is not null)
            sb.Append(", s.[").Append(src.Schema.LevelColumn).Append("] AS [").Append(AliasLevel).Append("]");
        sb.Append(", CASE WHEN EXISTS (SELECT 1 FROM Matches m WHERE m.Id = s.").Append(id).Append(") THEN 1 ELSE 0 END AS [").Append(AliasIsMatch).Append("]");
        sb.Append(", CASE WHEN EXISTS (SELECT 1 FROM Matches m WHERE m.L > s.").Append(left).Append(" AND m.R < s.").Append(right).Append(") THEN 1 ELSE 0 END AS [").Append(AliasHasMatchChildren).Append("]");
        sb.Append(" FROM Src s");
        sb.Append(" WHERE EXISTS (SELECT 1 FROM Matches m WHERE m.Id = s.").Append(id).Append(")");
        sb.Append(" OR EXISTS (SELECT 1 FROM Matches m WHERE s.").Append(left).Append(" < m.L AND s.").Append(right).Append(" > m.R)");
        sb.Append(" ORDER BY s.").Append(left);

        return sb.ToString();
    }

    private static string BuildParentKeyFilterSql(ClayTreeSource src, string whereClause, int max)
    {
        var id = $"[{src.Schema.IdColumn}]";
        var text = $"[{src.Schema.TextColumn}]";
        var parent = $"[{src.Schema.ParentColumn}]";
        var orderBy = BuildOrderBy(src);

        var sb = new StringBuilder();
        sb.Append("WITH Src AS (SELECT * FROM (").Append(src.SelectSql).Append(") x),");
        sb.Append("Matches AS (SELECT TOP (@").Append(MaxParam).Append(" + 1) s.").Append(id).Append(" AS Id, s.").Append(parent).Append(" AS Parent FROM Src s WHERE ").Append(whereClause).Append(" ORDER BY s.").Append(text).Append("),");
        sb.Append("Chain AS (");
        sb.Append("SELECT m.Id, m.Parent, CAST(1 AS bit) AS IsMatchSeed FROM Matches m");
        sb.Append(" UNION ALL");
        sb.Append(" SELECT p.").Append(id).Append(", p.").Append(parent).Append(", CAST(0 AS bit)");
        sb.Append(" FROM Src p INNER JOIN Chain c ON p.").Append(id).Append(" = c.Parent");
        sb.Append("),");
        sb.Append("Agg AS (");
        sb.Append("SELECT Id, MAX(CAST(IsMatchSeed AS int)) AS IsMatch FROM Chain GROUP BY Id");
        sb.Append(")");
        sb.Append("SELECT s.").Append(id).Append(" AS [").Append(AliasId).Append("], ");
        sb.Append("s.").Append(text).Append(" AS [").Append(AliasText).Append("], ");
        sb.Append("s.").Append(parent).Append(" AS [").Append(AliasParent).Append("], ");
        sb.Append("a.IsMatch AS [").Append(AliasIsMatch).Append("], ");
        sb.Append("CASE WHEN a.IsMatch = 0 THEN 1 ELSE 0 END AS [").Append(AliasHasMatchChildren).Append("]");
        sb.Append(" FROM Src s JOIN Agg a ON a.Id = s.").Append(id);
        sb.Append(" ORDER BY ").Append(orderBy);

        return sb.ToString();
    }

    /// <summary>
    /// Проверяет, что каждый идентификатор в ORDER BY — известная колонка схемы или псевдоним,
    /// и возвращает безопасный ORDER BY.
    /// </summary>
    public static string BuildOrderBy(ClayTreeSource src)
    {
        if (string.IsNullOrWhiteSpace(src.OrderBy))
        {
            return src.Mode switch
            {
                ClayTreeHierarchyMode.NestedSet => $"[{src.Schema.LeftColumn}]",
                ClayTreeHierarchyMode.ParentKey => $"[{src.Schema.TextColumn}]",
                _ => $"[{src.Schema.IdColumn}]",
            };
        }

        var knownColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            src.Schema.IdColumn,
            src.Schema.TextColumn,
        };
        if (src.Schema.ParentColumn is not null) knownColumns.Add(src.Schema.ParentColumn);
        if (src.Schema.LeftColumn is not null) knownColumns.Add(src.Schema.LeftColumn);
        if (src.Schema.RightColumn is not null) knownColumns.Add(src.Schema.RightColumn);
        if (src.Schema.LevelColumn is not null) knownColumns.Add(src.Schema.LevelColumn);
        foreach (var col in src.Schema.ExtraColumns) knownColumns.Add(col);

        // Разбираем ORDER BY на идентификаторы и проверяем
        var parts = src.OrderBy.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var checkedParts = new List<string>();
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            // Извлекаем имя колонки (без [ ], DESC/ASC)
            var name = trimmed.Replace("[", "").Replace("]", "").Trim();
            var spaceIdx = name.IndexOf(' ');
            if (spaceIdx > 0) name = name[..spaceIdx];

            if (!knownColumns.Contains(name))
                throw new InvalidOperationException($"Колонка '{name}' из ORDER BY не найдена в схеме источника дерева. Допустимые колонки: {string.Join(", ", knownColumns)}.");

            // Оборачиваем в квадратные скобки если ещё не обёрнуто
            var bracketed = name;
            if (!trimmed.StartsWith("["))
            {
                var suffix = spaceIdx > 0 ? trimmed[(spaceIdx + 1)..] : "";
                bracketed = $"[{name}]" + (suffix.Length > 0 ? " " + suffix : "");
            }
            else
            {
                bracketed = trimmed;
            }
            checkedParts.Add(bracketed);
        }

        return string.Join(", ", checkedParts);
    }
}
