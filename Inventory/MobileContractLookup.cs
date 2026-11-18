using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Незавершённый договор мобильной связи.</summary>
public sealed class MobileContractLookup : ILookupEntity
{
    /// <summary>Идентификатор документа договора.</summary>
    [Column(MobileContractColumns.КодДокумента)]
    public int Id { get; set; }

    /// <summary>Отображаемое название договора.</summary>
    [Column(MobileContractColumns.ДоговорНазвание)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Идентификатор заказчика.</summary>
    [Column(MobileContractColumns.КодЛицаЗаказчика)]
    public int? CustomerId { get; set; }

    /// <summary>Тип лица заказчика.</summary>
    [Column(MobileContractColumns.ТипЛицаЗаказчика)]
    public int CustomerType { get; set; }
}
