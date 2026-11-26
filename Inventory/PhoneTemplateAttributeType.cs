using Clayzor.Lib.DALC;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Тип подстановки телефонного шаблона и его описание из справочника.</summary>
public sealed class PhoneTemplateAttributeType
{
    /// <summary>Имя типа без префикса подстановки.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Описание типа.</summary>
    public string? Description { get; init; }

    /// <summary>Загружает уникальные типы по правилам каталога телефонных шаблонов.</summary>
    public static async Task<PhoneTemplateAttributeType[]> ReadAsync(DbManager db, CancellationToken cancellationToken) =>
        (await db.QueryRequiredAsync<PhoneTemplateAttributeType>(
            SQLQueries.SELECT_ТипыАтрибутовДляШаблона, null, cancellationToken)).ToArray();
}
