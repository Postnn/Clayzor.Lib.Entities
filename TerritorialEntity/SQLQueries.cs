namespace Clayzor.Lib.Entities.TerritorialEntity;

// Feature-scoped until the coordinator assigns the shared SQLQueries.cs integration.
internal static class TerritorialEntitySql
{
    internal const string LatestOwnMove = """
        WITH Attempts AS
        (
            SELECT AttemptId, EmployeeId, ConflictKey, CommandKind, State, ResultEntityId,
                   CreatedUtc, SettledUtc, CAST(0 AS bit) AS Imported
              FROM dbo.ClayzorOperationJournal
             WHERE EmployeeId = @EmployeeId AND ConflictKey = @ConflictKey AND CommandKind = @CommandKind
            UNION ALL
            SELECT AttemptId, EmployeeId, ConflictKey, CommandKind, @Unknown, NULL,
                   ImportedUtc, NULL, CAST(1 AS bit)
              FROM dbo.ClayzorLegacyOperationEvidence
             WHERE EmployeeId = @EmployeeId AND ConflictKey = @ConflictKey AND CommandKind = @CommandKind
               AND ResolvedUtc IS NULL
        ),
        Latest AS
        (
            SELECT TOP (1) AttemptId, EmployeeId, ConflictKey, CommandKind, State, ResultEntityId, Imported
              FROM Attempts
             ORDER BY CASE WHEN SettledUtc IS NULL THEN 0 ELSE 1 END, CreatedUtc DESC, AttemptId DESC, Imported DESC
        )
        SELECT l.AttemptId, l.EmployeeId, l.ConflictKey, l.CommandKind, l.State, l.ResultEntityId,
               s.ConflictKey AS RelatedScope
          FROM Latest l
         OUTER APPLY
        (
            SELECT ConflictKey FROM dbo.ClayzorOperationScope WHERE AttemptId = l.AttemptId AND l.Imported = 0
            UNION ALL
            SELECT ConflictKey FROM dbo.ClayzorLegacyOperationScope WHERE AttemptId = l.AttemptId AND l.Imported = 1
        ) s;
        """;

    internal const string ReadMoveNode = """
        SELECT КодТерритории AS Id, Parent AS ParentId, L AS [Left], R AS [Right]
        FROM dbo.Территории WITH (UPDLOCK,HOLDLOCK) WHERE КодТерритории=@Id;
        """;
    internal const string Reparent = "UPDATE dbo.Территории SET Parent=@ParentId WHERE КодТерритории=@Id;";

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
            CONVERT(bit,ISNULL(HAS_PERMS_BY_NAME(N'dbo.Территории','OBJECT','INSERT'),0)) AS TerritoryInsert,
            CONVERT(bit,ISNULL(HAS_PERMS_BY_NAME(N'master.dbo.sp_Delete_Территория','OBJECT','EXECUTE'),0)) AS TerritoryDelete,
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

    // Legacy INSERT returns the key from the database result, including any result produced by its triggers.
    internal const string TerritoryInsert = """
        INSERT dbo.Территории (КодТТерритории, Территория, Caption, Территория1, Caption1,
            Аббревиатура, КодОКСМ, ТелКодСтраны, Направление, Parent)
        VALUES (@Type, @Name, @Caption, @AlternateName, @AlternateCaption, @Abbreviation,
            @CountryClassification, @CountryCode, @Direction, @ParentId);
        """;

    internal const string TerritoryDelete = "master.dbo.sp_Delete_Территория";

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

    internal const string LoadViewState = "SELECT Значение FROM dbo.vwНастройки WHERE КодНастройкиКлиента=@Clid AND КодНастройкиОбщей=0 AND Параметр='InvTerTreeState'";
    internal const string SaveViewState = "INSERT dbo.vwНастройки (КодНастройкиКлиента,КодНастройкиОбщей,Параметр,Значение) VALUES (@Clid,0,'InvTerTreeState',@Value)";

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

    internal static string TreePath => ScopedTree + """
         SELECT a.Id FROM ScopeRows a JOIN ScopeRows t ON a.[Left]<=t.[Left] AND t.[Right]<=a.[Right]
         WHERE t.Id=@Id ORDER BY a.[Left];
        """;

    internal static string TreeSelectedIds => ScopedTree + " SELECT Id FROM ScopeRows WHERE Id IN @Ids ORDER BY Id;";

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
