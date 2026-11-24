namespace Clayzor.Lib.Entities.TerritorialEntity;

// Feature-scoped until the coordinator assigns the shared SQLQueries.cs integration.
internal static class TerritorialEntitySql
{
    internal const string TerritoryRead = """
        SELECT t.КодТерритории AS Id, t.Parent AS ParentId, t.КодТТерритории AS Type, t.Территория AS Name,
            t.Территория1 AS AlternateName, t.Caption, t.Caption1 AS AlternateCaption,
            t.Аббревиатура AS Abbreviation, t.КодОКСМ AS CountryClassification,
            t.ТелКодСтраны AS CountryCode, t.Направление AS Direction,
            t.Изменил AS ChangedBy, e.Сотрудник AS ChangedByName, t.Изменено AS ChangedAt
        FROM dbo.Территории t {0}
        LEFT JOIN dbo.Сотрудники e ON e.КодСотрудника=t.Изменил
        WHERE t.КодТерритории=@Id;
        """;

    internal const string PhoneRead = """
        SELECT p.ТелефонныйКод AS Code, p.КодТерритории AS TerritoryId,
            CONVERT(int,p.ДлинаКодаОбласти) AS Length, p.КодТипаТелефоннойСвязи AS TypeId,
            p.Комментарий AS Comment, p.Изменил AS ChangedBy, e.Сотрудник AS ChangedByName, p.Изменено AS ChangedAt
        FROM dbo.ТелефонныеКоды p {0}
        LEFT JOIN dbo.Сотрудники e ON e.КодСотрудника=p.Изменил
        WHERE p.ТелефонныйКод=@Code;
        """;

    internal const string Access = """
        SELECT CONVERT(bit,ISNULL(HAS_PERMS_BY_NAME(N'dbo.Территории','OBJECT','UPDATE'),0)) AS TerritoryUpdate,
            CONVERT(bit,ISNULL(HAS_PERMS_BY_NAME(N'dbo.ТелефонныеКоды','OBJECT','INSERT'),0)) AS PhoneInsert,
            CONVERT(bit,ISNULL(HAS_PERMS_BY_NAME(N'dbo.ТелефонныеКоды','OBJECT','UPDATE'),0)) AS PhoneUpdate,
            CONVERT(bit,ISNULL(HAS_PERMS_BY_NAME(N'dbo.ТелефонныеКоды','OBJECT','DELETE'),0)) AS PhoneDelete;
        """;

    internal const string TerritoryTypes = """
        SELECT КодТТерритории AS Id, ТипТерриитории AS Name FROM dbo.ТипыТерриитории ORDER BY КодТТерритории;
        """;

    internal const string PhoneTypes = """
        SELECT КодТипаТелефоннойСвязи AS Id, ТипТелефоннойСвязи AS Name
        FROM dbo.ТипыТелефоннойСвязи ORDER BY КодТипаТелефоннойСвязи;
        """;

    internal const string Lookup = """
        SELECT КодТерритории AS Id, КодТТерритории AS Type, Территория AS Name,
            Caption, ТелКодСтраны AS CountryCode
        FROM dbo.Территории
        """;

    internal const string LookupById = Lookup + " WHERE КодТерритории=@Id;";
    internal const string LookupSearch = """
        SELECT TOP (@Limit) КодТерритории AS Id, КодТТерритории AS Type, Территория AS Name,
            Caption, ТелКодСтраны AS CountryCode
        FROM dbo.Территории WHERE КодТТерритории IN @Types
        """;

    internal const string PhoneTerritoryCheck = """
        SELECT COUNT(*) FROM dbo.Территории WITH (HOLDLOCK) WHERE КодТерритории=@TerritoryId AND КодТТерритории IN @Types;
        """;

    internal const string PhoneTypeCheck = """
        SELECT COUNT(*) FROM dbo.ТипыТелефоннойСвязи WITH (HOLDLOCK) WHERE КодТипаТелефоннойСвязи=@TypeId;
        """;

    internal const string TerritoryUpdate = """
        UPDATE dbo.Территории SET КодТТерритории=@Type, Территория=@Name, Территория1=@AlternateName,
            Caption=@Caption, Caption1=@AlternateCaption, Аббревиатура=@Abbreviation,
            КодОКСМ=@CountryClassification, ТелКодСтраны=@CountryCode, Направление=@Direction
        WHERE КодТерритории=@Id;
        """;

    internal const string PhoneInsert = """
        INSERT dbo.ТелефонныеКоды (ТелефонныйКод, КодТерритории, ДлинаКодаОбласти, КодТипаТелефоннойСвязи, Комментарий)
        VALUES (@Code, @TerritoryId, @Length, @TypeId, @Comment);
        """;

    internal const string PhoneUpdate = """
        UPDATE dbo.ТелефонныеКоды SET КодТерритории=@TerritoryId, ДлинаКодаОбласти=@Length,
            КодТипаТелефоннойСвязи=@TypeId, Комментарий=@Comment WHERE ТелефонныйКод=@Code;
        """;

    internal const string PhoneDelete = "DELETE dbo.ТелефонныеКоды WHERE ТелефонныйКод=@Code;";

    internal const string TreeSource = """
        SELECT t.КодТерритории AS Id, t.Территория AS Text, t.Parent AS ParentId,
            t.L AS [Left], t.R AS [Right], t.КодТТерритории AS Type
        FROM dbo.Территории t
        """;

    private const string ScopedTree = """
        WITH ScopeRows AS (
            SELECT t.КодТерритории AS Id, t.Территория AS Text,
                CASE WHEN @Restricted=1 AND t.КодТерритории IN @Roots THEN NULL ELSE t.Parent END AS ParentId,
                t.L AS [Left], t.R AS [Right], t.КодТТерритории AS Type
            FROM dbo.Территории t
            WHERE @Restricted=0 OR EXISTS (
                SELECT 1 FROM dbo.Территории r
                WHERE r.КодТерритории IN @Roots AND r.L<=t.L AND t.R<=r.R)
        )
        """;

    private const string Children = "CONVERT(bit,CASE WHEN EXISTS (SELECT 1 FROM ScopeRows c WHERE c.ParentId=n.Id) THEN 1 ELSE 0 END) AS HasChildren";

    internal static string TreeLevel => $"""
        {ScopedTree}
        SELECT n.*, {Children} FROM ScopeRows n
        WHERE n.ParentId=@ParentId OR (@ParentId IS NULL AND n.ParentId IS NULL)
        ORDER BY n.Text, n.Id;
        """;

    internal static string TreeFilter(string predicate) => $"""
        {ScopedTree}
        SELECT COUNT(*) FROM ScopeRows WHERE {predicate};
        {ScopedTree}, Matches AS (
            SELECT TOP (@Limit) Id, [Left], [Right] FROM ScopeRows WHERE {predicate} ORDER BY [Left]
        )
        SELECT n.*, {Children},
            CONVERT(bit,CASE WHEN EXISTS (SELECT 1 FROM Matches m WHERE m.Id=n.Id) THEN 1 ELSE 0 END) AS IsMatch,
            CONVERT(bit,CASE WHEN EXISTS (SELECT 1 FROM Matches m WHERE n.[Left]<m.[Left] AND m.[Right]<n.[Right]) THEN 1 ELSE 0 END) AS HasMatchChildren
        FROM ScopeRows n WHERE EXISTS (SELECT 1 FROM Matches m WHERE n.[Left]<=m.[Left] AND m.[Right]<=n.[Right])
        ORDER BY n.Text,n.Id;
        """;

    internal const string CountryCodeSearch = " AND ТелКодСтраны=@CountryCode";
    internal static string LookupWords(int count)
    {
        var tokens = Enumerable.Range(0, count).ToArray();
        var russian = string.Join(" AND ", tokens.Select(index => $"Территория LIKE @word{index}"));
        var english = string.Join(" AND ", tokens.Select(index => $"Caption LIKE @word{index}"));
        return $" AND (({russian}) OR ({english}))";
    }
    internal const string LookupOrder = " ORDER BY Территория, КодТерритории;";
}
