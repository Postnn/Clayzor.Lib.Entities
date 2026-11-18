using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Text.RegularExpressions;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Оборудование: создание через хранимую процедуру и редактирование основных полей.</summary>
[Table("Оборудование")]
public sealed class Equipment
{
    /// <summary>Идентификатор оборудования.</summary>
    [Column(EquipmentColumns.КодОборудования)]
    public int Id { get; set; }

    /// <summary>Идентификатор типа оборудования.</summary>
    [Column(EquipmentTypeColumns.КодТипаОборудования)]
    public int? EquipmentTypeId { get; set; }

    /// <summary>Идентификатор модели оборудования.</summary>
    [Column(EquipmentModelColumns.КодМоделиОборудования)]
    public int? ModelId { get; set; }

    /// <summary>Название модели оборудования.</summary>
    [Column(EquipmentModelColumns.МодельОборудования)]
    public string? ModelName { get; set; }

    /// <summary>Парт-номер модели.</summary>
    [Column(EquipmentModelColumns.PN)]
    public string? PartNumber { get; set; }

    /// <summary>Название типа оборудования.</summary>
    [Column(EquipmentTypeColumns.ТипОборудования)]
    public string? EquipmentTypeName { get; set; }

    /// <summary>Подключена ли камера.</summary>
    [Column(EquipmentColumns.ПодключенаКамера)]
    public byte CameraIsConnected { get; set; }

    /// <summary>Дата и время списания в UTC.</summary>
    [Column(EquipmentColumns.Списано)]
    public DateTime? WriteOffAtUtc { get; set; }

    /// <summary>Идентификатор документа списания.</summary>
    [Column(EquipmentColumns.КодДокументаСписания)]
    public int? DocumentWriteOffId { get; set; }

    /// <summary>Идентификатор телефонного пула.</summary>
    [Column(EquipmentColumns.КодПулаТелефонов)]
    public int? PhonePoolId { get; set; }

    /// <summary>Идентификатор сотрудника, изменившего запись.</summary>
    [Column(EquipmentColumns.Изменил)]
    public int ChangedById { get; set; }

    /// <summary>Дата и время последнего изменения в UTC.</summary>
    [Column(EquipmentColumns.Изменено)]
    public DateTime ChangedAtUtc { get; set; }

    /// <summary>ФИО сотрудника, изменившего запись.</summary>
    [Column(EquipmentPeriodColumns.ИзменилФИО)]
    public string? ChangedByName { get; set; }

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
    [Column(EquipmentColumns.КодДокументаПоступления)]
    public int? DocumentReceiptOuId { get; set; }

    /// <summary>Идентификатор лица-поставщика.</summary>
    [Column(EquipmentColumns.КодЛицаПоставщика)]
    public int? ShipperId { get; set; }

    /// <summary>Примечания оборудования.</summary>
    [Column(EquipmentColumns.Примечания)]
    public string? Description { get; set; }

    /// <summary>Дата и время поступления в UTC.</summary>
    public DateTime ReceivedAtUtc { get; set; }

    /// <summary>Идентификатор последнего либо начального расположения.</summary>
    [Column(LocationLookupColumns.КодРасположения)]
    public int? LocationId { get; set; }

    /// <summary>Примечание начального расположения.</summary>
    public string? LocationDescription { get; set; }

    /// <summary>Идентификатор текущего либо начального ответственного.</summary>
    [Column(EmployeeColumns.КодСотрудника)]
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

    /// <summary>Обновляет основные поля существующего оборудования по подтверждённому legacy-контракту.</summary>
    public async Task UpdateAsync(DbManager db)
    {
        if (Id <= 0)
            throw new InvalidOperationException("Не указан идентификатор редактируемого оборудования.");
        var affected = await db.ExecuteAsync(
            SQLQueries.UPDATE_Оборудование, CreateUpdateParameters(), commandType: CommandType.Text);
        if (affected != 1)
            throw new InvalidOperationException("Запись оборудования не найдена или не была обновлена.");
    }

    /// <summary>Атомарно обновляет основные поля и diff телефонных связей.</summary>
    public Task UpdateWithPhonesAsync(
        DbManager db,
        IReadOnlyCollection<EquipmentPhoneLink> original,
        IReadOnlyCollection<EquipmentPhoneLink> current)
    {
        if (Id <= 0)
            throw new InvalidOperationException("Не указан идентификатор редактируемого оборудования.");
        var command = EquipmentPhoneSaveBuilder.Build(this, original, current);
        return db.ExecuteAsync(command.Sql, command.Parameters, commandType: CommandType.Text);
    }

    internal DynamicParameters CreateUpdateParameters()
    {
        var parameters = new DynamicParameters();
        parameters.Add("@КодОборудования", Id, DbType.Int32);
        parameters.Add("@SN", SerialNumber ?? string.Empty, DbType.String, size: 50);
        parameters.Add("@ПодключенаКамера", CameraIsConnected, DbType.Byte);
        parameters.Add("@Списано", WriteOffAtUtc, DbType.DateTime);
        parameters.Add("@КодДокументаСписания", DocumentWriteOffId, DbType.Int32);
        parameters.Add("@СетевоеИмя", NetworkName ?? string.Empty, DbType.String, size: 50);
        parameters.Add("@MACадрес", MacAddress ?? string.Empty, DbType.String, size: 12);
        parameters.Add("@MACадрес2", MacAddress2 ?? string.Empty, DbType.String, size: 12);
        parameters.Add("@MACадресILO", MacAddressIlo ?? string.Empty, DbType.String, size: 12);
        parameters.Add("@Примечания", Description ?? string.Empty, DbType.String, size: 300);
        parameters.Add("@КодЛицаВладельца", OwnerId, DbType.Int32);
        parameters.Add("@КодЛицаАрендатора", TenantId, DbType.Int32);
        parameters.Add("@КодЛицаПоставщика", ShipperId, DbType.Int32);
        parameters.Add("@КодПулаТелефонов", PhonePoolId, DbType.Int32);
        parameters.Add("@КодМоделиОборудования", ModelId, DbType.Int32);
        parameters.Add("@КодДокументаПоступления", DocumentReceiptOuId, DbType.Int32);
        return parameters;
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
