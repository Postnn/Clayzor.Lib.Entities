namespace Clayzor.Lib.Entities.DynamicGrid;

/// <summary>
/// Определение колонки динамического грида, загружаемое из таблицы ClayGridColumns.
/// </summary>
/// <param name="ColumnId">Код колонки (первичный ключ).</param>
/// <param name="GridId">Код запроса, к которому относится колонка.</param>
/// <param name="Column">Имя колонки в SQL.</param>
/// <param name="Header">Заголовок колонки для отображения.</param>
/// <param name="UrlKey">Ключ для URL-параметров (фильтр, колонки).</param>
/// <param name="Order">Порядок сортировки (0/NULL — скрыта по умолчанию).</param>
/// <param name="Format">Строка формата (зависит от типа).</param>
/// <param name="Type">Тип колонки (1–13, см. ClayColumnKind).</param>
/// <param name="QuickSearch">Участвует в быстром поиске (1→true, 0/NULL/нет колонки→false).</param>
public sealed record ClayColumnDefinition(
    int ColumnId,
    int GridId,
    string Column,
    string? Header,
    string? UrlKey,
    int? Order,
    string? Format,
    int Type,
    bool QuickSearch = false);
