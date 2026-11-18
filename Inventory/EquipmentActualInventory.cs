using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Последняя инвентаризация оборудования.</summary>
public sealed class EquipmentActualInventory
{
    /// <summary>Время проверки.</summary>
    [Column(ActualInventoryColumns.ВремяПроверки)]
    public DateTime CheckedAt { get; set; }

    /// <summary>Идентификатор оборудования.</summary>
    [Column(EquipmentColumns.КодОборудования)]
    public int EquipmentId { get; set; }

    /// <summary>Название оборудования.</summary>
    [Column(ActualInventoryColumns.Оборудование)]
    public string EquipmentName { get; set; } = string.Empty;

    /// <summary>Фактическое расположение.</summary>
    [Column(ActualInventoryColumns.КодРасположения)]
    public int LocationId { get; set; }

    /// <summary>Полный путь фактического расположения.</summary>
    [Column(LocationLookupColumns.РасположениеPath1)]
    public string LocationName { get; set; } = string.Empty;

    /// <summary>Признак неремонтопригодности.</summary>
    [Column(ActualInventoryColumns.СломаноРемонтуНеПодлежит)]
    public byte IsBeyondRepair { get; set; }

    /// <summary>Примечание.</summary>
    [Column(ActualInventoryColumns.Примечание)]
    public string Notes { get; set; } = string.Empty;

    /// <summary>Идентификатор сотрудника.</summary>
    [Column(EquipmentColumns.Изменил)]
    public int ChangedById { get; set; }

    /// <summary>ФИО сотрудника.</summary>
    [Column(EquipmentPeriodColumns.ИзменилФИО)]
    public string ChangedByName { get; set; } = string.Empty;

    /// <summary>Время изменения.</summary>
    [Column(EquipmentColumns.Изменено)]
    public DateTime ChangedAt { get; set; }
}
