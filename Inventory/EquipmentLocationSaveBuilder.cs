using System.Data;
using System.Text;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Строит атомарную команду расположения с проверкой expected active responsible.</summary>
public static class EquipmentLocationSaveBuilder
{
    internal static EquipmentPhoneSaveCommand BuildAdd(
        EquipmentLocationPeriod location,
        EquipmentResponsiblePeriod? responsible) =>
        BuildAdd(location, responsible, expectedActiveResponsible: false);

    /// <summary>Создаёт параметризованную SQL Server 2008 R2 команду без её выполнения.</summary>
    public static EquipmentPhoneSaveCommand BuildAdd(
        EquipmentLocationPeriod location,
        EquipmentResponsiblePeriod? responsible,
        bool expectedActiveResponsible)
    {
        Validate(location, responsible);
        var parameters = new DynamicParameters();
        parameters.Add("@КодОборудования", location.EquipmentId, DbType.Int32);
        parameters.Add("@От", location.FromUtc, DbType.DateTime);
        parameters.Add("@КодРасположения", location.LocationId, DbType.Int32);
        parameters.Add("@СрокВозврата", location.ReturnDate, DbType.DateTime);
        parameters.Add("@Примечания", location.Notes ?? string.Empty, DbType.String, size: 300);
        parameters.Add("@КодСотрудника", responsible?.EmployeeId, DbType.Int32);
        parameters.Add("@РасположениеНаРуках", (int)Расположения.На_руках, DbType.Int32);
        parameters.Add("@ОжидаетсяДействующийОтветственный", expectedActiveResponsible ? 1 : 0, DbType.Int32);
        var sql = new StringBuilder();
        sql.AppendLine("SET NOCOUNT ON;");
        sql.AppendLine("SET XACT_ABORT ON;");
        sql.AppendLine("BEGIN TRY");
        sql.AppendLine("    BEGIN TRANSACTION;");
        sql.AppendLine("    DECLARE @ДействующихОтветственных int;");
        sql.AppendLine("    SELECT @ДействующихОтветственных = COUNT(*) FROM ОборудованиеСотрудников WITH (UPDLOCK, HOLDLOCK)");
        sql.AppendLine("    WHERE КодОборудования = @КодОборудования AND До IS NULL;");
        sql.AppendLine("    IF @ДействующихОтветственных <> @ОжидаетсяДействующийОтветственный");
        sql.AppendLine("        RAISERROR('Действующий ответственный изменился. Обновите данные и повторите операцию.', 16, 1);");
        sql.AppendLine("    IF @КодРасположения = @РасположениеНаРуках AND @ДействующихОтветственных = 0 AND @КодСотрудника IS NULL");
        sql.AppendLine("       AND NOT EXISTS (SELECT 1 FROM ОборудованиеСотрудников WITH (UPDLOCK, HOLDLOCK)");
        sql.AppendLine("                       WHERE КодОборудования = @КодОборудования AND До IS NULL)");
        sql.AppendLine("        RAISERROR('Для расположения «На руках» без действующего ответственного укажите сотрудника.', 16, 1);");
        sql.AppendLine("    IF @КодРасположения = @РасположениеНаРуках AND @ДействующихОтветственных > 0 AND @КодСотрудника IS NOT NULL");
        sql.AppendLine("        RAISERROR('При действующем ответственном новая выдача из расположения не создаётся. Обновите данные.', 16, 1);");
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

    private static void Validate(EquipmentLocationPeriod location, EquipmentResponsiblePeriod? responsible)
    {
        if (location.EquipmentId <= 0 || location.LocationId <= 0)
            throw new InvalidOperationException("Не указаны оборудование или расположение.");
        if (responsible is not null &&
            (responsible.EquipmentId != location.EquipmentId || responsible.EmployeeId <= 0))
            throw new InvalidOperationException("Ответственный не соответствует оборудованию.");
    }
}
