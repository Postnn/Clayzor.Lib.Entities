using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Тип оборудования и его характеристики, влияющие на поля модели.</summary>
[Table("ТипыОборудования")]
public sealed class EquipmentType : ILookupEntity
{
    /// <summary>Имя ключа типа в метаданных списка моделей.</summary>
    public const string IdColumnName = EquipmentTypeColumns.КодТипаОборудования;

    /// <summary>Имя русского наименования типа в SQL.</summary>
    public const string NameColumnName = EquipmentTypeColumns.ТипОборудования;

    /// <summary>Загружает меню типов с фильтром и порядком из доверенного SQL-билдера.</summary>
    public static Task<IEnumerable<EquipmentType>> ReadMenuAsync(DbManager db, CancellationToken cancellationToken = default,
        string? gridPredicate = null, DynamicParameters? parameters = null, string? gridOrderBy = null) =>
        db.QueryRequiredAsync<EquipmentType>(string.Format(CultureInfo.InvariantCulture, SQLQueries.SELECT_ТипыОборудованияДляМеню,
            gridPredicate ?? "1=1", gridOrderBy ?? "КодТипаОборудования ASC"), parameters, cancellationToken);

    /// <summary>Возвращает название на языке сотрудника с резервным русским названием.</summary>
    public string GetMenuName(Employee employee) => employee.PrefersRussian || string.IsNullOrWhiteSpace(NameEn)
        ? Name : NameEn;

    /// <summary>Идентификатор типа оборудования.</summary>
    [Column(EquipmentTypeColumns.КодТипаОборудования)]
    public int Id { get; set; }

    /// <summary>Наименование типа оборудования.</summary>
    [Column(EquipmentTypeColumns.ТипОборудования)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Наименование типа на английском языке.</summary>
    [Column(EquipmentTypeColumns.ТипОборудованияЛат)]
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Признак обязательного серийного номера.</summary>
    [Column(EquipmentTypeColumns.SNобязателен)]
    public byte IsSerialNumberRequired { get; set; }

    /// <summary>Признак отсутствия точного расположения.</summary>
    [Column(EquipmentTypeColumns.БезТочногоРасположения)]
    public byte WithoutAccuratePositioning { get; set; }

    /// <summary>Признак характеристик компьютера.</summary>
    [Column(EquipmentTypeColumns.ЕстьХарактеристикиКомпьютера)]
    public byte HasComputerCharacteristics { get; set; }

    /// <summary>Признак характеристик монитора.</summary>
    [Column(EquipmentTypeColumns.ЕстьХарактеристикиМонитора)]
    public byte HasMonitorCharacteristics { get; set; }

    /// <summary>Признак характеристик SIM-карты.</summary>
    [Column(EquipmentTypeColumns.ЕстьХарактеристикиSIM)]
    public byte HasSimCharacteristics { get; set; }

    /// <summary>Признак программного обеспечения.</summary>
    [Column(EquipmentTypeColumns.ЕстьХарактеристикиSoft)]
    public byte HasSoftwareCharacteristics { get; set; }

    /// <summary>Признак наличия телефонного номера.</summary>
    [Column(EquipmentTypeColumns.ЕстьТелефонныйНомер)]
    public byte HasPhoneNumber { get; set; }

    /// <summary>Максимальное количество сетевых интерфейсов.</summary>
    [Column(EquipmentTypeColumns.ЕстьMACадрес)]
    public byte MacAddressCount { get; set; }

    /// <summary>Признак наличия сетевого имени.</summary>
    [Column(EquipmentTypeColumns.ЕстьСетевоеИмя)]
    public byte HasNetworkName { get; set; }
}
