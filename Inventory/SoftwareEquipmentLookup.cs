using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Программное обеспечение как связанное оборудование.</summary>
public sealed class SoftwareEquipmentLookup : ILookupEntity
{
    /// <summary>Идентификатор оборудования ПО.</summary>
    [Column(EquipmentColumns.КодОборудования)]
    public int Id { get; set; }

    /// <summary>Отображаемое название с типом и серийным номером.</summary>
    [Column(SoftwareInstallationColumns.ОтображаемоеНазвание)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Модель для поля «Установлено».</summary>
    [Column(EquipmentModelColumns.МодельОборудования)]
    public string InstalledName { get; set; } = string.Empty;
}
