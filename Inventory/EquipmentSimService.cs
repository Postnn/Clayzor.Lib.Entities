using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Дополнительная услуга SIM с включительной датой окончания в UI.</summary>
public sealed class EquipmentSimService
{
    /// <summary>Идентификатор записи.</summary>
    [Column(SimServiceColumns.КодSIMКартыДопУслугиСотОператора)]
    public int Id { get; set; }

    /// <summary>Идентификатор оборудования.</summary>
    [NotMapped]
    public int EquipmentId { get; set; }

    /// <summary>Номер телефона.</summary>
    [Column(SimCardColumns.НомерТелефона)]
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Тип услуги.</summary>
    [Column(SimServiceColumns.КодТипаДопУслугиСотОператора)]
    public string ServiceTypeId { get; set; } = string.Empty;

    /// <summary>Название услуги.</summary>
    [Column(SimServiceColumns.ДопУслугаСотОператора)]
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>Дата подключения.</summary>
    [Column(EquipmentPeriodColumns.От)]
    public DateTime? DateFrom { get; set; }

    /// <summary>Включительная дата прекращения.</summary>
    [Column(EquipmentPeriodColumns.До)]
    public DateTime? DateTo { get; set; }

    /// <summary>Стоимость включения.</summary>
    [Column(SimServiceColumns.СтоимостьВключения)]
    public decimal ActivationCost { get; set; }

    /// <summary>Стоимость в день.</summary>
    [Column(SimServiceColumns.СтоимостьВдень)]
    public decimal DailyCost { get; set; }

    /// <summary>Стоимость в месяц.</summary>
    [Column(SimServiceColumns.СтоимостьВмесяц)]
    public decimal MonthlyCost { get; set; }

    /// <summary>Плановая дата подключения; в действующем legacy SQL источник не задан.</summary>
    [Column(SimServiceColumns.ОтПлан)]
    public DateTime? PlannedDateFrom { get; set; }

    /// <summary>Включительная плановая дата отключения; в действующем legacy SQL источник не задан.</summary>
    [Column(SimServiceColumns.ДоПлан)]
    public DateTime? PlannedDateTo { get; set; }

    /// <summary>Признак обязательной услуги компании.</summary>
    [Column(SimServiceColumns.КодОбязательнойУслугиДляКомпании)]
    public bool IsCompanyRequired { get; set; }

    /// <summary>Признак услуги, которую необходимо отключить.</summary>
    [Column(SimServiceColumns.УслугуНеобходимоОтключить)]
    public byte MustBeDisabled { get; set; }

    /// <summary>Описание обязательной услуги компании.</summary>
    [Column(SimServiceColumns.ОбязательнаяУслуга)]
    public string RequiredServiceDescription { get; set; } = string.Empty;

    /// <summary>Признак тарифного плана.</summary>
    [Column(SimServiceColumns.ТарифныйПлан)]
    public byte IsTariffPlan { get; set; }

    /// <summary>Идентификатор изменившего сотрудника.</summary>
    [Column(EquipmentColumns.Изменил)]
    public int ChangedById { get; set; }

    /// <summary>ФИО изменившего сотрудника.</summary>
    [Column(EquipmentPeriodColumns.ИзменилФИО)]
    public string ChangedByName { get; set; } = string.Empty;

    /// <summary>Время изменения.</summary>
    [Column(EquipmentColumns.Изменено)]
    public DateTime ChangedAt { get; set; }

    /// <summary>Проверяет просроченное плановое подключение или отключение по правилам legacy.</summary>
    public bool HasOverduePlannedAction(DateTime today) =>
        PlannedDateFrom?.Date < today.Date || PlannedDateTo?.Date <= today.Date;

    /// <summary>Проверяет действующую блокирующую услугу с учётом включительной UI-даты окончания.</summary>
    public bool IsActiveBlock(DateTime today) =>
        ServiceTypeId == "_BLOCK" && (DateTo is null || DateTo.Value.Date >= today.Date);

    /// <summary>Возвращает предупреждение строки о требуемом отключении или просроченном плане.</summary>
    public string? GetWarning(DateTime today)
    {
        if (MustBeDisabled > 0)
            return string.IsNullOrWhiteSpace(RequiredServiceDescription)
                ? "Услугу необходимо отключить."
                : RequiredServiceDescription;
        return HasOverduePlannedAction(today) ? "Плановое действие просрочено." : null;
    }

    /// <summary>Проверяет catalog/custom-идентичность и фактические даты услуги.</summary>
    public string? Validate(DateTime today)
    {
        if (string.IsNullOrWhiteSpace(ServiceTypeId) && string.IsNullOrWhiteSpace(ServiceName))
            return "Выберите тип услуги.";
        if (string.IsNullOrWhiteSpace(ServiceName))
            return "Укажите название услуги.";
        if (DateFrom is null)
            return "Укажите фактическую дату подключения.";
        if (DateTo is not null && DateFrom.Value.Date > DateTo.Value.Date)
            return "Дата подключения не может быть позже даты прекращения.";
        if (DateFrom.Value.Date > today.Date || DateTo?.Date > today.Date)
            return "Фактические даты не могут быть в будущем.";
        return null;
    }

    /// <summary>Добавляет услугу или обновляет её по идентификатору.</summary>
    public Task SaveAsync(DbManager db) => db.ExecuteAsync(SQLQueries.UPDATE_SIMКартыДопУслугиСотОператора,
        WriteParameters(), commandType: CommandType.Text);

    internal async Task<int> SaveEnlistedAsync(DbConnection connection, DbTransaction transaction, CancellationToken ct)
    {
        var proof = await connection.QuerySingleOrDefaultAsync<SimServiceWriteProof>(new CommandDefinition(
            SQLQueries.UPDATE_SIMКартыДопУслугиСотОператораEnlisted, WriteParameters(), transaction,
            commandType: CommandType.Text, cancellationToken: ct));
        var rejection = proof?.Status switch
        {
            1 => "Привязка SIM изменилась. Обновите данные.",
            2 => "Период пересекается с существующей дополнительной услугой.",
            3 => "Дополнительная услуга изменилась. Обновите данные.",
            4 or 5 => "База не подтвердила сохранение дополнительной услуги.",
            _ => null,
        };
        if (rejection is not null) throw new EquipmentSimWriteRejectedException(rejection);
        if (proof is not { Status: 0, ConfirmedServiceId: > 0 } || Id > 0 && proof.ConfirmedServiceId != Id)
            throw new InvalidOperationException("База не подтвердила точный ключ дополнительной услуги SIM.");
        return proof.ConfirmedServiceId.Value;
    }

    private sealed class SimServiceWriteProof
    {
        /// <summary>Именованный статус сохранения дополнительной услуги.</summary>
        public int? Status { get; set; }
        /// <summary>Подтверждённый сервером код дополнительной услуги.</summary>
        public int? ConfirmedServiceId { get; set; }
    }

    private object WriteParameters() => new
    {
        КодОборудования = EquipmentId,
        НомерТелефона = PhoneNumber,
        КодSIMКартыДопУслугиСотОператора = Id == 0 ? (int?)null : Id,
        ИсходноеИзменено = Id == 0 ? (DateTime?)null : ChangedAt,
        КодТипаДопУслугиСотОператора = ServiceTypeId,
        ДопУслугаСотОператора = ServiceName,
        От = DateFrom?.Date,
        До = DateTo?.Date.AddDays(1),
        СтоимостьВключения = ActivationCost,
        СтоимостьВдень = DailyCost,
        СтоимостьВмесяц = MonthlyCost,
    };
}
