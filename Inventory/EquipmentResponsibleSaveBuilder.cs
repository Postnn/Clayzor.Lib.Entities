using System.Data;
using System.Text;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

internal static class EquipmentResponsibleSaveBuilder
{
    internal static EquipmentPhoneSaveCommand BuildWithdraw(
        EquipmentResponsiblePeriod current,
        EquipmentResponsiblePeriod? next,
        EquipmentLocationPeriod? location)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@КодОборудования", current.EquipmentId, DbType.Int32);
        parameters.Add("@От", location?.FromUtc ?? next?.FromUtc ?? current.FromUtc, DbType.DateTime);
        parameters.Add("@ТекущийОт", current.FromUtc, DbType.DateTime);
        parameters.Add("@До", current.ToUtc, DbType.DateTime);
        parameters.Add("@СрокВозврата", next?.ReturnDate, DbType.DateTime);
        parameters.Add("@Примечания", next?.Notes ?? string.Empty, DbType.String, size: 300);
        if (next is not null)
        {
            parameters.Add("@КодСотрудника", next.EmployeeId, DbType.Int32);
            parameters.Add("@ОтСледующего", next.FromUtc, DbType.DateTime);
        }
        if (location is not null)
        {
            parameters.Add("@КодРасположения", location.LocationId, DbType.Int32);
        }

        var sql = new StringBuilder();
        sql.AppendLine("SET NOCOUNT ON;");
        sql.AppendLine("SET XACT_ABORT ON;");
        sql.AppendLine("BEGIN TRY");
        sql.AppendLine("    BEGIN TRANSACTION;");
        if (location is not null)
        {
            sql.AppendLine("    DECLARE @ResponsibleReturn datetime = @СрокВозврата;");
            sql.AppendLine("    DECLARE @ResponsibleNotes nvarchar(300) = @Примечания;");
            if (next is not null)
                sql.AppendLine("    DECLARE @ResponsibleFrom datetime = @ОтСледующего;");
            sql.AppendLine("    SET @СрокВозврата = NULL;");
            sql.AppendLine("    SET @Примечания = N'';");
            sql.AppendLine(SQLQueries.INSERT_ОборудованиеРасположение);
            sql.AppendLine("    SET @СрокВозврата = @ResponsibleReturn;");
            sql.AppendLine("    SET @Примечания = @ResponsibleNotes;");
            if (next is not null)
                sql.AppendLine("    SET @От = @ResponsibleFrom;");
        }
        sql.AppendLine(SQLQueries.UPDATE_ОборудованиеСотрудников_Закрытие);
        if (next is not null)
            sql.AppendLine(SQLQueries.INSERT_ОборудованиеСотрудников);
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
