using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Тип оборудования и его характеристики, влияющие на поля модели.</summary>
[Table("ТипыОборудования")]
public sealed class EquipmentType : ILookupEntity
{
    /// <summary>Идентификатор типа оборудования.</summary>
    [Column(EquipmentTypeColumns.КодТипаОборудования)]
    public int Id { get; set; }

    /// <summary>Наименование типа оборудования.</summary>
    [Column(EquipmentTypeColumns.ТипОборудования)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Наименование типа на английском языке.</summary>
    [Column(EquipmentTypeColumns.ТипОборудованияЛат)]
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Признак обязательного серийного номера.</summary>
    [Column(EquipmentTypeColumns.SNобязателен)]
    public byte IsSerialNumberRequired { get; set; }

    /// <summary>Признак отсутствия точного расположения.</summary>
    [Column(EquipmentTypeColumns.БезТочногоРасположения)]
    public byte WithoutAccuratePositioning { get; set; }

    /// <summary>Признак характеристик компьютера.</summary>
    [Column(EquipmentTypeColumns.ЕстьХарактеристикиКомпьютера)]
    public byte HasComputerCharacteristics { get; set; }

    /// <summary>Признак характеристик монитора.</summary>
    [Column(EquipmentTypeColumns.ЕстьХарактеристикиМонитора)]
    public byte HasMonitorCharacteristics { get; set; }

    /// <summary>Признак характеристик SIM-карты.</summary>
    [Column(EquipmentTypeColumns.ЕстьХарактеристикиSIM)]
    public byte HasSimCharacteristics { get; set; }

    /// <summary>Признак программного обеспечения.</summary>
    [Column(EquipmentTypeColumns.ЕстьХарактеристикиSoft)]
    public byte HasSoftwareCharacteristics { get; set; }

    /// <summary>Признак наличия телефонного номера.</summary>
    [Column(EquipmentTypeColumns.ЕстьТелефонныйНомер)]
    public byte HasPhoneNumber { get; set; }

    /// <summary>Максимальное количество сетевых интерфейсов.</summary>
    [Column(EquipmentTypeColumns.ЕстьMACадрес)]
    public byte MacAddressCount { get; set; }

    /// <summary>Признак наличия сетевого имени.</summary>
    [Column(EquipmentTypeColumns.ЕстьСетевоеИмя)]
    public byte HasNetworkName { get; set; }
}
