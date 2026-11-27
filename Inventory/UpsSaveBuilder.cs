using System.Data;
using System.Text;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

internal static class UpsSaveBuilder
{
    internal static EquipmentPhoneSaveCommand Build(
        int upsEquipmentId,
        int sourceServerEquipmentId,
        IReadOnlyCollection<UpsServerLink> originalServers,
        IReadOnlyCollection<UpsServerLink> currentServers,
        IReadOnlyCollection<UpsTest> originalTests,
        IReadOnlyCollection<UpsTest> currentTests) =>
        BuildCore(upsEquipmentId, sourceServerEquipmentId, originalServers, currentServers,
            originalTests, currentTests, enlisted: false, out _);

    internal static EquipmentPhoneSaveCommand BuildEnlisted(
        int upsEquipmentId,
        int sourceServerEquipmentId,
        IReadOnlyCollection<UpsServerLink> originalServers,
        IReadOnlyCollection<UpsServerLink> currentServers,
        IReadOnlyCollection<UpsTest> originalTests,
        IReadOnlyCollection<UpsTest> currentTests,
        out int[] affectedServerIds) =>
        BuildCore(upsEquipmentId, sourceServerEquipmentId, originalServers, currentServers,
            originalTests, currentTests, enlisted: true, out affectedServerIds);

    private static EquipmentPhoneSaveCommand BuildCore(
        int upsEquipmentId,
        int sourceServerEquipmentId,
        IReadOnlyCollection<UpsServerLink> originalServers,
        IReadOnlyCollection<UpsServerLink> currentServers,
        IReadOnlyCollection<UpsTest> originalTests,
        IReadOnlyCollection<UpsTest> currentTests,
        bool enlisted,
        out int[] affectedServerIds)
    {
        if (originalServers.Any(item => item.ServerEquipmentId <= 0) ||
            currentServers.Any(item => item.ServerEquipmentId <= 0) ||
            originalServers.GroupBy(item => item.ServerEquipmentId).Any(group => group.Count() > 1) ||
            currentServers.GroupBy(item => item.ServerEquipmentId).Any(group => group.Count() > 1))
            throw new InvalidOperationException("В списке серверов UPS есть недопустимый или повторяющийся идентификатор.");
        if (originalTests.Any(item => item.Id <= 0) ||
            originalTests.GroupBy(item => item.Id).Any(group => group.Count() > 1) ||
            currentTests.Where(item => item.Id > 0).GroupBy(item => item.Id).Any(group => group.Count() > 1))
            throw new InvalidOperationException("В списке тестов UPS есть недопустимый или повторяющийся идентификатор.");

        var currentIds = currentServers.Select(item => item.ServerEquipmentId).ToHashSet();
        var originalById = originalServers.ToDictionary(item => item.ServerEquipmentId);
        var originalTestsById = originalTests.ToDictionary(item => item.Id);
        if (currentTests.Any(item => item.Id < 0 || item.Id > 0 && !originalTestsById.ContainsKey(item.Id)))
            throw new InvalidOperationException("Список тестов UPS устарел. Обновите диалог.");
        var removedServers = originalServers.Where(item => !currentIds.Contains(item.ServerEquipmentId)).ToList();
        var updatedServers = currentServers.Where(item =>
            originalById.TryGetValue(item.ServerEquipmentId, out var original) &&
            original.TurnOffConfigured != item.TurnOffConfigured).ToList();
        var addedServers = currentServers.Where(item => !originalById.ContainsKey(item.ServerEquipmentId)).ToList();
        var computerTypeId = (int)ТипыОборудования.Компьютер;
        if (updatedServers.Any(item => item.EquipmentTypeId > 0 && item.EquipmentTypeId != computerTypeId) ||
            addedServers.Any(item => item.TurnOffConfigured > 0 && item.EquipmentTypeId > 0 && item.EquipmentTypeId != computerTypeId))
            throw new InvalidOperationException("Признак выключения доступен только для компьютеров.");
        affectedServerIds = removedServers.Concat(updatedServers).Concat(addedServers)
            .Select(item => item.ServerEquipmentId).Distinct().Order().ToArray();
        var updatedTests = currentTests.Where(item =>
            originalTestsById.TryGetValue(item.Id, out var original) &&
            (original.TestedAt != item.TestedAt || original.BatteryTime != item.BatteryTime ||
             !string.Equals(original.Servers, item.Servers, StringComparison.Ordinal) ||
             !string.Equals(original.Issues, item.Issues, StringComparison.Ordinal))).ToList();
        var addedTests = currentTests.Where(item => item.Id == 0).ToList();
        if (enlisted && (originalServers.Concat(currentServers).Any(item => item.UpsEquipmentId != 0 && item.UpsEquipmentId != upsEquipmentId) ||
            originalTests.Concat(currentTests).Any(item => item.UpsEquipmentId != 0 && item.UpsEquipmentId != upsEquipmentId)))
            throw new InvalidOperationException("Связь или тест относятся к другому UPS. Обновите данные.");

        var parameters = new DynamicParameters();
        var sql = new StringBuilder();
        var proof = new StringBuilder();
        if (!enlisted)
        {
            sql.AppendLine("SET NOCOUNT ON;");
            sql.AppendLine("SET XACT_ABORT ON;");
            sql.AppendLine("BEGIN TRY");
            sql.AppendLine("    BEGIN TRANSACTION;");
        }
        AppendAccessGuard(sql, parameters, upsEquipmentId, sourceServerEquipmentId, affectedServerIds, enlisted);
        AppendApplicabilityGuard(sql, parameters, updatedServers, addedServers, enlisted);
        var index = 0;
        foreach (var removed in removedServers)
        {
            parameters.Add($"@srvDel{index}Ups", upsEquipmentId, DbType.Int32);
            parameters.Add($"@srvDel{index}Server", removed.ServerEquipmentId, DbType.Int32);
            parameters.Add($"@srvDel{index}OriginalFlag", removed.TurnOffConfigured, DbType.Byte);
            if (enlisted)
            {
                AppendLinkGuard(sql, $"srvDel{index}", 1, $"@srvDel{index}OriginalFlag", 2);
                AppendLinkGuard(proof, $"srvDel{index}", 0, null, 5);
            }
            sql.AppendLine($"    DELETE vwПодключенияUPS WHERE КодОборудованияUPS = @srvDel{index}Ups AND КодОборудованияСервера = @srvDel{index}Server AND НастроеноВыключение = @srvDel{index}OriginalFlag;");
            if (!enlisted)
                AppendConflictGuard(sql);
            index++;
        }
        index = 0;
        foreach (var updated in updatedServers)
        {
            parameters.Add($"@srvUpd{index}Ups", upsEquipmentId, DbType.Int32);
            parameters.Add($"@srvUpd{index}Server", updated.ServerEquipmentId, DbType.Int32);
            parameters.Add($"@srvUpd{index}Flag", updated.TurnOffConfigured, DbType.Byte);
            parameters.Add($"@srvUpd{index}OriginalFlag", originalById[updated.ServerEquipmentId].TurnOffConfigured, DbType.Byte);
            if (enlisted)
            {
                AppendLinkGuard(sql, $"srvUpd{index}", 1, $"@srvUpd{index}OriginalFlag", 2);
                AppendLinkGuard(proof, $"srvUpd{index}", 1, $"@srvUpd{index}Flag", 5);
            }
            sql.AppendLine($"    UPDATE vwПодключенияUPS SET НастроеноВыключение = @srvUpd{index}Flag WHERE КодОборудованияUPS = @srvUpd{index}Ups AND КодОборудованияСервера = @srvUpd{index}Server AND НастроеноВыключение = @srvUpd{index}OriginalFlag;");
            if (!enlisted)
                AppendConflictGuard(sql);
            index++;
        }
        index = 0;
        foreach (var added in addedServers)
        {
            parameters.Add($"@srvIns{index}Ups", upsEquipmentId, DbType.Int32);
            parameters.Add($"@srvIns{index}Server", added.ServerEquipmentId, DbType.Int32);
            parameters.Add($"@srvIns{index}Flag", added.TurnOffConfigured, DbType.Byte);
            if (enlisted)
            {
                AppendLinkGuard(sql, $"srvIns{index}", 0, null, 2);
                AppendLinkGuard(proof, $"srvIns{index}", 1, $"@srvIns{index}Flag", 5);
            }
            sql.AppendLine($"    INSERT vwПодключенияUPS (КодОборудованияUPS, КодОборудованияСервера, НастроеноВыключение) VALUES (@srvIns{index}Ups, @srvIns{index}Server, @srvIns{index}Flag);");
            index++;
        }
        index = 0;
        foreach (var updated in updatedTests)
        {
            var original = originalTestsById[updated.Id];
            var prefix = $"testUpd{index}";
            AddTestParameters(parameters, prefix, upsEquipmentId, updated);
            parameters.Add($"@{prefix}Id", updated.Id, DbType.Int32);
            parameters.Add($"@{prefix}OriginalDate", original.TestedAt, DbType.DateTime);
            parameters.Add($"@{prefix}OriginalBattery", original.BatteryTime, DbType.Int32);
            parameters.Add($"@{prefix}OriginalServers", original.Servers ?? string.Empty, DbType.String, size: 4000);
            parameters.Add($"@{prefix}OriginalIssues", original.Issues ?? string.Empty, DbType.String, size: 4000);
            if (enlisted)
            {
                AppendTestGuard(sql, prefix, $"@{prefix}Id", original: true, status: 2);
                AppendTestGuard(proof, prefix, $"@{prefix}Id", original: false, status: 5);
            }
            sql.AppendLine($"    UPDATE vwПодключенияUPSтестирование SET ДатаТестирования = @{prefix}Date, ВремяБатарейUPS = @{prefix}Battery, СервераПолучившиеОповещение = @{prefix}Servers, ОшибкиТестирования = @{prefix}Issues WHERE КодТестирования = @{prefix}Id AND КодОборудованияUPS = @{prefix}Ups AND ДатаТестирования = @{prefix}OriginalDate AND ВремяБатарейUPS = @{prefix}OriginalBattery AND ISNULL(СервераПолучившиеОповещение, '') = @{prefix}OriginalServers AND ISNULL(ОшибкиТестирования, '') = @{prefix}OriginalIssues;");
            if (!enlisted)
                AppendConflictGuard(sql);
            index++;
        }
        index = 0;
        foreach (var added in addedTests)
        {
            var prefix = $"testIns{index}";
            AddTestParameters(parameters, prefix, upsEquipmentId, added);
            if (enlisted)
                AppendTestKeysBeforeInsert(sql, prefix);
            sql.AppendLine($"    INSERT vwПодключенияUPSтестирование (КодОборудованияUPS, ДатаТестирования, ВремяБатарейUPS, СервераПолучившиеОповещение, ОшибкиТестирования) VALUES (@testIns{index}Ups, @testIns{index}Date, @testIns{index}Battery, @testIns{index}Servers, @testIns{index}Issues);");
            if (enlisted)
            {
                AppendInsertedTestKey(sql, prefix);
                AppendTestGuard(proof, prefix, $"@{prefix}NewId", original: false, status: 5);
            }
            index++;
        }
        if (enlisted)
        {
            sql.Append(proof);
            if (removedServers.All(link => link.ServerEquipmentId != sourceServerEquipmentId))
            {
                sql.AppendLine("    IF (SELECT COUNT_BIG(*) FROM vwПодключенияUPS WITH (UPDLOCK, HOLDLOCK)");
                sql.AppendLine("        WHERE КодОборудованияUPS = @accessUps AND КодОборудованияСервера = @accessSource) <> 1");
                AppendFailure(sql, 5, enlisted: true);
            }
            sql.AppendLine("    SELECT 0 AS Status, @accessSource AS ConfirmedEquipmentId;");
            return new EquipmentPhoneSaveCommand(sql.ToString(), parameters);
        }

        sql.AppendLine("    COMMIT TRANSACTION;");
        sql.AppendLine("    SELECT 0; -- сохранение выполнено");
        sql.AppendLine("END TRY");
        sql.AppendLine("BEGIN CATCH");
        sql.AppendLine("    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;");
        sql.AppendLine("    DECLARE @ErrorMessage nvarchar(4000) = ERROR_MESSAGE();");
        sql.AppendLine("    DECLARE @ErrorSeverity int = ERROR_SEVERITY();");
        sql.AppendLine("    DECLARE @ErrorState int = ERROR_STATE();");
        sql.AppendLine("    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);");
        sql.AppendLine("END CATCH;");
        return new EquipmentPhoneSaveCommand(sql.ToString(), parameters);
    }

    private static void AppendConflictGuard(StringBuilder sql)
    {
        sql.AppendLine("    IF @@ROWCOUNT <> 1");
        sql.AppendLine("    BEGIN ROLLBACK TRANSACTION; SELECT 2; RETURN; END; -- строка изменена другим пользователем");
    }

    private static void AppendApplicabilityGuard(
        StringBuilder sql,
        DynamicParameters parameters,
        IReadOnlyCollection<UpsServerLink> updatedServers,
        IReadOnlyCollection<UpsServerLink> addedServers,
        bool enlisted)
    {
        var serverIds = updatedServers
            .Concat(addedServers.Where(item => item.TurnOffConfigured > 0))
            .Select(item => item.ServerEquipmentId)
            .Distinct()
            .Order()
            .ToList();
        if (serverIds.Count == 0)
            return;

        parameters.Add("@accessComputerType", (int)ТипыОборудования.Компьютер, DbType.Int32);
        sql.AppendLine("    DECLARE @ShutdownServers TABLE (КодОборудования int NOT NULL PRIMARY KEY);");
        var index = 0;
        foreach (var serverId in serverIds)
        {
            var name = $"@accessShutdown{index++}";
            parameters.Add(name, serverId, DbType.Int32);
            sql.AppendLine($"    INSERT @ShutdownServers (КодОборудования) VALUES ({name});");
        }
        sql.AppendLine("    IF EXISTS (");
        sql.AppendLine("        SELECT 1");
        sql.AppendLine("        FROM @ShutdownServers A");
        sql.AppendLine("        LEFT JOIN vwОборудование E WITH (UPDLOCK, HOLDLOCK) ON E.КодОборудования = A.КодОборудования");
        sql.AppendLine("        LEFT JOIN МоделиОборудования M WITH (UPDLOCK, HOLDLOCK) ON E.КодМоделиОборудования = M.КодМоделиОборудования");
        sql.AppendLine("        LEFT JOIN ТипыОборудования T WITH (UPDLOCK, HOLDLOCK) ON M.КодТипаОборудования = T.КодТипаОборудования");
        sql.AppendLine("        WHERE E.КодОборудования IS NULL OR T.КодТипаОборудования IS NULL OR T.КодТипаОборудования <> @accessComputerType)");
        AppendFailure(sql, 4, enlisted);
    }

    private static void AppendAccessGuard(
        StringBuilder sql,
        DynamicParameters parameters,
        int upsEquipmentId,
        int sourceServerEquipmentId,
        IReadOnlyCollection<int> affectedServerIds,
        bool enlisted)
    {
        parameters.Add("@accessUps", upsEquipmentId, DbType.Int32);
        parameters.Add("@accessSource", sourceServerEquipmentId, DbType.Int32);
        parameters.Add("@accessRole", (int)Роли.Специалист_по_инвентаризации_оборудования, DbType.Int32);
        if (enlisted)
        {
            sql.AppendLine("    IF @accessSource <= 0 OR @accessUps <= 0");
            AppendFailure(sql, 1, enlisted: true);
        }
        sql.AppendLine("    DECLARE @AffectedServers TABLE (КодОборудования int NOT NULL PRIMARY KEY);");
        var index = 0;
        foreach (var serverId in affectedServerIds)
        {
            var name = $"@accessAffected{index++}";
            parameters.Add(name, serverId, DbType.Int32);
            sql.AppendLine($"    INSERT @AffectedServers (КодОборудования) VALUES ({name}); -- изменяемый сервер");
        }
        sql.AppendLine("    IF NOT EXISTS (");
        sql.AppendLine("        SELECT 1");
        sql.AppendLine("        FROM vwОборудование E WITH (UPDLOCK, HOLDLOCK)");
        sql.AppendLine("        INNER JOIN МоделиОборудования M WITH (UPDLOCK, HOLDLOCK) ON E.КодМоделиОборудования = M.КодМоделиОборудования");
        sql.AppendLine("        INNER JOIN ТипыОборудования T WITH (UPDLOCK, HOLDLOCK) ON M.КодТипаОборудования = T.КодТипаОборудования");
        sql.AppendLine("        WHERE E.КодОборудования = @accessSource AND T.КодТипаОборудования < 100 AND T.ЕстьХарактеристикиSIM = 0");
        sql.AppendLine("          AND EXISTS (SELECT 1 FROM fn_ТекущиеРоли() WHERE КодРоли = @accessRole AND КодЛица IN (0, E.КодЛицаВладельца)))");
        AppendFailure(sql, 1, enlisted);
        if (enlisted)
        {
            sql.AppendLine("    IF (SELECT COUNT_BIG(*) FROM vwПодключенияUPS WITH (UPDLOCK, HOLDLOCK)");
            sql.AppendLine("        WHERE КодОборудованияUPS = @accessUps AND КодОборудованияСервера = @accessSource) <> 1");
        }
        else
        {
            sql.AppendLine("    IF NOT EXISTS (SELECT 1 FROM vwПодключенияUPS WITH (UPDLOCK, HOLDLOCK)");
            sql.AppendLine("                   WHERE КодОборудованияUPS = @accessUps AND КодОборудованияСервера = @accessSource)");
        }
        AppendFailure(sql, 2, enlisted);
        sql.AppendLine("    IF EXISTS (");
        sql.AppendLine("        SELECT 1");
        sql.AppendLine("        FROM @AffectedServers A");
        sql.AppendLine("        LEFT JOIN vwОборудование E WITH (UPDLOCK, HOLDLOCK) ON E.КодОборудования = A.КодОборудования");
        sql.AppendLine("        LEFT JOIN МоделиОборудования M WITH (UPDLOCK, HOLDLOCK) ON E.КодМоделиОборудования = M.КодМоделиОборудования");
        sql.AppendLine("        LEFT JOIN ТипыОборудования T WITH (UPDLOCK, HOLDLOCK) ON M.КодТипаОборудования = T.КодТипаОборудования");
        sql.AppendLine("        WHERE E.КодОборудования IS NULL OR M.КодМоделиОборудования IS NULL OR T.КодТипаОборудования IS NULL");
        sql.AppendLine("           OR T.КодТипаОборудования >= 100 OR T.ЕстьХарактеристикиSIM <> 0");
        sql.AppendLine("           OR NOT EXISTS (SELECT 1 FROM fn_ТекущиеРоли() WHERE КодРоли = @accessRole AND КодЛица IN (0, E.КодЛицаВладельца)))");
        AppendFailure(sql, 3, enlisted);
    }

    private static void AppendFailure(StringBuilder sql, int status, bool enlisted)
    {
        if (enlisted)
            sql.AppendLine($"    BEGIN SELECT {status} AS Status, 0 AS ConfirmedEquipmentId; RETURN; END;");
        else
        {
            var comment = status switch
            {
                1 => "нет права на исходное оборудование",
                2 => "исходная связь UPS изменилась",
                3 => "нет права на изменяемый сервер",
                4 => "выключение применимо только к компьютерам",
                _ => throw new ArgumentOutOfRangeException(nameof(status))
            };
            sql.AppendLine($"    BEGIN ROLLBACK TRANSACTION; SELECT {status}; RETURN; END; -- {comment}");
        }
    }

    private static void AppendLinkGuard(
        StringBuilder sql, string prefix, int expectedCount, string? flag, int status)
    {
        var pair = $"КодОборудованияUPS = @{prefix}Ups AND КодОборудованияСервера = @{prefix}Server";
        sql.Append($"    IF (SELECT COUNT_BIG(*) FROM vwПодключенияUPS WITH (UPDLOCK, HOLDLOCK) WHERE {pair}) <> {expectedCount}");
        if (flag is not null)
            sql.Append($" OR NOT EXISTS (SELECT 1 FROM vwПодключенияUPS WITH (UPDLOCK, HOLDLOCK) WHERE {pair} AND НастроеноВыключение = {flag})");
        sql.AppendLine();
        AppendFailure(sql, status, enlisted: true);
    }

    private static void AppendTestGuard(StringBuilder sql, string prefix, string testId, bool original, int status)
    {
        var key = $"КодТестирования = {testId} AND КодОборудованияUPS = @{prefix}Ups";
        var expected = original ? prefix + "Original" : prefix;
        var values = $"""
            AND EXISTS (
                SELECT ДатаТестирования, ВремяБатарейUPS,
                       CONVERT(varbinary(max), ISNULL(CONVERT(nvarchar(max), СервераПолучившиеОповещение), N'')),
                       DATALENGTH(ISNULL(CONVERT(nvarchar(max), СервераПолучившиеОповещение), N'')),
                       CONVERT(varbinary(max), ISNULL(CONVERT(nvarchar(max), ОшибкиТестирования), N'')),
                       DATALENGTH(ISNULL(CONVERT(nvarchar(max), ОшибкиТестирования), N''))
                INTERSECT
                SELECT @{expected}Date, @{expected}Battery,
                       CONVERT(varbinary(max), CONVERT(nvarchar(max), @{expected}Servers)),
                       DATALENGTH(CONVERT(nvarchar(max), @{expected}Servers)),
                       CONVERT(varbinary(max), CONVERT(nvarchar(max), @{expected}Issues)),
                       DATALENGTH(CONVERT(nvarchar(max), @{expected}Issues))
            )
            """;
        sql.AppendLine($"    IF (SELECT COUNT_BIG(*) FROM vwПодключенияUPSтестирование WITH (UPDLOCK, HOLDLOCK) WHERE {key}) <> 1");
        sql.AppendLine($"       OR NOT EXISTS (SELECT 1 FROM vwПодключенияUPSтестирование WITH (UPDLOCK, HOLDLOCK) WHERE {key} {values})");
        AppendFailure(sql, status, enlisted: true);
    }

    private static void AppendTestKeysBeforeInsert(StringBuilder sql, string prefix)
    {
        sql.AppendLine($"""
            DECLARE @{prefix}Before TABLE (TestId int NOT NULL PRIMARY KEY);
            INSERT @{prefix}Before (TestId)
            SELECT КодТестирования FROM vwПодключенияUPSтестирование WITH (UPDLOCK, HOLDLOCK)
            WHERE КодОборудованияUPS = @{prefix}Ups;
            """);
    }

    private static void AppendInsertedTestKey(StringBuilder sql, string prefix)
    {
        sql.AppendLine($"""
            DECLARE @{prefix}NewId int;
            DECLARE @{prefix}Delta TABLE (TestId int NOT NULL);
            INSERT @{prefix}Delta (TestId)
            SELECT T.КодТестирования
            FROM vwПодключенияUPSтестирование T WITH (UPDLOCK, HOLDLOCK)
            WHERE T.КодОборудованияUPS = @{prefix}Ups
              AND NOT EXISTS (SELECT 1 FROM @{prefix}Before B WHERE B.TestId = T.КодТестирования);
            IF (SELECT COUNT(*) FROM @{prefix}Delta) <> 1
               OR EXISTS (SELECT 1 FROM @{prefix}Delta WHERE TestId <= 0)
               OR EXISTS (
                   SELECT TestId FROM @{prefix}Before
                   EXCEPT
                   SELECT КодТестирования FROM vwПодключенияUPSтестирование WITH (UPDLOCK, HOLDLOCK)
                   WHERE КодОборудованияUPS = @{prefix}Ups
               )
            """);
        AppendFailure(sql, 5, enlisted: true);
        sql.AppendLine($"    SELECT @{prefix}NewId = TestId FROM @{prefix}Delta;");
    }

    private static void AddTestParameters(DynamicParameters parameters, string prefix, int upsEquipmentId, UpsTest test)
    {
        parameters.Add($"@{prefix}Ups", upsEquipmentId, DbType.Int32);
        parameters.Add($"@{prefix}Date", test.TestedAt, DbType.DateTime);
        parameters.Add($"@{prefix}Battery", test.BatteryTime, DbType.Int32);
        parameters.Add($"@{prefix}Servers", test.Servers ?? string.Empty, DbType.String, size: 4000);
        parameters.Add($"@{prefix}Issues", test.Issues ?? string.Empty, DbType.String, size: 4000);
    }
}
