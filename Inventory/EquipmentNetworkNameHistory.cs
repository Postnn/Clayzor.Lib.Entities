using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Запись истории сетевого имени оборудования.</summary>
public sealed class EquipmentNetworkNameHistory
{
    /// <summary>Идентификатор оборудования.</summary>
    [Column(EquipmentColumns.КодОборудования)]
    public int EquipmentId { get; set; }

    /// <summary>Сетевое имя.</summary>
    [Column(EquipmentColumns.СетевоеИмя)]
    public string NetworkName { get; set; } = string.Empty;

    /// <summary>Время изменения в UTC.</summary>
    [Column(EquipmentColumns.Изменено)]
    public DateTime ChangedAtUtc { get; set; }

    /// <summary>Идентификатор изменившего сотрудника.</summary>
    [Column(EquipmentColumns.Изменил)]
    public int ChangedById { get; set; }

    /// <summary>ФИО изменившего сотрудника.</summary>
    [Column(EquipmentPeriodColumns.ИзменилФИО)]
    public string ChangedByName { get; set; } = string.Empty;
}
