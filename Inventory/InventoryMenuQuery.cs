namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Строка таблицы Запросы, необходимая для построения меню Inventory.</summary>
public sealed class InventoryMenuQuery
{
    /// <summary>Стабильный идентификатор запроса.</summary>
    public int Id { get; init; }

    /// <summary>Отображаемое название запроса.</summary>
    public string Caption { get; init; } = string.Empty;

    /// <summary>Тип запроса: 1 — инвентаризация, 2 — несоответствия, 3 — отчёты.</summary>
    public int QueryType { get; init; }

    /// <summary>Внешняя форма списка либо ключ конфигурации с префиксом @.</summary>
    public string ListForm { get; init; } = string.Empty;

    /// <summary>Пиктограмма из настройки запроса.</summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>Название контекстного пункта отчёта.</summary>
    public string ReportMenuItem { get; init; } = string.Empty;

    /// <summary>Ключ конфигурации формы контекстного отчёта.</summary>
    public string ReportForm { get; init; } = string.Empty;
}

/// <summary>Данные меню и выбранного запроса, загруженные из БД.</summary>
public sealed class InventoryMenuLoadResult
{
    /// <summary>Все пункты типов 1–3 в порядке ТипЗапроса, КодЗапроса.</summary>
    public IReadOnlyList<InventoryMenuQuery> Items { get; init; } = [];

    /// <summary>Текущий запрос для контекстного отчёта, если он существует.</summary>
    public InventoryMenuQuery? CurrentQuery { get; init; }
}
