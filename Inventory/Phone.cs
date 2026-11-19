using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Корпоративный телефонный номер: 20 редактируемых полей legacy и сведения об изменении.</summary>
[Table("ТелефонныеНомера")]
public sealed class Phone
{
    /// <summary>Идентификатор телефонного номера.</summary>
    [Column(PhoneColumns.КодТелефонногоНомера)]
    public int Id { get; set; }

    /// <summary>Короткий номер со ведущими нулями, # и *.</summary>
    [Column(PhoneColumns.ТелефонныйНомер)]
    public string Number { get; set; } = string.Empty;

    /// <summary>Полный номер, предварительно рассчитанный формой и окончательно назначаемый триггером.</summary>
    [Column(PhoneColumns.ПолныйНомер)]
    public string FullNumber { get; set; } = string.Empty;

    /// <summary>Тип телефонного номера.</summary>
    [Column(PhoneColumns.КодТипаТелефонныхНомеров)]
    public int? PhoneTypeId { get; set; }

    /// <summary>Телефонная станция.</summary>
    [Column(PhoneColumns.КодТелефоннойСтанции)]
    public int? StationId { get; set; }

    /// <summary>Порт АТС.</summary>
    [Column(PhoneColumns.ПортАТС)]
    public string AtsPort { get; set; } = string.Empty;

    /// <summary>Ведущий номер.</summary>
    [Column(PhoneColumns.ВедомыйОт)]
    public int? DrivenById { get; set; }

    /// <summary>Отображаемый номер.</summary>
    [Column(PhoneColumns.ОтображаемыйОт)]
    public int? DisplayedFromId { get; set; }

    /// <summary>Розетка.</summary>
    [Column(PhoneColumns.КодРозетки)]
    public int? SocketId { get; set; }

    /// <summary>Сотрудник.</summary>
    [Column(PhoneColumns.КодСотрудника)]
    public int? EmployeeId { get; set; }

    /// <summary>Лицо-заказчик.</summary>
    [Column(PhoneColumns.КодЛицаЗаказчика)]
    public int? CustomerId { get; set; }

    /// <summary>Текст подключения.</summary>
    [Column(PhoneColumns.Подключено)]
    public string Connected { get; set; } = string.Empty;

    /// <summary>Описание АТС латиницей.</summary>
    [Column(PhoneColumns.ОписаниеАТС)]
    public string AtsDescription { get; set; } = string.Empty;

    /// <summary>Признак основного номера сотрудника.</summary>
    [Column(PhoneColumns.ОсновнойНомер)]
    public bool IsDefault { get; set; }

    /// <summary>Срок возврата номера.</summary>
    [Column(PhoneColumns.СрокВозврата)]
    public DateTime? ReturnDate { get; set; }

    /// <summary>Услуги связи: 0, 1, 3, 4 или 5.</summary>
    [Column(PhoneColumns.УслугиСвязи)]
    public int ServicesKind { get; set; }

    /// <summary>Отображение в справочнике: 0, 1, 2, 4, 6 или 8.</summary>
    [Column(PhoneColumns.ВСправочнике)]
    public int ViewInDirectory { get; set; }

    /// <summary>Группа перехвата.</summary>
    [Column(PhoneColumns.Группа6)]
    public string PickupGroup { get; set; } = string.Empty;

    /// <summary>Переадресация.</summary>
    [Column(PhoneColumns.БыстрыйНабор)]
    public string QuickCall { get; set; } = string.Empty;

    /// <summary>Номер автонабора.</summary>
    [Column(PhoneColumns.НомерАвтонабора)]
    public string AutoDialNumber { get; set; } = string.Empty;

    /// <summary>Примечания.</summary>
    [Column(PhoneColumns.Примечания)]
    public string Notes { get; set; } = string.Empty;

    /// <summary>Идентификатор сотрудника, изменившего запись.</summary>
    [Column(PhoneColumns.Изменил)]
    public int ChangedById { get; set; }

    /// <summary>Отображаемое имя изменившего сотрудника.</summary>
    [Column(PhoneColumns.ИзменилФИО)]
    public string ChangedByName { get; set; } = string.Empty;

    /// <summary>Время последнего изменения в UTC.</summary>
    [Column(PhoneColumns.Изменено)]
    public DateTime ChangedAtUtc { get; set; }

    /// <summary>Создаёт копию простых полей без идентификатора, истории и связанных коллекций.</summary>
    public Phone CopyForNew() => new()
    {
        Number = Number,
        FullNumber = FullNumber,
        PhoneTypeId = PhoneTypeId,
        StationId = StationId,
        AtsPort = AtsPort,
        DrivenById = DrivenById,
        DisplayedFromId = DisplayedFromId,
        SocketId = SocketId,
        EmployeeId = EmployeeId,
        CustomerId = CustomerId,
        Connected = Connected,
        AtsDescription = AtsDescription,
        IsDefault = IsDefault,
        ReturnDate = ReturnDate,
        ServicesKind = ServicesKind,
        ViewInDirectory = ViewInDirectory,
        PickupGroup = PickupGroup,
        QuickCall = QuickCall,
        AutoDialNumber = AutoDialNumber,
        Notes = Notes,
    };
}
