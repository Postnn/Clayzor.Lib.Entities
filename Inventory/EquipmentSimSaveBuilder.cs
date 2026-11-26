using System.Data;
using System.Text;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

internal static class EquipmentSimSaveBuilder
{
    internal static EquipmentPhoneSaveCommand BuildSave(
        EquipmentSimCard sim, EquipmentSimWriteMode mode, EquipmentSimCard? original, int? duplicateEquipmentId)
    {
        if ((mode == EquipmentSimWriteMode.Add && original is not null) ||
            (mode == EquipmentSimWriteMode.Edit && original?.EquipmentId != sim.EquipmentId))
            throw new InvalidOperationException("Исходное состояние SIM не подтверждено.");
        if (mode is not (EquipmentSimWriteMode.Add or EquipmentSimWriteMode.Edit))
            throw new ArgumentOutOfRangeException(nameof(mode));

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
        if (original is not null)
            AddOriginal(parameters, original);

        var sql = new StringBuilder();
        if (mode == EquipmentSimWriteMode.Add)
        {
            sql.AppendLine(SQLQueries.SELECT_SIMКарты_ОжидаетсяОтсутствие);
            sql.AppendLine("    BEGIN ROLLBACK TRANSACTION; SELECT 0; RETURN; END;");
        }
        if (duplicateEquipmentId is not null)
        {
            sql.AppendLine("    UPDATE vwОборудование SET Списано = @От");
            sql.AppendLine("    WHERE КодОборудования = @ДубликатОборудования AND Списано IS NULL");
            sql.AppendLine("      AND EXISTS (SELECT 1 FROM vwSIMКарты");
            sql.AppendLine("                  WHERE КодОборудования = @ДубликатОборудования AND НомерТелефона = @НомерТелефона);");
            sql.AppendLine("    IF @@ROWCOUNT <> 1");
            sql.AppendLine("    BEGIN ROLLBACK TRANSACTION; SELECT -1; RETURN; END;");
            sql.AppendLine("    SET @КодЛицаПерсональныхДанных = COALESCE(@КодЛицаПерсональныхДанных,");
            sql.AppendLine("        (SELECT КодЛицаПерсональныхДанных FROM vwSIMКарты WHERE КодОборудования = @ДубликатОборудования));");
        }
        if (mode == EquipmentSimWriteMode.Add)
            sql.AppendLine(SQLQueries.INSERT_SIMКарты);
        else
            sql.Append(SQLQueries.UPDATE_SIMКарты).AppendLine(SQLQueries.WHERE_SIMКарты_ИсходноеСостояние);
        return InTransaction(sql, parameters);
    }

    internal static EquipmentPhoneSaveCommand BuildDelete(EquipmentSimCard original)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@EquipmentId", original.EquipmentId, DbType.Int32);
        AddOriginal(parameters, original);
        var sql = new StringBuilder(SQLQueries.DELETE_ID_SIMКарты).AppendLine(SQLQueries.WHERE_SIMКарты_ИсходноеСостояние);
        return InTransaction(sql, parameters);
    }

    private static void AddOriginal(DynamicParameters parameters, EquipmentSimCard original)
    {
        parameters.Add("@ИсходныйНомер", original.PhoneNumber, DbType.String, size: 50);
        parameters.Add("@ИсходноеОт", original.ActivatedAtUtc, DbType.DateTime);
        parameters.Add("@ИсходныйДоговор", original.ContractId, DbType.Int32);
        parameters.Add("@ИсходноеЛицоПДн", original.PersonalDataPersonId, DbType.Int32);
        parameters.Add("@ИсходныйИзменил", original.ChangedById, DbType.Int32);
        parameters.Add("@ИсходноеИзменено", original.ChangedAt, DbType.DateTime);
        parameters.Add("@ИсходныйPIN", original.Secrets.Pin, DbType.String, size: 4);
        parameters.Add("@ИсходныйPIN2", original.Secrets.Pin2, DbType.String, size: 8);
        parameters.Add("@ИсходныйPUK", original.Secrets.Puk, DbType.String, size: 8);
        parameters.Add("@ИсходныйPUK2", original.Secrets.Puk2, DbType.String, size: 8);
    }

    private static EquipmentPhoneSaveCommand InTransaction(StringBuilder write, DynamicParameters parameters) => new($"""
        SET NOCOUNT ON;
        SET XACT_ABORT ON;
        BEGIN TRY
            BEGIN TRANSACTION;
        {write}
            IF @@ROWCOUNT <> 1
            BEGIN ROLLBACK TRANSACTION; SELECT 0; RETURN; END;
            COMMIT TRANSACTION;
            SELECT 1;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
            RAISERROR(N'SIM write failed.', 16, 1);
        END CATCH;
        """, parameters);
}
