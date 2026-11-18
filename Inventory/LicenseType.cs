using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Тип лицензирования программного обеспечения.</summary>
public sealed class LicenseType : ILookupEntity
{
    /// <summary>Идентификатор типа.</summary>
    [Column(LicenseColumns.КодТипаЛицензирования)]
    public int Id { get; set; }

    /// <summary>Название с компанией.</summary>
    [Column(LicenseColumns.Наименование)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Признак временной лицензии.</summary>
    [Column(LicenseColumns.Временная)]
    public byte Temporary { get; set; }

    /// <summary>Признак перепродажи.</summary>
    [Column(LicenseColumns.Перепродажа)]
    public byte Resale { get; set; }
}
