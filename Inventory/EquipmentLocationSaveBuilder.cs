using System.Data;
using System.Text;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

internal static class EquipmentLocationSaveBuilder
{
    internal static EquipmentPhoneSaveCommand BuildAdd(
        EquipmentLocationPeriod location,
        EquipmentResponsiblePeriod? responsible)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@КодОборудования", location.EquipmentId, DbType.Int32);
        parameters.Add("@От", location.FromUtc, DbType.DateTime);
        parameters.Add("@КодРасположения", location.LocationId, DbType.Int32);
        parameters.Add("@СрокВозврата", location.ReturnDate, DbType.DateTime);
        parameters.Add("@Примечания", location.Notes ?? string.Empty, DbType.String, size: 300);
        if (responsible is not null)
            parameters.Add("@КодСотрудника", responsible.EmployeeId, DbType.Int32);
        var sql = new StringBuilder();
        sql.AppendLine("SET NOCOUNT ON;");
        sql.AppendLine("SET XACT_ABORT ON;");
        sql.AppendLine("BEGIN TRY");
        sql.AppendLine("    BEGIN TRANSACTION;");
        sql.AppendLine(SQLQueries.INSERT_ОборудованиеРасположение);
        if (responsible is not null)
            sql.AppendLine(SQLQueries.INSERT_ОборудованиеСотрудникаИзРасположения);
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
