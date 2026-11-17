using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Тип памяти для модели компьютерного оборудования.</summary>
[Table("ТипыПамяти")]
public sealed class MemoryType : ILookupEntity
{
    /// <summary>Идентификатор типа памяти.</summary>
    [Column(MemoryTypeColumns.КодТипаПамяти)]
    public int Id { get; set; }

    /// <summary>Наименование типа памяти.</summary>
    [Column(MemoryTypeColumns.ТипПамяти)]
    public string Name { get; set; } = string.Empty;
}
