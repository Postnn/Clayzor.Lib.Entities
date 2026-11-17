using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Лицо для справочного выбора.</summary>
[Table("vwЛица")]
public sealed class PersonLookup : ILookupEntity
{
    /// <summary>Идентификатор лица.</summary>
    [Column(PersonLookupColumns.КодЛица)]
    public int Id { get; set; }

    /// <summary>Отображаемое название лица.</summary>
    [Column(PersonLookupColumns.Кличка)]
    public string Name { get; set; } = string.Empty;
}
