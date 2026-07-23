namespace Clayzor.Lib.Entities.DynamicGrid;

/// <summary>
/// Определение динамического грида, загружаемое из таблицы ClayGridSettings.
/// </summary>
/// <param name="GridId">Код запроса (первичный ключ).</param>
/// <param name="Title">Заголовок грида.</param>
/// <param name="IconUrl">URL пиктограммы (может быть null).</param>
/// <param name="Sql">SELECT-запрос источника данных.</param>
/// <param name="IdColumn">Имя колонки первичного ключа.</param>
/// <param name="IdNameColumn">Имя колонки названия (для карандаша).</param>
/// <param name="EditForm">URL формы редактирования (может быть null).</param>
/// <param name="NewForm">URL формы добавления (может быть null).</param>
/// <param name="SqlDelete">SQL DELETE с параметром @id (может быть null).</param>
/// <param name="SupportsQuickSearch">true — в таблице колонок есть УчаствуетВБыстромПоиске.</param>
public sealed record ClayGridDefinition(
    int GridId,
    string? Title,
    string? IconUrl,
    string Sql,
    string? IdColumn,
    string? IdNameColumn,
    string? EditForm,
    string? NewForm,
    string? SqlDelete,
    bool SupportsQuickSearch = false);
