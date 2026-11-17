using System.Data;
using System.ComponentModel.DataAnnotations.Schema;
using Clayzor.Lib.DALC;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Модель оборудования для справочных списков и формы редактирования.</summary>
[Table("МоделиОборудования")]
public sealed class EquipmentModel : ILookupEntity
{
    /// <summary>Идентификатор модели оборудования.</summary>
    [Column(EquipmentModelColumns.КодМоделиОборудования)]
    public int Id { get; set; }

    /// <summary>Наименование модели оборудования.</summary>
    [Column(EquipmentModelColumns.МодельОборудования)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Идентификатор типа, к которому относится модель.</summary>
    [Column(EquipmentModelColumns.КодТипаОборудования)]
    public int EquipmentTypeId { get; set; }

    /// <summary>Идентификатор связанного ресурса.</summary>
    [Column(EquipmentModelColumns.КодРесурса)]
    public int? ResourceId { get; set; }

    /// <summary>Part Number производителя.</summary>
    [Column(EquipmentModelColumns.PN)]
    public string PartNumber { get; set; } = string.Empty;

    /// <summary>Название процессора.</summary>
    [Column(EquipmentModelColumns.CPU)]
    public string Cpu { get; set; } = string.Empty;

    /// <summary>Диагональ монитора; 0 отображается как пустое значение.</summary>
    [Column(EquipmentModelColumns.РазмерМонитора)]
    public double MonitorSize { get; set; }

    /// <summary>Идентификатор типа памяти.</summary>
    [Column(EquipmentModelColumns.КодТипаПамяти)]
    public int MemoryTypeId { get; set; }

    /// <summary>Количество банков памяти.</summary>
    [Column(EquipmentModelColumns.БанковПамяти)]
    public int MemoryBanks { get; set; }

    /// <summary>Признак мультимедийного оборудования.</summary>
    [Column(EquipmentModelColumns.Мультимедиа)]
    public bool IsMultimedia { get; set; }

    /// <summary>Количество сетевых интерфейсов.</summary>
    [Column(EquipmentModelColumns.Net)]
    public int NetworkInterfaces { get; set; }

    /// <summary>Название сетевого интерфейса.</summary>
    [Column(EquipmentModelColumns.НазваниеNet)]
    public string NetworkName { get; set; } = string.Empty;

    /// <summary>Сведения о драйвере.</summary>
    [Column(EquipmentModelColumns.Драйвер)]
    public string Driver { get; set; } = string.Empty;

    /// <summary>Срок службы в свободном текстовом формате.</summary>
    [Column(EquipmentModelColumns.Ресурс)]
    public string LifeTime { get; set; } = string.Empty;

    /// <summary>Шаблон настройки IP-телефона.</summary>
    [Column(EquipmentModelColumns.ШаблонТелефона)]
    public string PhoneTemplate { get; set; } = string.Empty;

    /// <summary>Примечания к модели.</summary>
    [Column(EquipmentModelColumns.Примечания)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Идентификатор сотрудника, изменившего запись.</summary>
    [Column(EquipmentModelColumns.Изменил)]
    public int? ChangedById { get; set; }

    /// <summary>Дата последнего изменения записи.</summary>
    [Column(EquipmentModelColumns.Изменено)]
    public DateTime? ChangedAt { get; set; }

    /// <summary>Имя сотрудника, изменившего запись.</summary>
    [NotMapped]
    public string? ChangedByName { get; set; }

    /// <summary>Загружает полную модель для редактирования.</summary>
    public static async Task<EquipmentModel?> GetForEditAsync(DbManager db, int id) =>
        (await db.QueryAsync<EquipmentModel>(SQLQueries.SELECT_МодельОборудованияДляРедактирования, new { Id = id }))
            .SingleOrDefault();

    /// <summary>Проверяет уникальность пары «модель + PN» с исключением текущей записи.</summary>
    public static async Task<bool> HasDuplicateAsync(DbManager db, EquipmentModel model) =>
        await db.ExecuteScalarAsync<int>(SQLQueries.SELECT_МодельОборудования_EXISTS, new
        {
            Id = model.Id,
            model.Name,
            model.PartNumber,
        }, commandType: CommandType.Text) == 1;

    /// <summary>Возвращает количество оборудования модели, включая списанное.</summary>
    public static async Task<EquipmentModelUsage> GetUsageAsync(DbManager db, int id, int? newEquipmentTypeId = null)
    {
        var sql = newEquipmentTypeId is null
            ? SQLQueries.SELECT_ID_КоличествоОборудованияМодели
            : SQLQueries.SELECT_ID_КоличествоОборудованияМоделиДругогоТипа;
        return (await db.QueryAsync<EquipmentModelUsage>(sql, new { Id = id, EquipmentTypeId = newEquipmentTypeId }))
                   .SingleOrDefault()
               ?? new EquipmentModelUsage();
    }

    /// <summary>Создаёт или обновляет модель и возвращает её идентификатор.</summary>
    public async Task<int> SaveAsync(DbManager db)
    {
        if (Id == 0)
        {
            Id = await db.ExecuteWriteScalarAsync<int>(SQLQueries.INSERT_МоделиОборудования, SaveParameters());
            if (Id <= 0)
                throw new InvalidOperationException("База данных не вернула идентификатор созданной модели.");
        }
        else
            await db.ExecuteAsync(SQLQueries.UPDATE_МоделиОборудования, SaveParameters(), commandType: CommandType.Text);
        return Id;
    }

    /// <summary>Сохраняет только шаблон телефона существующей модели.</summary>
    public async Task SavePhoneTemplateAsync(DbManager db) =>
        await db.ExecuteAsync(
            SQLQueries.UPDATE_МоделиОборудования_ШаблонТелефона,
            new { Id, PhoneTemplate },
            commandType: CommandType.Text);

    private object SaveParameters() => new
    {
        Id,
        ResourceId,
        EquipmentTypeId,
        Name,
        PartNumber,
        Cpu,
        MonitorSize,
        MemoryTypeId,
        MemoryBanks,
        IsMultimedia,
        NetworkInterfaces,
        NetworkName,
        Driver,
        LifeTime,
        PhoneTemplate,
        Description,
    };
}
