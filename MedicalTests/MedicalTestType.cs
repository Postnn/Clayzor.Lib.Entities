using System.ComponentModel.DataAnnotations.Schema;
using Clayzor.Lib.DALC;

namespace Clayzor.Lib.Entities.MedicalTests;

/// <summary>
/// Тип медицинского исследования — элемент справочника МедицинскиеАнализыТипы.
/// </summary>
[Table("МедицинскиеАнализыТипы")]
public class MedicalTestType : ILookupEntity
{
    /// <summary>Идентификатор типа исследования.</summary>
    [Column(MedAT.КодТипаМедицинскогоАнализа)]
    public int Id { get; set; }

    /// <summary>Наименование типа исследования.</summary>
    [Column(MedAT.ТипМедицинскогоАнализа)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Возвращает все типы медицинских исследований из справочника.
    /// </summary>
    /// <param name="db">Менеджер подключения к БД.</param>
    /// <returns>Список типов исследований, отсортированный по наименованию.</returns>
    public static async Task<List<MedicalTestType>> GetAllAsync(DbManager db)
    {
        var result = await db.QueryAsync<MedicalTestType>(SQLQueries.SELECT_МедицинскиеАнализыТипы);
        return result.ToList();
    }
}
