using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Ресурс справочника ресурсов для элемента выбора.</summary>
[Table("Ресурсы")]
public sealed class ResourceLookup : ILookupEntity
{
    /// <summary>Идентификатор ресурса.</summary>
    [Column(ResourceColumns.КодРесурса)]
    public int Id { get; set; }

    /// <summary>Русское наименование ресурса.</summary>
    [Column(ResourceColumns.РесурсРус)]
    public string Name { get; set; } = string.Empty;
}
