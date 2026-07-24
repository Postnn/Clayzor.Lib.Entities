namespace Clayzor.Lib.Entities.Tree;

/// <summary>
/// «Сырая» строка одного уровня дерева, полученная из SQL. Маппится в UI-модель <c>ClayTreeNode</c>
/// компонентом <c>ClaySqlTreeDataSource</c>.
/// </summary>
public sealed class ClayTreeRow
{
    /// <summary>Идентификатор узла (как пришёл из БД).</summary>
    public object? Id { get; set; }

    /// <summary>Текстовое представление узла.</summary>
    public string Text { get; set; } = "";

    /// <summary>Идентификатор родителя (режим ParentKey).</summary>
    public object? ParentId { get; set; }

    /// <summary>Левый ключ вложенных множеств (режим NestedSet).</summary>
    public long? Left { get; set; }

    /// <summary>Правый ключ вложенных множеств (режим NestedSet).</summary>
    public long? Right { get; set; }

    /// <summary>Уровень вложенности (режим NestedSet).</summary>
    public int? Level { get; set; }

    /// <summary>Есть ли у узла дочерние элементы (вычислено в SQL).</summary>
    public bool HasChildren { get; set; }

    /// <summary>Дополнительные колонки (ключи без префикса <c>_</c>).</summary>
    public IReadOnlyDictionary<string, object?> Raw { get; set; } = new Dictionary<string, object?>();
}
