namespace Clayzor.Lib.Entities.Tree;

/// <summary>Модель хранения иерархии в источнике данных.</summary>
public enum ClayTreeHierarchyMode
{
    /// <summary>Модель вложенных множеств: левый/правый ключ + уровень.</summary>
    NestedSet = 0,

    /// <summary>Простая ссылка на родителя (adjacency list).</summary>
    ParentKey = 1,
}
