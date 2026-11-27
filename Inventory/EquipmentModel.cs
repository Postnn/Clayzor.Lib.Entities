using System.Data;
using System.Data.Common;
using System.Globalization;
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

    private const Роли ModelWriteRole = Роли.Специалист_по_инвентаризации_оборудования;
    private const string ModelVersionConflict = "Модель или её шаблон были изменены либо удалены. Перечитайте данные и проверьте свой ввод.";

    /// <summary>Проверяет модель, права и исходную версию, после чего сохраняет её в транзакции.</summary>
    public async Task<int> SaveAsync(DbManager db, string? windowsPrincipal, int expectedEmployeeId,
        EquipmentModelWriteBaseline? baseline = null, int? confirmedTypeId = null, CancellationToken cancellationToken = default)
    {
        var submitted = (EquipmentModel)MemberwiseClone();
        if (submitted.Validate() is { } validation)
            throw new InvalidOperationException(validation);

        await RequireModelWriterAsync(db, windowsPrincipal, expectedEmployeeId, cancellationToken);
        var clearTemplate = baseline is not null && baseline.EquipmentTypeId != submitted.EquipmentTypeId &&
            submitted.EquipmentTypeId != (int)ТипыОборудования.IP_телефон;
        await db.RunDbAsync(connection => CheckProposalAsync(connection, null, cancellationToken));

        try
        {
            Id = await db.RunDbAsync(async connection =>
            {
                await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                try
                {
                    var fresh = await Employee.GetCurrentWithRolesAsync(connection, transaction, windowsPrincipal, cancellationToken);
                    ValidateWriter(fresh, expectedEmployeeId, ModelWriteRole);
                    await CheckProposalAsync(connection, transaction, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (Exception error) when (error is InvalidOperationException or UnauthorizedAccessException or DbException or
                    TimeoutException or OperationCanceledException)
                {
                    throw new ModelWriteRejectedException(error is DbException
                        ? "Не удалось проверить актуальные данные модели. Запись не начата." : error.Message, error);
                }

                int resultId;
                if (submitted.Id == 0)
                {
                    var inserted = await connection.ExecuteScalarAsync<int?>(
                        Command(SQLQueries.INSERT_МоделиОборудования, submitted.SaveParameters(), transaction, cancellationToken));
                    if (inserted is not > 0)
                        throw new ModelWriteRejectedException("База данных не вернула идентификатор созданной модели.");
                    resultId = inserted.Value;
                }
                else
                {
                    var parameters = new DynamicParameters(submitted.SaveParameters());
                    AddExpectedVersion(parameters, baseline!);
                    if (clearTemplate)
                        AddExpectedTemplate(parameters, baseline!);
                    var sql = clearTemplate ? SQLQueries.UPDATE_МоделиОборудования_ОчиститьШаблон : SQLQueries.UPDATE_МоделиОборудования;
                    var affected = await connection.ExecuteScalarAsync<int?>(Command(sql, parameters, transaction, cancellationToken));
                    if (affected != 1)
                        throw new ModelWriteRejectedException(affected == 0 ? ModelVersionConflict :
                            "База данных не подтвердила обновление одной модели.");
                    resultId = submitted.Id;
                }

                await transaction.CommitAsync(CancellationToken.None);
                return resultId;
            });
            return Id;
        }
        catch (ModelWriteRejectedException error)
        {
            throw new InvalidOperationException(error.Message, error);
        }

        async Task<bool> CheckProposalAsync(DbConnection connection, DbTransaction? transaction, CancellationToken token)
        {
            var type = await connection.QuerySingleOrDefaultAsync<EquipmentType>(
                Command(SQLQueries.SELECT_ТипОборудованияПоИдентификатору, new { Id = submitted.EquipmentTypeId }, transaction, token))
                ?? throw new InvalidOperationException("Выбранный тип оборудования не найден.");
            if (submitted.NetworkInterfaces > type.MacAddressCount)
                throw new InvalidOperationException("Количество сетевых интерфейсов превышает предел выбранного типа оборудования.");

            if (submitted.Id > 0)
            {
                if (baseline?.Id != submitted.Id)
                    throw new InvalidOperationException("Исходная версия модели недоступна. Перечитайте модель перед сохранением.");
                var stored = await connection.QuerySingleOrDefaultAsync<EquipmentModel>(
                    Command(SQLQueries.SELECT_МодельОборудованияДляРедактирования, new { Id = submitted.Id }, transaction, token))
                    ?? throw new InvalidOperationException("Модель оборудования больше недоступна.");
                if (stored.EquipmentTypeId != baseline.EquipmentTypeId || stored.ChangedAt != baseline.ChangedAt ||
                    clearTemplate && (stored.PhoneTemplate ?? string.Empty) != (baseline.PhoneTemplate ?? string.Empty))
                    throw new InvalidOperationException(ModelVersionConflict);
                if (stored.EquipmentTypeId != submitted.EquipmentTypeId && confirmedTypeId != submitted.EquipmentTypeId)
                {
                    var usage = await connection.QuerySingleAsync<EquipmentModelUsage>(
                        Command(SQLQueries.SELECT_ID_КоличествоОборудованияМоделиДругогоТипа,
                            new { Id = submitted.Id, submitted.EquipmentTypeId }, transaction, token));
                    if (usage.Total > 0)
                        throw new InvalidOperationException("Подтвердите изменение типа модели со связанным оборудованием.");
                }
            }
            if (await connection.QuerySingleAsync<int>(Command(SQLQueries.SELECT_МодельОборудования_EXISTS,
                    new { submitted.Id, submitted.Name, submitted.PartNumber }, transaction, token)) == 1)
                throw new InvalidOperationException("Модель с таким наименованием и PN уже существует.");
            return true;
        }
    }

    private static async Task<Employee> RequireModelWriterAsync(DbManager db, string? principal, int expectedEmployeeId,
        CancellationToken ct)
    {
        EnsurePrincipalPresent(principal, expectedEmployeeId);
        var actor = await Employee.GetCurrentWithRolesAsync(db, principal, ct);
        ValidateWriter(actor, expectedEmployeeId, ModelWriteRole);
        ct.ThrowIfCancellationRequested();
        return actor!;
    }

    private static void ValidateWriter(Employee? actor, int expectedEmployeeId, Роли role)
    {
        if (actor is null || actor.Id != expectedEmployeeId || !actor.HasRole(role))
            throw new UnauthorizedAccessException("Текущий сотрудник или его право на сохранение не подтверждены.");
    }

    private static CommandDefinition Command(string sql, object parameters, DbTransaction? transaction, CancellationToken ct) =>
        new(sql, parameters, transaction, commandType: CommandType.Text, cancellationToken: ct);

    private sealed class ModelWriteRejectedException(string message, Exception? inner = null) : InvalidOperationException(message, inner);

    /// <summary>Проверяет актуальный тип и права перед условной записью шаблона существующего IP-телефона.</summary>
    public async Task SavePhoneTemplateAsync(DbManager db, string? windowsPrincipal, int expectedEmployeeId,
        EquipmentModelWriteBaseline baseline, CancellationToken cancellationToken = default)
    {
        var id = Id;
        EnsurePrincipalPresent(windowsPrincipal, expectedEmployeeId);
        if (id <= 0 || baseline.Id != id || baseline.EquipmentTypeId != (int)ТипыОборудования.IP_телефон)
            throw new InvalidOperationException("Исходная версия шаблона недоступна.");
        var stored = await GetForEditAsync(db, id, cancellationToken);
        if (stored?.EquipmentTypeId != (int)ТипыОборудования.IP_телефон)
            throw new InvalidOperationException("Модель не найдена или больше не относится к типу IP-телефон.");
        await RequireWriterAsync(db, windowsPrincipal, expectedEmployeeId, Роли.Дежурный_инженер, cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("Id", id);
        parameters.Add("PhoneTemplate", PhoneTemplate, DbType.String, size: -1);
        AddExpectedVersion(parameters, baseline);
        AddExpectedTemplate(parameters, baseline);
        await ExecuteVersionedUpdateAsync(db, SQLQueries.UPDATE_МоделиОборудования_ШаблонТелефона,
            parameters, cancellationToken);
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
        ValidateWriter(actor, expectedEmployeeId, role);
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

