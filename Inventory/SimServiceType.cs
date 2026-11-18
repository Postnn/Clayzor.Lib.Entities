using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Тип дополнительной услуги сотового оператора.</summary>
public sealed class SimServiceType
{
    /// <summary>Строковый идентификатор типа.</summary>
    [Column(SimServiceColumns.КодТипаДопУслугиСотОператора)]
    public string Id { get; set; } = string.Empty;

    /// <summary>Название услуги.</summary>
    [Column(SimServiceColumns.ДопУслугаСотОператора)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Стоимость включения.</summary>
    [Column(SimServiceColumns.СтоимостьВключения)]
    public decimal ActivationCost { get; set; }

    /// <summary>Стоимость в день.</summary>
    [Column(SimServiceColumns.СтоимостьВдень)]
    public decimal DailyCost { get; set; }

    /// <summary>Стоимость в месяц.</summary>
    [Column(SimServiceColumns.СтоимостьВмесяц)]
    public decimal MonthlyCost { get; set; }

    /// <summary>Признак тарифного плана.</summary>
    [Column(SimServiceColumns.ТарифныйПлан)]
    public byte IsTariffPlan { get; set; }
}
