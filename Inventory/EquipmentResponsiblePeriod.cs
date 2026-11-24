using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
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
    public Task InsertAsync(DbManager db) => db.ExecuteAsync(SQLQueries.INSERT_ОборудованиеСотрудников, new
    {
        КодОборудования = EquipmentId,
        От = FromUtc,
        КодСотрудника = EmployeeId,
        СрокВозврата = ReturnDate,
        Примечания = Notes ?? string.Empty,
    }, commandType: CommandType.Text);

    /// <summary>Обновляет только срок возврата и примечания существующего периода.</summary>
    public async Task UpdateAsync(DbManager db)
    {
        var affectedRows = await db.ExecuteAsync(SQLQueries.UPDATE_ОборудованиеСотрудников, new
        {
            КодОборудования = EquipmentId,
            От = FromUtc,
            ИсходноеИзменено = ChangedAtUtc,
            СрокВозврата = ReturnDate,
            Примечания = Notes ?? string.Empty,
        }, commandType: CommandType.Text);
        if (affectedRows != 1)
            throw new InvalidOperationException(
                "Период ответственности был изменён или удалён другим пользователем. Обновите данные и повторите операцию.");
    }

    /// <summary>Атомарно закрывает период, добавляет новое расположение и следующего ответственного.</summary>
    public Task WithdrawAsync(DbManager db, EquipmentResponsiblePeriod? next, EquipmentLocationPeriod? location)
    {
        var command = EquipmentResponsibleSaveBuilder.BuildWithdraw(this, next, location);
        return db.ExecuteAsync(command.Sql, command.Parameters, commandType: CommandType.Text);
    }
}
