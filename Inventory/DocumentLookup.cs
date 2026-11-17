using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Документ для справочного выбора.</summary>
[Table("vwДокументы")]
public sealed class DocumentLookup : ILookupEntity
{
    /// <summary>Идентификатор документа.</summary>
    [Column(DocumentLookupColumns.КодДокумента)]
    public int Id { get; set; }

    /// <summary>Название документа.</summary>
    [Column(DocumentLookupColumns.НазваниеДокумента)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Номер документа.</summary>
    [Column(DocumentLookupColumns.НомерДокумента)]
    public string? Number { get; set; }

    /// <summary>Дата документа.</summary>
    [Column(DocumentLookupColumns.ДатаДокумента)]
    public DateTime? Date { get; set; }

    /// <summary>Составное отображаемое название документа.</summary>
    [NotMapped]
    public string DisplayName
    {
        get
        {
            var number = string.IsNullOrWhiteSpace(Number) ? string.Empty : $" №{Number}";
            var date = Date is null ? string.Empty : $" от {Date:dd.MM.yyyy}";
            return $"{Name}{number}{date}";
        }
    }
}
