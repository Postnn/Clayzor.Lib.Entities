using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>
/// Тип оборудования для справочных списков и элементов выбора.
/// </summary>
[Table("ТипыОборудования")]
public sealed class EquipmentType : ILookupEntity
{
    /// <summary>Идентификатор типа оборудования.</summary>
    [Column(EquipmentTypeColumns.КодТипаОборудования)]
    public int Id { get; set; }

    /// <summary>Наименование типа оборудования.</summary>
    [Column(EquipmentTypeColumns.ТипОборудования)]
    public string Name { get; set; } = string.Empty;
}
