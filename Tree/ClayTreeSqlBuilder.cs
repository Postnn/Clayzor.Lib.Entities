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
                sb.Append(" AND s.[").Append(src.Schema.LevelColumn).Append("] = @").Append(LevelParam).Append(" + 1");
        }

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

        sb.Append(" ORDER BY ").Append(orderBy);
        return sb.ToString();
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
