using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Оборудование для связи UPS и сервера.</summary>
public sealed class UpsEquipmentLookup : ILookupEntity
{
    /// <summary>Идентификатор оборудования.</summary>
    [Column(EquipmentColumns.КодОборудования)]
    public int Id { get; set; }

    /// <summary>Отображаемое название.</summary>
    [Column(UpsColumns.НазваниеОборудования)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Тип оборудования.</summary>
    [Column(EquipmentTypeColumns.КодТипаОборудования)]
    public int EquipmentTypeId { get; set; }

    /// <summary>Сетевое имя сервера.</summary>
    [Column(UpsColumns.НазваниеСервера)]
    public string ServerName { get; set; } = string.Empty;
}
