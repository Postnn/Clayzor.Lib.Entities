using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using Clayzor.Lib.DALC;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Период ответственности сотрудника за оборудование.</summary>
public sealed class EquipmentResponsiblePeriod
{
    /// <summary>Идентификатор оборудования.</summary>
    [Column(EquipmentColumns.КодОборудования)]
    public int EquipmentId { get; set; }

    /// <summary>Начало периода в UTC.</summary>
    [Column(EquipmentPeriodColumns.От)]
    public DateTime FromUtc { get; set; }

    /// <summary>Окончание периода в UTC.</summary>
    [Column(EquipmentPeriodColumns.До)]
    public DateTime? ToUtc { get; set; }

    /// <summary>Идентификатор сотрудника.</summary>
    [Column(EmployeeColumns.КодСотрудника)]
    public int EmployeeId { get; set; }

    /// <summary>ФИО сотрудника.</summary>
    [Column(EmployeeColumns.Сотрудник)]
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>Срок возврата.</summary>
    [Column(PhoneColumns.СрокВозврата)]
    public DateTime? ReturnDate { get; set; }

    /// <summary>Примечания.</summary>
    [Column(EquipmentPeriodColumns.Примечания)]
    public string Notes { get; set; } = string.Empty;

    /// <summary>Идентификатор изменившего сотрудника.</summary>
    [Column(EquipmentColumns.Изменил)]
    public int ChangedById { get; set; }

    /// <summary>ФИО изменившего сотрудника.</summary>
    [Column(EquipmentPeriodColumns.ИзменилФИО)]
    public string ChangedByName { get; set; } = string.Empty;

    /// <summary>Время последнего изменения в UTC.</summary>
    [Column(EquipmentColumns.Изменено)]
    public DateTime ChangedAtUtc { get; set; }

    /// <summary>Добавляет нового ответственного.</summary>
    public Task InsertAsync(DbManager db) => db.ExecuteAsync(SQLQueries.INSERT_ОборудованиеСотрудников,
        WriteParameters(FromUtc), commandType: CommandType.Text);

    /// <summary>Обновляет только срок возврата и примечания существующего периода.</summary>
    public async Task UpdateAsync(DbManager db)
    {
        var affectedRows = await db.ExecuteAsync(SQLQueries.UPDATE_ОборудованиеСотрудников,
            WriteParameters(FromUtc), commandType: CommandType.Text);
        if (affectedRows != 1)
            throw new InvalidOperationException(
                "Период ответственности был изменён или удалён другим пользователем. Обновите данные и повторите операцию.");
    }

    internal Task<int> IssueEnlistedAsync(DbConnection connection, DbTransaction transaction, DateTime fromUtc, CancellationToken ct) =>
        EquipmentPeriodWrite.ExecuteAsync(connection, transaction, SQLQueries.INSERT_ОборудованиеСотрудниковEnlisted,
            WriteParameters(fromUtc), EquipmentId, ct);

    internal Task<int> UpdateEnlistedAsync(DbConnection connection, DbTransaction transaction, DateTime fromUtc, CancellationToken ct) =>
        EquipmentPeriodWrite.ExecuteAsync(connection, transaction, SQLQueries.UPDATE_ОборудованиеСотрудниковEnlisted,
            WriteParameters(fromUtc), EquipmentId, ct);

    private object WriteParameters(DateTime fromUtc) => new
    {
        КодОборудования = EquipmentId,
        КодСотрудника = EmployeeId,
        От = fromUtc,
        ИсходноеИзменено = ChangedAtUtc,
        ИсходноеДо = ToUtc,
        СрокВозврата = ReturnDate,
        Примечания = Notes ?? string.Empty,
    };

    /// <summary>Атомарно закрывает период, добавляет новое расположение и следующего ответственного.</summary>
    public Task WithdrawAsync(DbManager db, EquipmentResponsiblePeriod? next, EquipmentLocationPeriod? location)
    {
        var command = EquipmentResponsibleSaveBuilder.BuildWithdraw(this, next, location);
        return db.ExecuteAsync(command.Sql, command.Parameters, commandType: CommandType.Text);
    }
}
