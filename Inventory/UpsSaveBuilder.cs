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
        IReadOnlyCollection<UpsTest> currentTests)
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
        var affectedServerIds = removedServers.Concat(updatedServers).Concat(addedServers)
            .Select(item => item.ServerEquipmentId).Distinct().Order().ToList();
        var updatedTests = currentTests.Where(item =>
            originalTestsById.TryGetValue(item.Id, out var original) &&
            (original.TestedAt != item.TestedAt || original.BatteryTime != item.BatteryTime ||
             !string.Equals(original.Servers, item.Servers, StringComparison.Ordinal) ||
             !string.Equals(original.Issues, item.Issues, StringComparison.Ordinal))).ToList();
        var addedTests = currentTests.Where(item => item.Id == 0).ToList();

        var parameters = new DynamicParameters();
        var sql = new StringBuilder();
        sql.AppendLine("SET NOCOUNT ON;");
        sql.AppendLine("SET XACT_ABORT ON;");
        sql.AppendLine("BEGIN TRY");
        sql.AppendLine("    BEGIN TRANSACTION;");
        AppendAccessGuard(sql, parameters, upsEquipmentId, sourceServerEquipmentId, affectedServerIds);
        var index = 0;
        foreach (var removed in removedServers)
        {
            parameters.Add($"@srvDel{index}Ups", upsEquipmentId, DbType.Int32);
            parameters.Add($"@srvDel{index}Server", removed.ServerEquipmentId, DbType.Int32);
            sql.AppendLine($"    DELETE vwПодключенияUPS WHERE КодОборудованияUPS = @srvDel{index}Ups AND КодОборудованияСервера = @srvDel{index}Server;");
            index++;
        }
        index = 0;
        foreach (var updated in updatedServers)
        {
            parameters.Add($"@srvUpd{index}Ups", upsEquipmentId, DbType.Int32);
            parameters.Add($"@srvUpd{index}Server", updated.ServerEquipmentId, DbType.Int32);
            parameters.Add($"@srvUpd{index}Flag", updated.TurnOffConfigured, DbType.Byte);
            sql.AppendLine($"    UPDATE vwПодключенияUPS SET НастроеноВыключение = @srvUpd{index}Flag WHERE КодОборудованияUPS = @srvUpd{index}Ups AND КодОборудованияСервера = @srvUpd{index}Server;");
            index++;
        }
        index = 0;
        foreach (var added in addedServers)
        {
            parameters.Add($"@srvIns{index}Ups", upsEquipmentId, DbType.Int32);
            parameters.Add($"@srvIns{index}Server", added.ServerEquipmentId, DbType.Int32);
            parameters.Add($"@srvIns{index}Flag", added.TurnOffConfigured, DbType.Byte);
            sql.AppendLine($"    INSERT vwПодключенияUPS (КодОборудованияUPS, КодОборудованияСервера, НастроеноВыключение) VALUES (@srvIns{index}Ups, @srvIns{index}Server, @srvIns{index}Flag);");
            index++;
        }
        index = 0;
        foreach (var updated in updatedTests)
        {
            AddTestParameters(parameters, $"testUpd{index}", upsEquipmentId, updated);
            parameters.Add($"@testUpd{index}Id", updated.Id, DbType.Int32);
            sql.AppendLine($"    UPDATE vwПодключенияUPSтестирование SET ДатаТестирования = @testUpd{index}Date, ВремяБатарейUPS = @testUpd{index}Battery, СервераПолучившиеОповещение = @testUpd{index}Servers, ОшибкиТестирования = @testUpd{index}Issues WHERE КодТестирования = @testUpd{index}Id AND КодОборудованияUPS = @testUpd{index}Ups;");
            index++;
        }
        index = 0;
        foreach (var added in addedTests)
        {
            AddTestParameters(parameters, $"testIns{index}", upsEquipmentId, added);
            sql.AppendLine($"    INSERT vwПодключенияUPSтестирование (КодОборудованияUPS, ДатаТестирования, ВремяБатарейUPS, СервераПолучившиеОповещение, ОшибкиТестирования) VALUES (@testIns{index}Ups, @testIns{index}Date, @testIns{index}Battery, @testIns{index}Servers, @testIns{index}Issues);");
            index++;
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

    private static void AppendAccessGuard(
        StringBuilder sql,
        DynamicParameters parameters,
        int upsEquipmentId,
        int sourceServerEquipmentId,
        IReadOnlyCollection<int> affectedServerIds)
    {
        parameters.Add("@accessUps", upsEquipmentId, DbType.Int32);
        parameters.Add("@accessSource", sourceServerEquipmentId, DbType.Int32);
        parameters.Add("@accessRole", (int)Роли.Специалист_по_инвентаризации_оборудования, DbType.Int32);
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
        sql.AppendLine("    BEGIN ROLLBACK TRANSACTION; SELECT 1; RETURN; END; -- нет права на исходное оборудование");
        sql.AppendLine("    IF NOT EXISTS (SELECT 1 FROM vwПодключенияUPS WITH (UPDLOCK, HOLDLOCK)");
        sql.AppendLine("                   WHERE КодОборудованияUPS = @accessUps AND КодОборудованияСервера = @accessSource)");
        sql.AppendLine("    BEGIN ROLLBACK TRANSACTION; SELECT 2; RETURN; END; -- исходная связь UPS изменилась");
        sql.AppendLine("    IF EXISTS (");
        sql.AppendLine("        SELECT 1");
        sql.AppendLine("        FROM @AffectedServers A");
        sql.AppendLine("        LEFT JOIN vwОборудование E WITH (UPDLOCK, HOLDLOCK) ON E.КодОборудования = A.КодОборудования");
        sql.AppendLine("        LEFT JOIN МоделиОборудования M WITH (UPDLOCK, HOLDLOCK) ON E.КодМоделиОборудования = M.КодМоделиОборудования");
        sql.AppendLine("        LEFT JOIN ТипыОборудования T WITH (UPDLOCK, HOLDLOCK) ON M.КодТипаОборудования = T.КодТипаОборудования");
        sql.AppendLine("        WHERE E.КодОборудования IS NULL OR M.КодМоделиОборудования IS NULL OR T.КодТипаОборудования IS NULL");
        sql.AppendLine("           OR T.КодТипаОборудования >= 100 OR T.ЕстьХарактеристикиSIM <> 0");
        sql.AppendLine("           OR NOT EXISTS (SELECT 1 FROM fn_ТекущиеРоли() WHERE КодРоли = @accessRole AND КодЛица IN (0, E.КодЛицаВладельца)))");
        sql.AppendLine("    BEGIN ROLLBACK TRANSACTION; SELECT 3; RETURN; END; -- нет права на изменяемый сервер");
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
