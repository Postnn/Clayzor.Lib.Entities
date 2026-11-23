using System.Data;
using System.Text;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

internal static class EquipmentPhoneSaveBuilder
{
    internal static EquipmentPhoneSaveCommand Build(
        Equipment equipment,
        IReadOnlyCollection<EquipmentPhoneLink> original,
        IReadOnlyCollection<EquipmentPhoneLink> current)
    {
        var parameters = new DynamicParameters();
        parameters.AddDynamicParams(equipment.CreateUpdateParameters());
        equipment.AddUpdateConcurrencyParameter(parameters);
        var sql = new StringBuilder();
        sql.AppendLine("SET NOCOUNT ON;");
        sql.AppendLine("SET XACT_ABORT ON;");
        sql.AppendLine("BEGIN TRY");
        sql.AppendLine("    BEGIN TRANSACTION;");
        sql.AppendLine(SQLQueries.UPDATE_Оборудование);
        sql.AppendLine("    IF @@ROWCOUNT <> 1 RAISERROR('Запись оборудования была изменена или удалена другим пользователем. Обновите данные и повторите операцию.', 16, 1);");

        var originalIds = original.Select(item => item.PhoneId).ToHashSet();
        var currentIds = current.Select(item => item.PhoneId).ToHashSet();
        if (originalIds.Count != original.Count || currentIds.Count != current.Count)
            throw new InvalidOperationException("Телефонный номер не может быть добавлен дважды.");
        var index = 0;
        foreach (var phoneId in originalIds.Where(id => !currentIds.Contains(id)).Order())
        {
            var phoneParameter = $"@delPhone{index++}";
            parameters.Add(phoneParameter, phoneId, DbType.Int32);
            sql.AppendLine(string.Format(SQLQueries.DELETE_ОборудованиеТелефона, phoneParameter));
        }

        var originalById = original.ToDictionary(item => item.PhoneId);
        index = 0;
        foreach (var item in current.Where(item => originalById.TryGetValue(item.PhoneId, out var source) &&
                     (item.ReturnDate != source.ReturnDate ||
                      (item.Notes ?? string.Empty) != (source.Notes ?? string.Empty))).OrderBy(item => item.PhoneId))
        {
            var phoneParameter = $"@updPhone{index}";
            var returnParameter = $"@updReturn{index}";
            var notesParameter = $"@updNotes{index}";
            parameters.Add(phoneParameter, item.PhoneId, DbType.Int32);
            parameters.Add(returnParameter, item.ReturnDate, DbType.DateTime);
            parameters.Add(notesParameter, item.Notes ?? string.Empty, DbType.String, size: 300);
            sql.AppendLine(string.Format(SQLQueries.UPDATE_ОборудованиеТелефонаСрокВозврата,
                phoneParameter, returnParameter, notesParameter));
            index++;
        }

        index = 0;
        foreach (var item in current.Where(item => !originalIds.Contains(item.PhoneId)).OrderBy(item => item.PhoneId))
        {
            var phoneParameter = $"@newPhone{index}";
            var connectionParameter = $"@newConnection{index}";
            var returnParameter = $"@newReturn{index}";
            var notesParameter = $"@newNotes{index}";
            parameters.Add(phoneParameter, item.PhoneId, DbType.Int32);
            parameters.Add(connectionParameter, item.ConnectionEquipmentId is 0 ? null : item.ConnectionEquipmentId, DbType.Int32);
            parameters.Add(returnParameter, item.ReturnDate, DbType.DateTime);
            parameters.Add(notesParameter, item.Notes ?? string.Empty, DbType.String, size: 300);
            sql.AppendLine(string.Format(SQLQueries.INSERT_ОборудованиеТелефона,
                phoneParameter, connectionParameter, returnParameter, notesParameter));
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
}

internal sealed record EquipmentPhoneSaveCommand(string Sql, DynamicParameters Parameters);
