using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Количество оборудования, связанного с моделью.</summary>
public sealed class EquipmentModelUsage
{
    /// <summary>Общее количество оборудования.</summary>
    [Column("N")]
    public int Total { get; set; }

    /// <summary>Количество списанного оборудования.</summary>
    [Column("Списанного")]
    public int WrittenOff { get; set; }
}
