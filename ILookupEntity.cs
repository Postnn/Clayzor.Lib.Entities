namespace Clayzor.Lib.Entities;

/// <summary>
/// Контракт для справочных сущностей, используемых в выпадающих списках.
/// </summary>
public interface ILookupEntity
{
    /// <summary>Идентификатор элемента справочника.</summary>
    int Id { get; }

    /// <summary>Отображаемое наименование элемента справочника.</summary>
    string Name { get; }
}
