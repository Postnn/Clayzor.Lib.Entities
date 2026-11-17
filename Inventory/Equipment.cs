using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Text.RegularExpressions;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Новое оборудование, создаваемое хранимой процедурой вместе с начальными сведениями.</summary>
[Table("Оборудование")]
public sealed class Equipment
{
    /// <summary>Идентификатор созданного оборудования.</summary>
    [Column(EquipmentColumns.КодОборудования)]
    public int Id { get; set; }

    /// <summary>Идентификатор типа оборудования.</summary>
    [Column(EquipmentTypeColumns.КодТипаОборудования)]
    public int? EquipmentTypeId { get; set; }

    /// <summary>Идентификатор модели оборудования.</summary>
    [Column(EquipmentModelColumns.КодМоделиОборудования)]
    public int? ModelId { get; set; }

    /// <summary>Серийный номер.</summary>
    [Column(EquipmentColumns.SN)]
    public string? SerialNumber { get; set; }

    /// <summary>Основной MAC-адрес.</summary>
    [Column(EquipmentColumns.MACадрес)]
    public string? MacAddress { get; set; }

    /// <summary>Второй MAC-адрес.</summary>
    [Column(EquipmentColumns.MACадрес2)]
    public string? MacAddress2 { get; set; }

    /// <summary>MAC-адрес ILO.</summary>
    [Column(EquipmentColumns.MACадресILO)]
    public string? MacAddressIlo { get; set; }

    /// <summary>Сетевое имя.</summary>
    [Column(EquipmentColumns.СетевоеИмя)]
    public string? NetworkName { get; set; }

    /// <summary>Идентификатор лица-владельца.</summary>
    [Column(EquipmentColumns.КодЛицаВладельца)]
    public int? OwnerId { get; set; }

    /// <summary>Идентификатор арендатора.</summary>
    [Column(EquipmentColumns.КодЛицаАрендатора)]
    public int? TenantId { get; set; }

    /// <summary>Идентификатор документа поступления ОУ.</summary>
    public int? DocumentReceiptOuId { get; set; }

    /// <summary>Идентификатор лица-поставщика.</summary>
    [Column(EquipmentColumns.КодЛицаПоставщика)]
    public int? ShipperId { get; set; }

    /// <summary>Примечания оборудования.</summary>
    [Column(EquipmentColumns.Примечания)]
    public string? Description { get; set; }

    /// <summary>Дата и время поступления в UTC.</summary>
    public DateTime ReceivedAtUtc { get; set; }

    /// <summary>Идентификатор начального расположения.</summary>
    [Column(LocationLookupColumns.КодРасположения)]
    public int? LocationId { get; set; }

    /// <summary>Примечание начального расположения.</summary>
    public string? LocationDescription { get; set; }

    /// <summary>Идентификатор начального ответственного.</summary>
    public int? EmployeeId { get; set; }

    /// <summary>Примечание начального ответственного.</summary>
    public string? EmployeeDescription { get; set; }

    /// <summary>Принять оборудование к бухгалтерскому учёту.</summary>
    public bool IsAccounting { get; set; }

    /// <summary>Инвентарный номер.</summary>
    public string? InventoryNumber { get; set; }

    /// <summary>Идентификатор документа бухгалтерского поступления.</summary>
    public int? AccountingDocumentId { get; set; }

    /// <summary>Примечание бухгалтерского учёта.</summary>
    public string? AccountingDescription { get; set; }

    /// <summary>Удаляет разрешённые разделители из MAC-адреса и приводит его к верхнему регистру.</summary>
    public static string NormalizeMacAddress(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value, @"[\s:\-.]", string.Empty).ToUpperInvariant();

    /// <summary>Проверяет, что MAC-адрес пуст либо содержит 12 hex-символов и только разрешённые разделители.</summary>
    public static bool IsValidMacAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;
        if (!Regex.IsMatch(value, @"^[0-9A-Fa-f\s:.\-]+$"))
            return false;
        return Regex.IsMatch(NormalizeMacAddress(value), "^[0-9A-F]{12}$");
    }

    /// <summary>Создаёт оборудование через sp_Оборудование_Ins и возвращает output-идентификатор.</summary>
    public async Task<int> InsertAsync(DbManager db)
    {
        var parameters = CreateInsertParameters();
        await db.ExecuteAsync(SQLQueries.SP_Оборудование_Ins, parameters);
        Id = parameters.Get<int>("@КодОборудования");
        if (Id <= 0)
            throw new InvalidOperationException("Хранимая процедура не вернула идентификатор созданного оборудования.");
        return Id;
    }

    internal DynamicParameters CreateInsertParameters()
    {
        var parameters = new DynamicParameters();
        parameters.Add("@SN", SerialNumber ?? string.Empty, DbType.String, size: 50);
        parameters.Add("@КодМоделиОборудования", ModelId, DbType.Int32);
        parameters.Add("@MACадрес", MacAddress ?? string.Empty, DbType.String, size: 12);
        parameters.Add("@MACадрес2", MacAddress2 ?? string.Empty, DbType.String, size: 12);
        parameters.Add("@MACадресILO", MacAddressIlo ?? string.Empty, DbType.String, size: 12);
        parameters.Add("@СетевоеИмя", NetworkName ?? string.Empty, DbType.String, size: 50);
        parameters.Add("@КодЛицаВладельца", OwnerId, DbType.Int32);
        parameters.Add("@КодЛицаАрендатора", TenantId, DbType.Int32);
        parameters.Add("@КодДокументаПоступленияОУ", DocumentReceiptOuId, DbType.Int32);
        parameters.Add("@КодЛицаПоставщика", ShipperId, DbType.Int32);
        parameters.Add("@Примечания", Description ?? string.Empty, DbType.String, size: 300);
        parameters.Add("@От", ReceivedAtUtc, DbType.DateTime);
        parameters.Add("@КодРасположения", LocationId, DbType.Int32);
        parameters.Add("@ПримечанияРасположения", LocationDescription ?? string.Empty, DbType.String, size: 300);
        parameters.Add("@КодСотрудника", EmployeeId, DbType.Int32);
        parameters.Add("@ПримечанияСотрудника", EmployeeDescription ?? string.Empty, DbType.String, size: 300);
        parameters.Add("@ПринятьКБухгалтерскомуУчету", IsAccounting ? 1 : 0, DbType.Byte);
        parameters.Add("@ИнвентарныйНомер", InventoryNumber ?? string.Empty, DbType.String, size: 50);
        parameters.Add("@КодДокументаПоступления", AccountingDocumentId, DbType.Int32);
        parameters.Add("@ПримечанияБух", AccountingDescription ?? string.Empty, DbType.String, size: 300);
        parameters.Add("@КодОборудования", dbType: DbType.Int32, direction: ParameterDirection.Output);
        return parameters;
    }
}
