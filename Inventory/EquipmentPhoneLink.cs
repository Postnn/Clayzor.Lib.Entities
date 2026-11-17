using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Связь телефонного номера с оборудованием.</summary>
public sealed class EquipmentPhoneLink
{
    /// <summary>Идентификатор телефонного номера.</summary>
    [Column(PhoneColumns.КодТелефонногоНомера)]
    public int PhoneId { get; set; }

    /// <summary>Идентификатор оборудования.</summary>
    [Column(EquipmentColumns.КодОборудования)]
    public int EquipmentId { get; set; }

    /// <summary>Отображаемое описание номера.</summary>
    [Column(PhoneColumns.Описание)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Признак основного номера.</summary>
    [Column(PhoneColumns.Основной)]
    public bool IsDefault { get; set; }

    /// <summary>Оборудование подключения.</summary>
    [Column(PhoneColumns.КодОборудованияПодключения)]
    public int? ConnectionEquipmentId { get; set; }

    /// <summary>Номер автонабора.</summary>
    [Column(PhoneColumns.НомерАвтонабора)]
    public string AutoDialNumber { get; set; } = string.Empty;

    /// <summary>Задержка автонабора.</summary>
    [Column(PhoneColumns.ЗадержкаАвтонабора)]
    public byte? AutoDialDelay { get; set; }

    /// <summary>Срок возврата.</summary>
    [Column(PhoneColumns.СрокВозврата)]
    public DateTime? ReturnDate { get; set; }

    /// <summary>Примечание к сроку возврата.</summary>
    [Column(PhoneColumns.Примечание)]
    public string Notes { get; set; } = string.Empty;

    /// <summary>Отображаемое имя изменившего сотрудника.</summary>
    [Column(PhoneColumns.Изменил)]
    public string ChangedByName { get; set; } = string.Empty;

    /// <summary>Время последнего изменения.</summary>
    [Column(PhoneColumns.Изменено)]
    public DateTime ChangedAt { get; set; }
}
