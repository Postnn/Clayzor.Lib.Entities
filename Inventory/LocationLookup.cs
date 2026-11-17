using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Расположение для справочного выбора.</summary>
[Table("vwРасположения")]
public sealed class LocationLookup : ILookupEntity
{
    /// <summary>Идентификатор расположения.</summary>
    [Column(LocationLookupColumns.КодРасположения)]
    public int Id { get; set; }

    /// <summary>Полный путь расположения.</summary>
    [Column(LocationLookupColumns.РасположениеPath1)]
    public string Name { get; set; } = string.Empty;
}
