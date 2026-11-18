using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Тестирование UPS.</summary>
public sealed class UpsTest
{
    /// <summary>Идентификатор тестирования.</summary>
    [Column(UpsColumns.КодТестирования)]
    public int Id { get; set; }

    /// <summary>Идентификатор UPS.</summary>
    [Column(UpsColumns.КодОборудованияUPS)]
    public int UpsEquipmentId { get; set; }

    /// <summary>Дата и время тестирования.</summary>
    [Column(UpsColumns.ДатаТестирования)]
    public DateTime TestedAt { get; set; }

    /// <summary>Время батарей.</summary>
    [Column(UpsColumns.ВремяБатарейUPS)]
    public int BatteryTime { get; set; }

    /// <summary>Серверы, получившие оповещение, через запятую.</summary>
    [Column(UpsColumns.СервераПолучившиеОповещение)]
    public string Servers { get; set; } = string.Empty;

    /// <summary>Ошибки тестирования.</summary>
    [Column(UpsColumns.ОшибкиТестирования)]
    public string Issues { get; set; } = string.Empty;

    /// <summary>ФИО изменившего сотрудника.</summary>
    [Column(EquipmentPeriodColumns.ИзменилФИО)]
    public string ChangedByName { get; set; } = string.Empty;

    /// <summary>Время изменения.</summary>
    [Column(EquipmentColumns.Изменено)]
    public DateTime ChangedAt { get; set; }
}
