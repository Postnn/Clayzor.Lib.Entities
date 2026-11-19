using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Тип корпоративного телефонного номера.</summary>
public sealed class PhoneTypeLookup
{
    /// <summary>Идентификатор типа.</summary>
    [Column(PhoneColumns.КодТипаТелефонныхНомеров)]
    public int Id { get; set; }

    /// <summary>Название типа.</summary>
    [Column(PhoneColumns.ТипТелефонныхНомеров)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Латинское имя типа для вычисления возможностей станции.</summary>
    [Column(PhoneColumns.ТипТелефонныхНомеровЛат)]
    public string LatinName { get; set; } = string.Empty;
}

/// <summary>Телефонная станция и её возможности.</summary>
public sealed class PhoneStationLookup
{
    /// <summary>Идентификатор станции.</summary>
    [Column(PhoneColumns.КодТелефоннойСтанции)]
    public int Id { get; set; }

    /// <summary>Название станции.</summary>
    [Column(PhoneColumns.ТелефоннаяСтанция)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Оборудование станции.</summary>
    [Column(PhoneColumns.КодОборудования)]
    public int EquipmentId { get; set; }

    /// <summary>Поддержка параллельных вызовов.</summary>
    [Column(PhoneColumns.ПараллельныйВызов)]
    public bool SupportsParallelCalls { get; set; }

    /// <summary>Описание АТС заглавными буквами.</summary>
    [Column(PhoneColumns.ОписаниеАТСзаглавными)]
    public bool AtsDescriptionCapital { get; set; }

    /// <summary>Поддержка ведомых номеров.</summary>
    [Column(PhoneColumns.ВедомыеНомера)]
    public bool SupportsDrivenBy { get; set; }

    /// <summary>Поддержка подмены отображаемого номера.</summary>
    [Column(PhoneColumns.ПодменаОтображаемогоАномера)]
    public bool SupportsDisplayedFrom { get; set; }

    /// <summary>Префикс полного номера.</summary>
    [Column(PhoneColumns.ПрефиксСтанции)]
    public string Prefix { get; set; } = string.Empty;

    /// <summary>Шаблон короткого номера, для которого добавляется префикс.</summary>
    [Column(PhoneColumns.ПрефиксИспользуется)]
    public string PrefixUsed { get; set; } = string.Empty;

    /// <summary>Имя Xorcom либо пустая строка.</summary>
    [Column(PhoneColumns.XorcomName)]
    public string XorcomName { get; set; } = string.Empty;
}

/// <summary>Справочный телефонный номер для групп и зависимых связей.</summary>
public sealed class PhoneDirectoryItem
{
    /// <summary>Идентификатор номера.</summary>
    [Column(PhoneColumns.КодТелефонногоНомера)]
    public int Id { get; set; }

    /// <summary>Отображаемое имя.</summary>
    [Column(PhoneColumns.Телефон)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Короткий номер.</summary>
    [Column(PhoneColumns.ТелефонныйНомер)]
    public string Number { get; set; } = string.Empty;

    /// <summary>Полный номер.</summary>
    [Column(PhoneColumns.ПолныйНомер)]
    public string FullNumber { get; set; } = string.Empty;

    /// <summary>Идентификатор станции.</summary>
    [Column(PhoneColumns.КодТелефоннойСтанции)]
    public int StationId { get; set; }
}

/// <summary>Розетка для телефонного номера.</summary>
public sealed class PhoneSocketLookup
{
    /// <summary>Идентификатор розетки.</summary>
    [Column(PhoneColumns.КодРозетки)]
    public int Id { get; set; }

    /// <summary>Название розетки.</summary>
    [Column(PhoneColumns.Название)]
    public string Name { get; set; } = string.Empty;
}

/// <summary>Типизированный составной ключ связи телефона с оборудованием.</summary>
public sealed record PhoneEquipmentKey(int EquipmentId, int? ConnectionEquipmentId);

/// <summary>Оборудование, доступное для связывания с номером.</summary>
public sealed class PhoneEquipmentLookup
{
    /// <summary>Составной ключ оборудования и оборудования подключения.</summary>
    public PhoneEquipmentKey Key => new(EquipmentId, ConnectionEquipmentId);

    /// <summary>Идентификатор оборудования.</summary>
    [Column(PhoneColumns.КодОборудования)]
    public int EquipmentId { get; set; }

    /// <summary>Идентификатор оборудования подключения.</summary>
    [Column(PhoneColumns.КодОборудованияПодключения)]
    public int? ConnectionEquipmentId { get; set; }

    /// <summary>Отображаемое название.</summary>
    [Column(PhoneColumns.Название)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Модель оборудования.</summary>
    [Column(PhoneColumns.МодельОборудования)]
    public string Model { get; set; } = string.Empty;

    /// <summary>Серийный номер.</summary>
    [Column(PhoneColumns.SN)]
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>Текущее расположение.</summary>
    [Column(PhoneColumns.РасположениеPath)]
    public string LocationPath { get; set; } = string.Empty;

    /// <summary>Идентификатор текущего расположения.</summary>
    [Column(PhoneColumns.КодРасположения)]
    public int LocationId { get; set; }

    /// <summary>Поддерживаемый оборудованием тип телефонного номера.</summary>
    [Column(PhoneColumns.ЕстьТелефонныйНомер)]
    public int PhoneTypeId { get; set; }
}

/// <summary>Связь телефонного номера с оборудованием в черновике формы.</summary>
public sealed class PhoneEquipmentLink
{
    /// <summary>Составной ключ связи.</summary>
    public PhoneEquipmentKey Key => new(EquipmentId, ConnectionEquipmentId);

    /// <summary>Идентификатор оборудования.</summary>
    [Column(PhoneColumns.КодОборудования)]
    public int EquipmentId { get; set; }

    /// <summary>Идентификатор оборудования подключения.</summary>
    [Column(PhoneColumns.КодОборудованияПодключения)]
    public int? ConnectionEquipmentId { get; set; }

    /// <summary>Серийный номер.</summary>
    [Column(PhoneColumns.SN)]
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>Модель оборудования.</summary>
    [Column(PhoneColumns.МодельОборудования)]
    public string Model { get; set; } = string.Empty;

    /// <summary>Расположение оборудования.</summary>
    [Column(PhoneColumns.РасположениеPath)]
    public string LocationPath { get; set; } = string.Empty;

    /// <summary>Идентификатор расположения для фильтра розеток.</summary>
    [Column(PhoneColumns.КодРасположения)]
    public int LocationId { get; set; }

    /// <summary>Поддерживаемый оборудованием тип номера.</summary>
    [Column(PhoneColumns.ЕстьТелефонныйНомер)]
    public int PhoneTypeId { get; set; }

    /// <summary>Основной номер на оборудовании.</summary>
    [Column(PhoneColumns.Основной)]
    public bool IsDefault { get; set; }

    /// <summary>Срок возврата связи.</summary>
    [Column(PhoneColumns.СрокВозврата)]
    public DateTime? ReturnDate { get; set; }

    /// <summary>Примечание связи.</summary>
    [Column(PhoneColumns.Примечание)]
    public string Notes { get; set; } = string.Empty;

    /// <summary>Изменивший сотрудник.</summary>
    [Column(PhoneColumns.Изменил)]
    public string ChangedByName { get; set; } = string.Empty;

    /// <summary>Время изменения.</summary>
    [Column(PhoneColumns.Изменено)]
    public DateTime ChangedAtUtc { get; set; }

    /// <summary>Другие номера на том же оборудовании.</summary>
    public string OtherPhones { get; set; } = string.Empty;

    /// <summary>Создаёт независимую копию связи для диалогов и baseline.</summary>
    public PhoneEquipmentLink Clone() => (PhoneEquipmentLink)MemberwiseClone();
}

/// <summary>Член PBX-группы телефонных номеров.</summary>
public sealed class PhoneGroupMember
{
    /// <summary>Идентификатор группы.</summary>
    [Column(PhoneColumns.КодТелефонногоНомераГруппы)]
    public int GroupPhoneId { get; set; }

    /// <summary>Идентификатор члена группы.</summary>
    [Column(PhoneColumns.КодТелефонногоНомераЧленаГруппы)]
    public int PhoneId { get; set; }

    /// <summary>Отображаемое название.</summary>
    [Column(PhoneColumns.Название)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Изменивший сотрудник.</summary>
    [Column(PhoneColumns.Изменил)]
    public string ChangedByName { get; set; } = string.Empty;

    /// <summary>Время изменения.</summary>
    [Column(PhoneColumns.Изменено)]
    public DateTime ChangedAtUtc { get; set; }

    /// <summary>Создаёт независимую копию члена группы.</summary>
    public PhoneGroupMember Clone() => (PhoneGroupMember)MemberwiseClone();
}

/// <summary>Группа, в которую входит текущий номер.</summary>
public sealed class PhoneGroupMembership
{
    /// <summary>Идентификатор группы.</summary>
    [Column(PhoneColumns.КодТелефонногоНомераГруппы)]
    public int PhoneId { get; set; }

    /// <summary>Отображаемое название группы.</summary>
    [Column(PhoneColumns.Название)]
    public string Name { get; set; } = string.Empty;
}

/// <summary>Запись параллельного вызова.</summary>
public sealed class PhoneParallelCall
{
    /// <summary>Идентификатор исходного номера.</summary>
    [Column(PhoneColumns.КодТелефонногоНомера)]
    public int PhoneId { get; set; }

    /// <summary>Номер параллельного вызова.</summary>
    [Column(PhoneColumns.НомерТелефонаПараллельногоВызова)]
    public string CallNumber { get; set; } = string.Empty;

    /// <summary>Не использовать запись.</summary>
    [Column(PhoneColumns.НеИспользовать)]
    public bool NotUsed { get; set; }

    /// <summary>Задержка перед вызовом в секундах.</summary>
    [Column(PhoneColumns.ЗадержкаПередВызовом)]
    public int DelayBeforeCall { get; set; }

    /// <summary>Задержка слишком быстрого ответа в секундах.</summary>
    [Column(PhoneColumns.ЗадержкаСлишкомБыстрогоОтвета)]
    public int DelayTooQuickResponse { get; set; }

    /// <summary>Изменивший сотрудник.</summary>
    [Column(PhoneColumns.Изменил)]
    public string ChangedByName { get; set; } = string.Empty;

    /// <summary>Время изменения.</summary>
    [Column(PhoneColumns.Изменено)]
    public DateTime ChangedAtUtc { get; set; }

    /// <summary>Создаёт независимую копию записи.</summary>
    public PhoneParallelCall Clone() => (PhoneParallelCall)MemberwiseClone();
}
