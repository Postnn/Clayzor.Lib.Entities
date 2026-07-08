using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Dapper;
using Clayzor.Lib.DALC;

namespace Clayzor.Lib.Entities.MedicalTests;

/// <summary>
/// Медицинское исследование — элемент справочника анализов.
/// Содержит название, нормы для женщин и мужчин, признаки группы и заключения.
/// </summary>
[Table("МедицинскиеАнализы")]
public class MedicalTest : Entity
{
    /// <summary>Идентификатор исследования.</summary>
    [Key]
    [Column(MedA.КодМедицинскогоАнализа)]
    public override int Id { get; set; }

    /// <summary>SQL-запрос SELECT для выборки медицинских исследований с JOIN типа.</summary>
    protected override string SelectSql => SQLQueries.SELECT_МедицинскиеАнализы;

    /// <summary>SQL-запрос INSERT для добавления медицинского исследования.</summary>
    protected override string InsertSql => SQLQueries.INSERT_МедицинскиеАнализы;

    /// <summary>SQL-запрос UPDATE для обновления медицинского исследования.</summary>
    protected override string UpdateSql => SQLQueries.UPDATE_МедицинскиеАнализы;

    /// <summary>SQL-запрос DELETE для удаления медицинского исследования.</summary>
    protected override string DeleteSql => SQLQueries.DELETE_МедицинскиеАнализы;

    /// <summary>Код типа исследования из справочника МедицинскиеАнализыТипы.</summary>
    [Column(MedA.КодТипаМедицинскогоАнализа)]
    public int TestTypeId { get; set; }

    /// <summary>Наименование исследования.</summary>
    [Column(MedA.НазваниеАнализа)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Нижняя граница нормы для женщин.</summary>
    [Column(MedA.НормаОтЖенщины)]
    public double? NormMinFemale { get; set; }

    /// <summary>Верхняя граница нормы для женщин.</summary>
    [Column(MedA.НормаПоЖенщины)]
    public double? NormMaxFemale { get; set; }

    /// <summary>Нижняя граница нормы для мужчин.</summary>
    [Column(MedA.НормаОтМужчины)]
    public double? NormMinMale { get; set; }

    /// <summary>Верхняя граница нормы для мужчин.</summary>
    [Column(MedA.НормаПоМужчины)]
    public double? NormMaxMale { get; set; }

    /// <summary>Текстовая норма (вместо диапазона, например «отрицательно»).</summary>
    [Column(MedA.НормаСтрока)]
    public string? NormString { get; set; }

    /// <summary>Признак группы — используется для объединения нескольких исследований под одним заголовком.</summary>
    [Column(MedA.Группа)]
    public bool IsGroup { get; set; }

    /// <summary>Признак заключения — определяет, является ли результат исследования заключительным.</summary>
    [Column(MedA.Заключение)]
    public bool IsConclusion { get; set; }

    /// <summary>Порядок сортировки при отображении.</summary>
    [Column(MedA.Порядок)]
    public int Order { get; set; }

    /// <summary>Наименование типа исследования (не хранится в БД, заполняется JOIN'ом).</summary>
    [NotMapped]
    public string? TestTypeName { get; set; }

}

/// <summary>
/// Регистрирует соответствие свойств .NET и русских колонок БД для Dapper.
/// </summary>
public static class DapperColumnMapper
{
    private static bool _initialized;

    /// <summary>
    /// Инициализирует маппинг колонок для всех сущностей. Вызывается однократно при старте приложения.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        RegisterColumnMap<MedicalTest>();
        RegisterColumnMap<MedicalTestType>();
        _initialized = true;
    }

    private static void RegisterColumnMap<T>()
    {
        SqlMapper.SetTypeMap(typeof(T), new CustomPropertyTypeMap(
            typeof(T),
            (type, columnName) =>
            {
                return type.GetProperties().FirstOrDefault(p =>
                {
                    var attr = p.GetCustomAttributes(false)
                        .OfType<ColumnAttribute>()
                        .FirstOrDefault();
                    if (attr is not null && string.Equals(attr.Name, columnName, StringComparison.OrdinalIgnoreCase))
                        return true;
                    return string.Equals(p.Name, columnName, StringComparison.OrdinalIgnoreCase);
                })!;
            }
        ));
    }
}
