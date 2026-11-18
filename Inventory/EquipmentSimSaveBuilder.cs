using System.Data;
using System.Text;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

internal static class EquipmentSimSaveBuilder
{
    internal static EquipmentPhoneSaveCommand BuildSave(EquipmentSimCard sim, int? duplicateEquipmentId)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@КодОборудования", sim.EquipmentId, DbType.Int32);
        parameters.Add("@НомерТелефона", sim.PhoneNumber, DbType.String, size: 50);
        parameters.Add("@От", sim.ActivatedAtUtc, DbType.DateTime);
        parameters.Add("@КодДоговора", sim.ContractId, DbType.Int32);
        parameters.Add("@КодЛицаПерсональныхДанных", sim.PersonalDataPersonId, DbType.Int32);
        parameters.Add("@PIN", sim.Secrets.Pin ?? string.Empty, DbType.String, size: 4);
        parameters.Add("@PIN2", sim.Secrets.Pin2 ?? string.Empty, DbType.String, size: 8);
        parameters.Add("@PUK", sim.Secrets.Puk ?? string.Empty, DbType.String, size: 8);
        parameters.Add("@PUK2", sim.Secrets.Puk2 ?? string.Empty, DbType.String, size: 8);
        parameters.Add("@ДубликатОборудования", duplicateEquipmentId, DbType.Int32);

        var sql = new StringBuilder();
        sql.AppendLine("SET NOCOUNT ON;");
        sql.AppendLine("SET XACT_ABORT ON;");
        sql.AppendLine("BEGIN TRY");
        sql.AppendLine("    BEGIN TRANSACTION;");
        if (duplicateEquipmentId is not null)
        {
            sql.AppendLine("    UPDATE vwОборудование SET Списано = @От WHERE КодОборудования = @ДубликатОборудования;");
            sql.AppendLine("    SET @КодЛицаПерсональныхДанных = COALESCE(@КодЛицаПерсональныхДанных,");
            sql.AppendLine("        (SELECT КодЛицаПерсональныхДанных FROM vwSIMКарты WHERE КодОборудования = @ДубликатОборудования));");
        }
        sql.AppendLine(SQLQueries.UPDATE_SIMКарты);
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
}
