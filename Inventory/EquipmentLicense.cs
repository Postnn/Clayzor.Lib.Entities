using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using Clayzor.Lib.DALC;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Лицензия оборудования с включительной датой окончания в UI.</summary>
public sealed class EquipmentLicense
{
    /// <summary>Идентификатор оборудования.</summary>
    [Column(EquipmentColumns.КодОборудования)]
    public int EquipmentId { get; set; }

    /// <summary>Тип лицензирования.</summary>
    [Column(LicenseColumns.КодТипаЛицензирования)]
    public int? LicenseTypeId { get; set; }

    /// <summary>Название типа лицензирования.</summary>
    [Column(LicenseColumns.ТипЛицензирования)]
    public string LicenseTypeName { get; set; } = string.Empty;

    /// <summary>Номер лицензиата.</summary>
    [Column(LicenseColumns.НомерЛицензиата)]
    public string LicenseeNumber { get; set; } = string.Empty;

    /// <summary>URL проверки лицензии.</summary>
    [Column(LicenseColumns.URLПроверкиЛицензии)]
    public string VerificationUrl { get; set; } = string.Empty;

    /// <summary>Количество серверов.</summary>
    [Column(LicenseColumns.КоличествоСерверов)]
    public int? ServerCount { get; set; }

    /// <summary>Количество клиентов.</summary>
    [Column(LicenseColumns.КоличествоКлиентов)]
    public int? ClientCount { get; set; }

    /// <summary>Начало действия.</summary>
    [Column(LicenseColumns.ДействуетОт)]
    public DateTime? DateFrom { get; set; }

    /// <summary>Включительная дата окончания.</summary>
    [Column(LicenseColumns.ДействуетДо)]
    public DateTime? DateTo { get; set; }

    /// <summary>Дистрибутив.</summary>
    [Column(LicenseColumns.Дистрибутив)]
    public string Distribution { get; set; } = string.Empty;

    /// <summary>Идентификатор изменившего сотрудника.</summary>
    [Column(EquipmentColumns.Изменил)]
    public int ChangedById { get; set; }

    /// <summary>ФИО изменившего сотрудника.</summary>
    [Column(EquipmentPeriodColumns.ИзменилФИО)]
    public string ChangedByName { get; set; } = string.Empty;

    /// <summary>Время изменения.</summary>
    [Column(EquipmentColumns.Изменено)]
    public DateTime ChangedAt { get; set; }

    /// <summary>Очищает поля, скрытые выбранным типом лицензии.</summary>
    public void ApplyTypeRules(LicenseType type)
    {
        LicenseTypeId = type.Id;
        LicenseTypeName = type.Name;
        if (type.Resale > 0)
            LicenseeNumber = string.Empty;
        if (type.Temporary == 0)
        {
            DateFrom = null;
            DateTo = null;
        }
    }

    /// <summary>Проверяет обязательные поля по типу лицензии.</summary>
    public string? Validate(LicenseType? type)
    {
        if (type is null || LicenseTypeId != type.Id)
            return "Выберите тип лицензирования.";
        if (type.Resale == 0 && string.IsNullOrWhiteSpace(LicenseeNumber))
            return "Укажите номер лицензиата.";
        if (type.Temporary > 0 && (DateFrom is null || DateTo is null))
            return "Укажите срок действия временной лицензии.";
        if (DateFrom is not null && DateTo is not null && DateFrom.Value.Date > DateTo.Value.Date)
            return "Начало действия не может быть позже окончания.";
        if (ServerCount is null)
            return "Укажите количество серверов.";
        if (ServerCount < 0)
            return "Количество серверов не может быть отрицательным.";
        if (ClientCount is null || ClientCount == 0)
            return "Количество клиентов должно быть ненулевым.";
        if (ClientCount < 0)
            return "Количество клиентов не может быть отрицательным.";
        return null;
    }

    /// <summary>Добавляет или обновляет лицензию оборудования.</summary>
    public Task SaveAsync(DbManager db) => db.ExecuteAsync(SQLQueries.UPDATE_Soft, new
    {
        КодОборудования = EquipmentId,
        КодТипаЛицензирования = LicenseTypeId,
        НомерЛицензиата = LicenseeNumber ?? string.Empty,
        URLПроверкиЛицензии = VerificationUrl ?? string.Empty,
        КоличествоСерверов = ServerCount,
        КоличествоКлиентов = ClientCount,
        ДействуетОт = DateFrom?.Date,
        ДействуетДо = DateTo?.Date.AddDays(1),
        Дистрибутив = Distribution ?? string.Empty,
    }, commandType: CommandType.Text);
}
