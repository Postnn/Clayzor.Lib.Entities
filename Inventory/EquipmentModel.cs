using System.Data;
using System.ComponentModel.DataAnnotations.Schema;
using Clayzor.Lib.DALC;
using Dapper;

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

    /// <summary>Название типа памяти.</summary>
    [Column(MemoryTypeColumns.ТипПамяти)]
    public string MemoryTypeName { get; set; } = string.Empty;

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
    public static async Task<EquipmentModel?> GetForEditAsync(DbManager db, int id, CancellationToken cancellationToken = default) =>
        (await db.QueryRequiredAsync<EquipmentModel>(SQLQueries.SELECT_МодельОборудованияДляРедактирования,
            new { Id = id }, cancellationToken))
            .SingleOrDefault();

    /// <summary>Проверяет уникальность пары «модель + PN» с исключением текущей записи.</summary>
    public static async Task<bool> HasDuplicateAsync(DbManager db, EquipmentModel model, CancellationToken cancellationToken = default) =>
        (await db.QueryRequiredAsync<int>(SQLQueries.SELECT_МодельОборудования_EXISTS, new
        {
            Id = model.Id,
            model.Name,
            model.PartNumber,
        }, cancellationToken)).Single() == 1;

    /// <summary>Возвращает количество оборудования модели, включая списанное.</summary>
    public static async Task<EquipmentModelUsage> GetUsageAsync(DbManager db, int id, int? newEquipmentTypeId = null, CancellationToken cancellationToken = default)
    {
        var sql = newEquipmentTypeId is null
            ? SQLQueries.SELECT_ID_КоличествоОборудованияМодели
            : SQLQueries.SELECT_ID_КоличествоОборудованияМоделиДругогоТипа;
        return (await db.QueryRequiredAsync<EquipmentModelUsage>(sql,
            new { Id = id, EquipmentTypeId = newEquipmentTypeId }, cancellationToken)).Single();
    }

    /// <summary>Возвращает первую ошибку обязательных полей модели.</summary>
    public string? Validate()
    {
        // Код 2 сохранён из действующего правила PN. Название строки справочника не подтверждено.
        const int existingPartNumberTypeId = 2;
        if (Id < 0 || EquipmentTypeId <= 0 || string.IsNullOrWhiteSpace(Name))
            return "Выберите тип и укажите наименование модели.";
        if ((EquipmentTypeId == (int)ТипыОборудования.Компьютер || EquipmentTypeId == existingPartNumberTypeId) &&
            (PartNumber?.Trim().Length ?? 0) < 3)
            return "Для типов 1 и 2 PN должен содержать не менее 3 символов.";
        if (NetworkInterfaces < 0)
            return "Количество сетевых интерфейсов не может быть отрицательным.";
        if (NetworkInterfaces > 0 && (NetworkName?.Trim().Length ?? 0) < 3)
            return "При Net больше 0 название Net должно содержать не менее 3 символов.";
        return null;
    }

    /// <summary>Фиксирует исходную версию, тип и шаблон независимо от дальнейших изменений полей.</summary>
    public EquipmentModelWriteBaseline CaptureWriteBaseline() => new(Id, EquipmentTypeId, ChangedAt, PhoneTemplate);

    /// <summary>Проверяет модель и свежие права перед созданием или обновлением.</summary>
    public async Task<int> SaveAsync(DbManager db, string? windowsPrincipal, int expectedEmployeeId,
        EquipmentModelWriteBaseline? baseline = null, int? confirmedTypeId = null, CancellationToken cancellationToken = default,
        EquipmentModelWriteBoundary? writeBoundary = null)
    {
        var submitted = (EquipmentModel)MemberwiseClone();
        if (submitted.Validate() is { } validation)
            throw new InvalidOperationException(validation);
        EnsurePrincipalPresent(windowsPrincipal, expectedEmployeeId);

        var type = (await db.QueryRequiredAsync<EquipmentType>(SQLQueries.SELECT_ТипОборудованияПоИдентификатору,
            new { Id = submitted.EquipmentTypeId }, cancellationToken)).SingleOrDefault()
            ?? throw new InvalidOperationException("Выбранный тип оборудования не найден.");
        if (submitted.NetworkInterfaces > type.MacAddressCount)
            throw new InvalidOperationException("Количество сетевых интерфейсов превышает предел выбранного типа оборудования.");
        if (submitted.Id > 0)
        {
            if (baseline?.Id != submitted.Id)
                throw new InvalidOperationException("Исходная версия модели недоступна. Перечитайте модель перед сохранением.");
            var stored = await GetForEditAsync(db, submitted.Id, cancellationToken)
                ?? throw new InvalidOperationException("Модель оборудования больше недоступна.");
            if (stored.EquipmentTypeId != submitted.EquipmentTypeId && confirmedTypeId != submitted.EquipmentTypeId &&
                (await GetUsageAsync(db, submitted.Id, submitted.EquipmentTypeId, cancellationToken)).Total > 0)
                throw new InvalidOperationException("Подтвердите изменение типа модели со связанным оборудованием.");
        }
        if (await HasDuplicateAsync(db, submitted, cancellationToken))
            throw new InvalidOperationException("Модель с таким наименованием и PN уже существует.");

        if (writeBoundary is not null)
            await writeBoundary.PrepareAsync(cancellationToken);
        await RequireWriterAsync(db, windowsPrincipal, expectedEmployeeId, Роли.Специалист_по_инвентаризации_оборудования, cancellationToken);
        if (submitted.Id == 0)
        {
            writeBoundary?.BeforeDispatch();
            var id = await db.ExecuteWriteScalarAsync<int>(SQLQueries.INSERT_МоделиОборудования, submitted.SaveParameters());
            if (id <= 0)
                throw new InvalidOperationException("База данных не вернула идентификатор созданной модели.");
            Id = id;
            return id;
        }

        var clearTemplate = baseline!.EquipmentTypeId != submitted.EquipmentTypeId &&
            submitted.EquipmentTypeId != (int)ТипыОборудования.IP_телефон;
        var parameters = new DynamicParameters(submitted.SaveParameters());
        AddExpectedVersion(parameters, baseline);
        if (clearTemplate)
            AddExpectedTemplate(parameters, baseline);

        writeBoundary?.BeforeDispatch();
        await ExecuteVersionedUpdateAsync(db,
            clearTemplate ? SQLQueries.UPDATE_МоделиОборудования_ОчиститьШаблон : SQLQueries.UPDATE_МоделиОборудования,
            parameters, cancellationToken, writeBoundary?.OnKnownNoEffect);
        return submitted.Id;
    }

    /// <summary>Проверяет актуальный тип и права перед условной записью шаблона существующего IP-телефона.</summary>
    public async Task SavePhoneTemplateAsync(DbManager db, string? windowsPrincipal, int expectedEmployeeId,
        EquipmentModelWriteBaseline baseline, CancellationToken cancellationToken = default,
        EquipmentModelWriteBoundary? writeBoundary = null)
    {
        var id = Id;
        var template = PhoneTemplate;
        EnsurePrincipalPresent(windowsPrincipal, expectedEmployeeId);
        if (id <= 0 || baseline.Id != id || baseline.EquipmentTypeId != (int)ТипыОборудования.IP_телефон)
            throw new InvalidOperationException("Исходная версия шаблона недоступна.");
        var stored = await GetForEditAsync(db, id, cancellationToken);
        if (stored?.EquipmentTypeId != (int)ТипыОборудования.IP_телефон)
            throw new InvalidOperationException("Модель не найдена или больше не относится к типу IP-телефон.");
        if (writeBoundary is not null)
            await writeBoundary.PrepareAsync(cancellationToken);
        await RequireWriterAsync(db, windowsPrincipal, expectedEmployeeId, Роли.Дежурный_инженер, cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("Id", id);
        parameters.Add("PhoneTemplate", template, DbType.String, size: -1);
        AddExpectedVersion(parameters, baseline);
        AddExpectedTemplate(parameters, baseline);
        if (writeBoundary is null)
        {
            await ExecuteVersionedUpdateAsync(db, SQLQueries.UPDATE_МоделиОборудования_ШаблонТелефона,
                parameters, cancellationToken);
            return;
        }

        var affected = await db.RunDbAsync(async connection =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            writeBoundary.BeforeDispatch();
            try
            {
                return await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                    SQLQueries.UPDATE_МоделиОборудования_ШаблонТелефона, parameters,
                    commandType: CommandType.Text, cancellationToken: cancellationToken));
            }
            catch (System.Data.Common.DbException)
            {
                // Текст SQL-ошибки может содержать данные параметров. Форма получает только безопасное состояние.
                throw new InvalidOperationException("Не удалось подтвердить результат записи шаблона.");
            }
        });
        if (affected == 0)
        {
            writeBoundary.OnKnownNoEffect();
            throw new InvalidOperationException("Модель или её шаблон были изменены либо удалены. Перечитайте данные и проверьте свой ввод.");
        }
        if (affected != 1)
            throw new InvalidOperationException("База данных не подтвердила результат обновления модели.");
    }

    private static void AddExpectedVersion(DynamicParameters parameters, EquipmentModelWriteBaseline baseline)
    {
        parameters.Add("ExpectedTypeId", baseline.EquipmentTypeId);
        parameters.Add("ExpectedChangedAt", baseline.ChangedAt, DbType.DateTime);
    }

    private static void AddExpectedTemplate(DynamicParameters parameters, EquipmentModelWriteBaseline baseline)
    {
        parameters.Add("PhoneEquipmentTypeId", (int)ТипыОборудования.IP_телефон);
        parameters.Add("ExpectedPhoneTemplate", baseline.PhoneTemplate ?? string.Empty, DbType.String, size: -1);
    }

    private static async Task ExecuteVersionedUpdateAsync(DbManager db, string sql, DynamicParameters parameters,
        CancellationToken cancellationToken, Action? onKnownNoEffect = null)
    {
        // Не передаём текст шаблона или его исходное значение в журнал параметров DbManager.
        var affected = await db.RunDbAsync(connection => connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(sql, parameters, commandType: CommandType.Text, cancellationToken: cancellationToken)));
        if (affected == 0)
        {
            onKnownNoEffect?.Invoke();
            throw new InvalidOperationException("Модель или её шаблон были изменены либо удалены. Перечитайте данные и проверьте свой ввод.");
        }
        if (affected != 1)
            throw new InvalidOperationException("База данных не подтвердила результат обновления модели.");
    }

    private static void EnsurePrincipalPresent(string? windowsPrincipal, int expectedEmployeeId)
    {
        if (string.IsNullOrWhiteSpace(windowsPrincipal) || expectedEmployeeId <= 0)
            throw new UnauthorizedAccessException("Не удалось подтвердить текущего сотрудника.");
    }

    private static async Task RequireWriterAsync(DbManager db, string? windowsPrincipal, int expectedEmployeeId,
        Роли role, CancellationToken cancellationToken)
    {
        var actor = await Employee.GetCurrentWithRolesAsync(db, windowsPrincipal, cancellationToken);
        if (actor is null || actor.Id != expectedEmployeeId || !actor.HasRole(role))
            throw new UnauthorizedAccessException("Текущий сотрудник или его право на сохранение не подтверждены.");
        cancellationToken.ThrowIfCancellationRequested();
    }

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
        Description,
    };
}

/// <summary>Значения сохранённой модели, на которые опирается условное изменение.</summary>
public sealed record EquipmentModelWriteBaseline(int Id, int EquipmentTypeId, DateTime? ChangedAt, string? PhoneTemplate);

/// <summary>Фазы подтверждения попытки модели до свежей авторизации и до отправки DML.</summary>
/// <param name="PrepareAsync">Подтверждает долговечную попытку до авторизации записи.</param>
/// <param name="BeforeDispatch">Отмечает достижение границы первой возможной изменяющей команды.</param>
/// <param name="OnKnownNoEffect">Отмечает доказанный сервером отсутствующий эффект условного UPDATE.</param>
public sealed record EquipmentModelWriteBoundary(
    Func<CancellationToken, Task> PrepareAsync, Action BeforeDispatch, Action OnKnownNoEffect);
