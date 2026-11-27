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
        Validate(current, next, location);
        var parameters = new DynamicParameters();
        parameters.Add("@КодОборудования", current.EquipmentId, DbType.Int32);
        parameters.Add("@От", location?.FromUtc ?? next?.FromUtc ?? current.FromUtc, DbType.DateTime);
        parameters.Add("@ТекущийОт", current.FromUtc, DbType.DateTime);
        parameters.Add("@ТекущийКодСотрудника", current.EmployeeId, DbType.Int32);
        parameters.Add("@ТекущееИзменено", current.ChangedAtUtc, DbType.DateTime);
        parameters.Add("@До", current.ToUtc, DbType.DateTime);
        parameters.Add("@СрокВозврата", next?.ReturnDate, DbType.DateTime);
        parameters.Add("@Примечания", next?.Notes ?? string.Empty, DbType.String, size: 300);
        if (next is not null)
        {
            parameters.Add("@КодСотрудника", next.EmployeeId, DbType.Int32);
            parameters.Add("@ОтСледующего", next.FromUtc, DbType.DateTime);
        }
        parameters.Add("@КодРасположения", location?.LocationId, DbType.Int32);
        parameters.Add("@РасположениеНаРуках", (int)Расположения.На_руках, DbType.Int32);

        var sql = new StringBuilder();
        sql.AppendLine("SET NOCOUNT ON;");
        sql.AppendLine("SET XACT_ABORT ON;");
        sql.AppendLine("BEGIN TRY");
        sql.AppendLine("    BEGIN TRANSACTION;");
        sql.AppendLine("    IF EXISTS (SELECT 1 FROM vwОборудование WITH (UPDLOCK, HOLDLOCK)");
        sql.AppendLine("               WHERE КодОборудования = @КодОборудования AND КодРасположения = @РасположениеНаРуках)");
        sql.AppendLine("       AND (@КодРасположения IS NULL OR @КодРасположения = @РасположениеНаРуках)");
        sql.AppendLine("        RAISERROR('Для изъятия оборудования с расположения «На руках» укажите новое расположение.', 16, 1);");
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
        sql.AppendLine("    IF @@ROWCOUNT <> 1");
        sql.AppendLine("        RAISERROR('Период ответственности был изменен или удален другим пользователем. Обновите данные и повторите операцию.', 16, 1);");
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

    internal static EquipmentPhoneSaveCommand BuildWithdrawEnlisted(
        EquipmentResponsiblePeriod current,
        EquipmentResponsiblePeriod? next,
        EquipmentLocationPeriod? location,
        IReadOnlyList<int> simIds)
    {
        Validate(current, next, location);
        ArgumentNullException.ThrowIfNull(simIds);
        if (current.ToUtc is not { } closedAt || closedAt == default)
            throw new InvalidOperationException("Не указано фактическое окончание текущего периода.");
        if (next?.EmployeeId == current.EmployeeId)
            throw new InvalidOperationException("Следующий ответственный должен отличаться от действующего.");
        if (location is null && simIds.Count != 0)
            throw new InvalidOperationException("Список перемещаемых SIM задан без нового расположения.");

        var parameters = new DynamicParameters();
        parameters.Add("@КодОборудования", current.EquipmentId, DbType.Int32);
        parameters.Add("@ТекущийОт", current.FromUtc, DbType.DateTime);
        parameters.Add("@ТекущийКодСотрудника", current.EmployeeId, DbType.Int32);
        parameters.Add("@ТекущееИзменено", current.ChangedAtUtc, DbType.DateTime);
        parameters.Add("@До", closedAt, DbType.DateTime);
        parameters.Add("@От", location?.FromUtc ?? next?.FromUtc ?? current.FromUtc, DbType.DateTime);
        parameters.Add("@КодРасположения", location?.LocationId, DbType.Int32);
        parameters.Add("@РасположениеНаРуках", (int)Расположения.На_руках, DbType.Int32);
        // При изъятии основная запись расположения получает пустые примечания и срок, как в legacy.
        parameters.Add("@СрокВозврата", null, DbType.DateTime);
        parameters.Add("@Примечания", string.Empty, DbType.String, size: 300);
        if (next is not null)
        {
            parameters.Add("@КодСотрудника", next.EmployeeId, DbType.Int32);
            parameters.Add("@ОтСледующего", next.FromUtc, DbType.DateTime);
            parameters.Add("@СрокВозвратаСледующего", next.ReturnDate, DbType.DateTime);
            parameters.Add("@ПримечанияСледующего", next.Notes ?? string.Empty, DbType.String, size: 300);
        }

        var sql = new StringBuilder();
        sql.AppendLine("""
            IF (SELECT COUNT(*) FROM ОборудованиеСотрудников WITH (UPDLOCK, HOLDLOCK)
                WHERE КодОборудования = @КодОборудования AND До IS NULL) <> 1
                OR NOT EXISTS
                (
                    SELECT 1 FROM ОборудованиеСотрудников WITH (UPDLOCK, HOLDLOCK)
                    WHERE КодОборудования = @КодОборудования AND От = @ТекущийОт
                      AND КодСотрудника = @ТекущийКодСотрудника
                      AND Изменено = @ТекущееИзменено AND До IS NULL
                )
            BEGIN
                SELECT 1 AS Status, 0 AS ConfirmedEquipmentId;
                RETURN;
            END;

            IF EXISTS (SELECT 1 FROM vwОборудование WITH (UPDLOCK, HOLDLOCK)
                       WHERE КодОборудования = @КодОборудования AND КодРасположения = @РасположениеНаРуках)
               AND (@КодРасположения IS NULL OR @КодРасположения = @РасположениеНаРуках)
            BEGIN
                SELECT 2 AS Status, 0 AS ConfirmedEquipmentId;
                RETURN;
            END;
            """);
        if (next is not null)
        {
            sql.AppendLine("""
                IF EXISTS (SELECT 1 FROM ОборудованиеСотрудников WITH (UPDLOCK, HOLDLOCK)
                           WHERE КодОборудования = @КодОборудования AND От = @ОтСледующего)
                BEGIN
                    SELECT 3 AS Status, 0 AS ConfirmedEquipmentId;
                    RETURN;
                END;
                """);
        }

        if (location is not null)
        {
            EquipmentLocationEnlistedSql.AppendPlan(sql, parameters, simIds);
            sql.AppendLine(SQLQueries.INSERT_ОборудованиеРасположениеEnlisted);
        }

        sql.AppendLine(SQLQueries.UPDATE_ОборудованиеСотрудников_Закрытие);
        sql.AppendLine("""
            IF @@ROWCOUNT <> 1
            BEGIN
                SELECT 1 AS Status, 0 AS ConfirmedEquipmentId;
                RETURN;
            END;
            """);
        if (next is not null)
        {
            sql.AppendLine("""
                SET @От = @ОтСледующего;
                SET @СрокВозврата = @СрокВозвратаСледующего;
                SET @Примечания = @ПримечанияСледующего;
                """);
            sql.AppendLine(SQLQueries.INSERT_ОборудованиеСотрудников);
            sql.AppendLine("""
                IF @@ROWCOUNT <> 1
                BEGIN
                    SELECT 3 AS Status, 0 AS ConfirmedEquipmentId;
                    RETURN;
                END;
                """);
        }

        sql.AppendLine("""
            IF (SELECT COUNT(*) FROM ОборудованиеСотрудников WITH (UPDLOCK, HOLDLOCK)
                WHERE КодОборудования = @КодОборудования AND От = @ТекущийОт
                  AND КодСотрудника = @ТекущийКодСотрудника AND До = @До) <> 1
            BEGIN
                SELECT 4 AS Status, 0 AS ConfirmedEquipmentId;
                RETURN;
            END;
            """);
        if (next is null)
        {
            sql.AppendLine("""
                IF EXISTS (SELECT 1 FROM ОборудованиеСотрудников WITH (UPDLOCK, HOLDLOCK)
                           WHERE КодОборудования = @КодОборудования AND До IS NULL)
                BEGIN
                    SELECT 4 AS Status, 0 AS ConfirmedEquipmentId;
                    RETURN;
                END;
                """);
        }
        else
        {
            sql.AppendLine("""
                IF (SELECT COUNT(*) FROM ОборудованиеСотрудников WITH (UPDLOCK, HOLDLOCK)
                    WHERE КодОборудования = @КодОборудования AND До IS NULL) <> 1
                    OR NOT EXISTS
                    (
                        SELECT 1 FROM ОборудованиеСотрудников WITH (UPDLOCK, HOLDLOCK)
                        WHERE КодОборудования = @КодОборудования AND От = @ОтСледующего
                          AND КодСотрудника = @КодСотрудника AND До IS NULL
                          AND (СрокВозврата = @СрокВозвратаСледующего
                               OR (СрокВозврата IS NULL AND @СрокВозвратаСледующего IS NULL))
                          AND DATALENGTH(ISNULL(CONVERT(nvarchar(max), Примечания), N'')) = DATALENGTH(@ПримечанияСледующего)
                          AND CONVERT(varbinary(max), ISNULL(CONVERT(nvarchar(max), Примечания), N''))
                              = CONVERT(varbinary(max), @ПримечанияСледующего)
                    )
                BEGIN
                    SELECT 4 AS Status, 0 AS ConfirmedEquipmentId;
                    RETURN;
                END;
                """);
        }

        if (location is not null)
            sql.AppendLine(SQLQueries.CHECK_ОборудованиеРасположениеEnlisted);
        sql.AppendLine("SELECT 0 AS Status, @КодОборудования AS ConfirmedEquipmentId;");
        return new EquipmentPhoneSaveCommand(sql.ToString(), parameters);
    }

    private static void Validate(
        EquipmentResponsiblePeriod current,
        EquipmentResponsiblePeriod? next,
        EquipmentLocationPeriod? location)
    {
        if (current.EquipmentId <= 0 || current.EmployeeId <= 0 || current.FromUtc == default)
            throw new InvalidOperationException("Не указаны оборудование, сотрудник или начало текущего периода.");
        if (next is not null &&
            (next.EquipmentId != current.EquipmentId || next.EmployeeId <= 0 || next.FromUtc == default))
            throw new InvalidOperationException("Следующий ответственный не соответствует оборудованию или имеет недопустимую дату выдачи.");
        if (location is not null &&
            (location.EquipmentId != current.EquipmentId || location.LocationId <= 0 || location.FromUtc == default))
            throw new InvalidOperationException("Новое расположение не соответствует оборудованию или имеет недопустимую дату.");
    }
}
