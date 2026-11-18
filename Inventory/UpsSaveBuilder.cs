using System.Data;
using System.Text;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

internal static class UpsSaveBuilder
{
    internal static EquipmentPhoneSaveCommand Build(
        int upsEquipmentId,
        IReadOnlyCollection<UpsServerLink> originalServers,
        IReadOnlyCollection<UpsServerLink> currentServers,
        IReadOnlyCollection<UpsTest> originalTests,
        IReadOnlyCollection<UpsTest> currentTests)
    {
        var parameters = new DynamicParameters();
        var sql = new StringBuilder();
        sql.AppendLine("SET NOCOUNT ON;");
        sql.AppendLine("SET XACT_ABORT ON;");
        sql.AppendLine("BEGIN TRY");
        sql.AppendLine("    BEGIN TRANSACTION;");
        var currentIds = currentServers.Select(item => item.ServerEquipmentId).ToHashSet();
        var index = 0;
        foreach (var removed in originalServers.Where(item => !currentIds.Contains(item.ServerEquipmentId)))
        {
            parameters.Add($"@srvDel{index}Ups", upsEquipmentId, DbType.Int32);
            parameters.Add($"@srvDel{index}Server", removed.ServerEquipmentId, DbType.Int32);
            sql.AppendLine($"    DELETE vwПодключенияUPS WHERE КодОборудованияUPS = @srvDel{index}Ups AND КодОборудованияСервера = @srvDel{index}Server;");
            index++;
        }
        var originalById = originalServers.ToDictionary(item => item.ServerEquipmentId);
        index = 0;
        foreach (var updated in currentServers.Where(item => originalById.ContainsKey(item.ServerEquipmentId)))
        {
            parameters.Add($"@srvUpd{index}Ups", upsEquipmentId, DbType.Int32);
            parameters.Add($"@srvUpd{index}Server", updated.ServerEquipmentId, DbType.Int32);
            parameters.Add($"@srvUpd{index}Flag", updated.TurnOffConfigured, DbType.Byte);
            sql.AppendLine($"    UPDATE vwПодключенияUPS SET НастроеноВыключение = @srvUpd{index}Flag WHERE КодОборудованияUPS = @srvUpd{index}Ups AND КодОборудованияСервера = @srvUpd{index}Server;");
            index++;
        }
        index = 0;
        foreach (var added in currentServers.Where(item => !originalById.ContainsKey(item.ServerEquipmentId)))
        {
            parameters.Add($"@srvIns{index}Ups", upsEquipmentId, DbType.Int32);
            parameters.Add($"@srvIns{index}Server", added.ServerEquipmentId, DbType.Int32);
            parameters.Add($"@srvIns{index}Flag", added.TurnOffConfigured, DbType.Byte);
            sql.AppendLine($"    INSERT vwПодключенияUPS (КодОборудованияUPS, КодОборудованияСервера, НастроеноВыключение) VALUES (@srvIns{index}Ups, @srvIns{index}Server, @srvIns{index}Flag);");
            index++;
        }
        var originalTestsById = originalTests.ToDictionary(item => item.Id);
        index = 0;
        foreach (var updated in currentTests.Where(item => originalTestsById.ContainsKey(item.Id)))
        {
            AddTestParameters(parameters, $"testUpd{index}", upsEquipmentId, updated);
            parameters.Add($"@testUpd{index}Id", updated.Id, DbType.Int32);
            sql.AppendLine($"    UPDATE vwПодключенияUPSтестирование SET ДатаТестирования = @testUpd{index}Date, ВремяБатарейUPS = @testUpd{index}Battery, СервераПолучившиеОповещение = @testUpd{index}Servers, ОшибкиТестирования = @testUpd{index}Issues WHERE КодТестирования = @testUpd{index}Id AND КодОборудованияUPS = @testUpd{index}Ups;");
            index++;
        }
        index = 0;
        foreach (var added in currentTests.Where(item => !originalTestsById.ContainsKey(item.Id)))
        {
            AddTestParameters(parameters, $"testIns{index}", upsEquipmentId, added);
            sql.AppendLine($"    INSERT vwПодключенияUPSтестирование (КодОборудованияUPS, ДатаТестирования, ВремяБатарейUPS, СервераПолучившиеОповещение, ОшибкиТестирования) VALUES (@testIns{index}Ups, @testIns{index}Date, @testIns{index}Battery, @testIns{index}Servers, @testIns{index}Issues);");
            index++;
        }
        sql.AppendLine("    COMMIT TRANSACTION;");
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

    private static void AddTestParameters(DynamicParameters parameters, string prefix, int upsEquipmentId, UpsTest test)
    {
        parameters.Add($"@{prefix}Ups", upsEquipmentId, DbType.Int32);
        parameters.Add($"@{prefix}Date", test.TestedAt, DbType.DateTime);
        parameters.Add($"@{prefix}Battery", test.BatteryTime, DbType.Int32);
        parameters.Add($"@{prefix}Servers", test.Servers ?? string.Empty, DbType.String, size: 4000);
        parameters.Add($"@{prefix}Issues", test.Issues ?? string.Empty, DbType.String, size: 4000);
    }
}
