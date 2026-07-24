namespace Clayzor.Lib.Entities.Tree;

/// <summary>
/// Схема колонок источника данных дерева. Описывает, какие колонки таблицы
/// соответствуют полям узла в каждом режиме иерархии.
/// </summary>
public sealed class ClayTreeSchema
{
    /// <summary>Имя колонки идентификатора узла. Обязательна в обоих режимах.</summary>
    public string IdColumn { get; set; } = "";

    /// <summary>Имя колонки текстового представления узла. Обязательна в обоих режимах.</summary>
    public string TextColumn { get; set; } = "";

    /// <summary>
    /// Имя колонки ссылки на родителя. По умолчанию <c>"Parent"</c>.
    /// Обязательна в режиме <see cref="ClayTreeHierarchyMode.ParentKey"/>.
    /// В <see cref="ClayTreeHierarchyMode.NestedSet"/> — опциональна.
    /// </summary>
    public string? ParentColumn { get; set; } = "Parent";

    /// <summary>Имя колонки левого ключа. По умолчанию <c>"L"</c>. Обязательна в режиме <see cref="ClayTreeHierarchyMode.NestedSet"/>.</summary>
    public string? LeftColumn { get; set; } = "L";

    /// <summary>Имя колонки правого ключа. По умолчанию <c>"R"</c>. Обязательна в режиме <see cref="ClayTreeHierarchyMode.NestedSet"/>.</summary>
    public string? RightColumn { get; set; } = "R";

    /// <summary>
    /// Имя колонки уровня. Может отсутствовать в источнике данных — необязательна.
    /// В режиме <see cref="ClayTreeHierarchyMode.NestedSet"/> без неё нельзя выразить
    /// «прямые дети» одним предикатом (см. валидацию). В режиме <see cref="ClayTreeHierarchyMode.ParentKey"/>
    /// уровень вычисляется в коде (родитель + 1).
    /// </summary>
    public string? LevelColumn { get; set; }

    /// <summary>
    /// Значение колонки родителя, обозначающее корень.
    /// <c>null</c> — генерируется <c>IS NULL</c>; иначе — <c>= @rootParent</c>.
    /// Используется только в режиме <see cref="ClayTreeHierarchyMode.ParentKey"/>.
    /// </summary>
    public object? RootParentValue { get; set; }

    /// <summary>Дополнительные колонки, попадающие в <c>Raw</c> строки и доступные в шаблоне узла.</summary>
    public IReadOnlyList<string> ExtraColumns { get; set; } = [];

    /// <summary>
    /// Проверяет заполненность обязательных колонок для заданного режима.
    /// Бросает <see cref="InvalidOperationException"/> с русским текстом при нарушении.
    /// </summary>
    public void Validate(ClayTreeHierarchyMode mode)
    {
        if (string.IsNullOrWhiteSpace(IdColumn))
            throw new InvalidOperationException("ClayTreeSchema.IdColumn не задана — колонка идентификатора обязательна в обоих режимах.");
        if (string.IsNullOrWhiteSpace(TextColumn))
            throw new InvalidOperationException("ClayTreeSchema.TextColumn не задана — колонка текста обязательна в обоих режимах.");

        if (mode == ClayTreeHierarchyMode.NestedSet)
        {
            if (string.IsNullOrWhiteSpace(LeftColumn))
                throw new InvalidOperationException("В режиме NestedSet обязательна ClayTreeSchema.LeftColumn (колонка левого ключа).");
            if (string.IsNullOrWhiteSpace(RightColumn))
                throw new InvalidOperationException("В режиме NestedSet обязательна ClayTreeSchema.RightColumn (колонка правого ключа).");
            // LevelColumn — необязательна: может отсутствовать в источнике данных
        }

        if (mode == ClayTreeHierarchyMode.ParentKey)
        {
            if (string.IsNullOrWhiteSpace(ParentColumn))
                throw new InvalidOperationException("В режиме ParentKey обязательна ClayTreeSchema.ParentColumn (колонка ссылки на родителя).");
        }
    }
}
