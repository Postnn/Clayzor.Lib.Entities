namespace Clayzor.Lib.Entities.Tree;

/// <summary>
/// Описание источника данных дерева для слоя данных: запрос, режим иерархии, схема, сортировка.
/// </summary>
/// <remarks>
/// Дублирует часть свойств <c>ClayTreeOptions</c> намеренно. <c>ClayTreeOptions</c> живёт в
/// <c>Clayzor.Lib.Web.Controls</c>, а зависимость направлена Controls → Entities: слой данных
/// не может его видеть. Поэтому у него свой immutable-тип, а компонент собирает
/// <c>ClayTreeSource</c> из настроек в ОДНОМ месте (<c>ClaySqlTreeDataSource</c>).
/// </remarks>
public sealed record ClayTreeSource(
    string SelectSql,
    ClayTreeHierarchyMode Mode,
    ClayTreeSchema Schema,
    string? OrderBy = null,
    object? RootId = null);
