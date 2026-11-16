using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>
/// Модель оборудования для справочных списков и элементов выбора.
/// </summary>
[Table("МоделиОборудования")]
public sealed class EquipmentModel : ILookupEntity
{
    /// <summary>Идентификатор модели оборудования.</summary>
    [Column(EquipmentModelColumns.КодМоделиОборудования)]
    public int Id { get; set; }

    /// <summary>Наименование модели оборудования.</summary>
    [Column(EquipmentModelColumns.МодельОборудования)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Идентификатор типа, к которому относится модель.</summary>
    [Column(EquipmentModelColumns.КодТипаОборудования)]
    public int EquipmentTypeId { get; set; }
}
