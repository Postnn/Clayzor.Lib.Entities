using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using Clayzor.Lib.DALC;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Период расположения оборудования.</summary>
public sealed class EquipmentLocationPeriod
{
    /// <summary>Идентификатор оборудования.</summary>
    [Column(EquipmentColumns.КодОборудования)]
    public int EquipmentId { get; set; }

    /// <summary>Начало периода в UTC.</summary>
    [Column(EquipmentPeriodColumns.От)]
    public DateTime FromUtc { get; set; }

    /// <summary>Идентификатор расположения.</summary>
    [Column(LocationLookupColumns.КодРасположения)]
    public int LocationId { get; set; }

    /// <summary>Полный путь расположения.</summary>
    [Column(LocationLookupColumns.РасположениеPath1)]
    public string LocationName { get; set; } = string.Empty;

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

    /// <summary>Добавляет расположение и, при необходимости, ответственного в одной транзакции.</summary>
    public Task InsertAsync(DbManager db, EquipmentResponsiblePeriod? responsible = null)
    {
        var command = EquipmentLocationSaveBuilder.BuildAdd(this, responsible);
        return db.ExecuteAsync(command.Sql, command.Parameters, commandType: CommandType.Text);
    }

    /// <summary>Обновляет только срок возврата и примечания существующего периода.</summary>
    public Task UpdateAsync(DbManager db) => db.ExecuteAsync(SQLQueries.UPDATE_ОборудованиеРасположение, new
    {
        КодОборудования = EquipmentId,
        От = FromUtc,
        СрокВозврата = ReturnDate,
        Примечания = Notes ?? string.Empty,
    }, commandType: CommandType.Text);
}
