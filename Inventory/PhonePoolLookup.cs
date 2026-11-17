using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Справочное значение телефонного пула.</summary>
public sealed class PhonePoolLookup
{
    /// <summary>Идентификатор телефонного пула.</summary>
    [Column(PhonePoolColumns.КодПулаТелефонов)]
    public int Id { get; set; }

    /// <summary>Название телефонного пула.</summary>
    [Column(PhonePoolColumns.ПулТелефона)]
    public string Name { get; set; } = string.Empty;
}
