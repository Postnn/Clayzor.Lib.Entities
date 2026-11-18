using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Строка инвентаризации маршрутизатора.</summary>
public sealed class EquipmentRouterInventoryItem
{
    /// <summary>Идентификатор строки.</summary>
    [Column(RouterColumns.Код)]
    public int Id { get; set; }

    /// <summary>Идентификатор оборудования.</summary>
    [Column(EquipmentColumns.КодОборудования)]
    public int EquipmentId { get; set; }

    /// <summary>Сетевое имя.</summary>
    [Column(RouterColumns.СетевоеИмя)]
    public string NetworkName { get; set; } = string.Empty;

    /// <summary>Модуль.</summary>
    [Column(RouterColumns.Модуль)]
    public string Module { get; set; } = string.Empty;

    /// <summary>Характеристика.</summary>
    [Column(RouterColumns.Характеристика)]
    public string Characteristic { get; set; } = string.Empty;
}
