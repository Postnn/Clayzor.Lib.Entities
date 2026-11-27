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

    private const string ModelSaveKind = "equipment-model.save";
    private const Роли ModelWriteRole = Роли.Специалист_по_инвентаризации_оборудования;
    private const string ModelVersionConflict = "Модель или её шаблон были изменены либо удалены. Перечитайте данные и проверьте свой ввод.";

    /// <summary>Проверяет модель и права, атомарно фиксируя запись и её результат в серверном журнале.</summary>
    public async Task<int> SaveAsync(DbManager db, string? windowsPrincipal, int expectedEmployeeId, Guid attemptId,
        EquipmentModelWriteBaseline? baseline = null, int? confirmedTypeId = null, CancellationToken cancellationToken = default,
        EquipmentModelWriteBoundary? writeBoundary = null)
    {
        var submitted = (EquipmentModel)MemberwiseClone();
        if (submitted.Validate() is { } validation)
            throw new InvalidOperationException(validation);

        var actor = await RequireModelWriterAsync(db, windowsPrincipal, expectedEmployeeId, cancellationToken);
        var attempt = InventoryOperationJournal.ForEmployee(actor, attemptId, ModelConflictKey(submitted.Id), ModelSaveKind);
        if (await InventoryOperationJournal.ReadAttemptAsync(db, attempt, cancellationToken) is { } previous)
            return AcceptReceipt(previous);

        var clearTemplate = baseline is not null && baseline.EquipmentTypeId != submitted.EquipmentTypeId &&
            submitted.EquipmentTypeId != (int)ТипыОборудования.IP_телефон;
        await db.RunDbAsync(connection => CheckProposalAsync(connection, null, cancellationToken));

        if (writeBoundary is not null)
            await writeBoundary.PrepareAsync(cancellationToken);

        try
        {
            var receipt = await InventoryOperationJournal.RunSqlAsync<ModelWriteRejectedException>(db, attempt,
                async (connection, transaction, token) =>
                {
                    try
                    {
                        var fresh = await Employee.GetCurrentWithRolesAsync(connection, transaction, windowsPrincipal, token);
                        ValidateWriter(fresh, expectedEmployeeId, ModelWriteRole);
                        await CheckProposalAsync(connection, transaction, token);
                        token.ThrowIfCancellationRequested();
                        writeBoundary?.BeforeDispatch();
                    }
                    catch (Exception error) when (error is InvalidOperationException or UnauthorizedAccessException or DbException or
                        TimeoutException or OperationCanceledException)
                    {
                        throw new ModelWriteRejectedException(error is DbException
                            ? "Не удалось проверить актуальные данные модели. Запись не начата." : error.Message, error);
                    }

                    if (submitted.Id == 0)
                    {
                        var inserted = await connection.ExecuteScalarAsync<int?>(
                            Command(SQLQueries.INSERT_МоделиОборудования_Журнал, submitted.SaveParameters(), transaction, token));
                        if (inserted is not > 0)
                            throw new ModelWriteRejectedException("База данных не вернула идентификатор созданной модели.");
                        return inserted;
                    }

                    var parameters = new DynamicParameters(submitted.SaveParameters());
                    AddExpectedVersion(parameters, baseline!);
                    if (clearTemplate)
                        AddExpectedTemplate(parameters, baseline!);
                    var sql = clearTemplate ? SQLQueries.UPDATE_МоделиОборудования_ОчиститьШаблон : SQLQueries.UPDATE_МоделиОборудования;
                    var affected = await connection.ExecuteScalarAsync<int?>(Command(sql, parameters, transaction, token));
                    if (affected != 1)
                        throw new ModelWriteRejectedException(affected == 0 ? ModelVersionConflict :
                            "База данных не подтвердила обновление одной модели.");
                    return submitted.Id;
                }, cancellationToken);
            return AcceptReceipt(receipt);
        }
        catch (ModelWriteRejectedException error)
        {
            writeBoundary?.OnKnownNoEffect();
            throw new InvalidOperationException(error.Message, error);
        }

        int AcceptReceipt(InventoryOperationReceipt receipt)
        {
            if (receipt.State == InventoryOperationState.Rejected)
            {
                writeBoundary?.OnKnownNoEffect();
                throw new InvalidOperationException("Эта попытка сохранения модели ранее отклонена.");
            }
            if (receipt.State != InventoryOperationState.Confirmed)
                throw new InventoryOperationPendingException();
            if (receipt.ResultEntityId is not > 0 || submitted.Id > 0 && receipt.ResultEntityId != submitted.Id)
                throw new InvalidOperationException("Журнал не подтвердил идентификатор сохранённой модели.");
            Id = receipt.ResultEntityId.Value;
            return Id;
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

    /// <summary>Читает только собственный последующий серверный исход и наличие незавершённой попытки модели.</summary>
    public static async Task<(InventoryOperationReceipt? Latest, bool HasPending)> ReadSaveJournalAsync(DbManager db,
        string? windowsPrincipal, int expectedEmployeeId, int id, CancellationToken ct)
    {
        var actor = await RequireModelWriterAsync(db, windowsPrincipal, expectedEmployeeId, ct);
        var key = ModelConflictKey(id);
        InventoryOperationReceipt? latest = null;
        try
        {
            latest = await InventoryOperationJournal.ReadLatestAsync(db, actor, key, ModelSaveKind, ct);
        }
        catch (Exception error) when (error is System.Data.Common.DbException or TimeoutException or InvalidOperationException)
        {
            // The informational result is independent of the required pending check.
        }
        return (latest, await InventoryOperationJournal.IsScopePendingAsync(db, key, ct));
    }

    /// <summary>Сверяет точную серверную попытку сохранения модели без повторной записи.</summary>
    public static async Task<InventoryOperationReceipt?> ReadSaveReceiptAsync(DbManager db, string? windowsPrincipal,
        int expectedEmployeeId, int id, Guid attemptId, CancellationToken ct)
    {
        var actor = await RequireModelWriterAsync(db, windowsPrincipal, expectedEmployeeId, ct);
        return await InventoryOperationJournal.ReadAttemptAsync(db,
            InventoryOperationJournal.ForEmployee(actor, attemptId, ModelConflictKey(id), ModelSaveKind), ct);
    }

    /// <summary>Учитывает прежние неподтверждённые попытки сохранения доступной модели.</summary>
    public static async Task ImportLegacySaveOperationsAsync(DbManager db, string? principal,
        int expectedEmployeeId, int id, IReadOnlyCollection<Guid> attempts, CancellationToken ct)
    {
        if (attempts.Count == 0) return;
        ArgumentOutOfRangeException.ThrowIfNegative(id);
        var actor = await RequireModelWriterAsync(db, principal, expectedEmployeeId, ct);
        if (id > 0 && await GetForEditAsync(db, id, ct) is null)
            throw new UnauthorizedAccessException("Исходная модель прежней операции недоступна.");

        await InventoryOperationJournal.ImportLegacyAsync(db, attempts.Distinct().Order()
            .Select(attemptId => InventoryOperationJournal.ForEmployee(actor, attemptId, ModelConflictKey(id), ModelSaveKind))
            .ToArray(), ct);
    }

    private static string ModelConflictKey(int id) => id == 0 ? "model/new" : "model/" + id.ToString(CultureInfo.InvariantCulture);

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

    private const string PhoneTemplateWriteKind = "equipment-phone-template.save";

    internal static Task<bool> HasPendingPhoneTemplateWriteAsync(DbManager db, Employee actor, int modelId, CancellationToken ct)
    {
        RequireTemplateWriter(actor, actor.Id);
        return InventoryOperationJournal.IsScopePendingAsync(db, PhoneTemplateScope(modelId), ct);
    }

    internal static Task<InventoryOperationReceipt?> ReadPhoneTemplateReceiptAsync(DbManager db, Employee actor,
        int modelId, Guid attemptId, CancellationToken ct)
    {
        RequireTemplateWriter(actor, actor.Id);
        return InventoryOperationJournal.ReadAttemptAsync(db, PhoneTemplateAttempt(actor, modelId, attemptId), ct);
    }

    internal static Task<InventoryOperationReceipt?> ReadLatestPhoneTemplateReceiptAsync(DbManager db, Employee actor,
        EquipmentModel model, CancellationToken ct)
    {
        RequireTemplateWriter(actor, actor.Id);
        if (model.Id <= 0 || model.EquipmentTypeId != (int)ТипыОборудования.IP_телефон)
            throw new UnauthorizedAccessException("Модель шаблона недоступна.");
        return InventoryOperationJournal.ReadLatestAsync(db, actor, PhoneTemplateScope(model.Id), PhoneTemplateWriteKind, ct);
    }

    /// <summary>Учитывает прежние неподтверждённые попытки изменения шаблона доступной IP-модели.</summary>
    public static async Task ImportLegacyPhoneTemplateOperationsAsync(DbManager db, string? principal,
        int expectedEmployeeId, int modelId, IReadOnlyCollection<Guid> attempts, CancellationToken ct)
    {
        if (attempts.Count == 0) return;
        EnsurePrincipalPresent(principal, expectedEmployeeId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(modelId);
        var actor = RequireTemplateWriter(await Employee.GetCurrentWithRolesAsync(db, principal, ct), expectedEmployeeId);
        if ((await GetForEditAsync(db, modelId, ct))?.EquipmentTypeId != (int)ТипыОборудования.IP_телефон)
            throw new UnauthorizedAccessException("Модель прежней операции с шаблоном недоступна.");

        await InventoryOperationJournal.ImportLegacyAsync(db, attempts.Distinct().Order()
            .Select(attemptId => PhoneTemplateAttempt(actor, modelId, attemptId)).ToArray(), ct);
    }

    internal async Task<InventoryOperationReceipt> SaveJournaledPhoneTemplateAsync(DbManager db, string? principal, int expectedEmployeeId,
        EquipmentModelWriteBaseline baseline, Guid attemptId, EquipmentModelWriteBoundary boundary, CancellationToken ct)
    {
        var id = Id;
        var template = PhoneTemplate;
        EnsurePrincipalPresent(principal, expectedEmployeeId);
        if (id <= 0 || baseline.Id != id || baseline.EquipmentTypeId != (int)ТипыОборудования.IP_телефон)
            throw new PhoneTemplateWriteRejectedException("Исходная версия шаблона недоступна.");

        var stored = await GetForEditAsync(db, id, ct);
        var actor = RequireTemplateWriter(await Employee.GetCurrentWithRolesAsync(db, principal, ct), expectedEmployeeId);
        var attempt = PhoneTemplateAttempt(actor, id, attemptId);
        if (await InventoryOperationJournal.ReadAttemptAsync(db, attempt, ct) is { } previous)
            return RequireReceipt(previous);
        if (stored?.EquipmentTypeId != (int)ТипыОборудования.IP_телефон)
            throw new PhoneTemplateWriteRejectedException("Модель не найдена или больше не относится к типу IP-телефон.");

        await boundary.PrepareAsync(ct);
        var parameters = new DynamicParameters();
        parameters.Add("Id", id);
        parameters.Add("PhoneTemplate", template, DbType.String, size: -1);
        AddExpectedVersion(parameters, baseline);
        AddExpectedTemplate(parameters, baseline);

        InventoryOperationReceipt receipt;
        try
        {
            receipt = await InventoryOperationJournal.RunSqlAsync<PhoneTemplateWriteRejectedException>(db, attempt,
                async (connection, transaction, token) =>
                {
                    RequireTemplateWriter(await Employee.GetCurrentWithRolesAsync(connection, transaction, principal, token),
                        expectedEmployeeId);
                    token.ThrowIfCancellationRequested();
                    try { boundary.BeforeDispatch(); }
                    catch (InvalidOperationException error) { throw new PhoneTemplateWriteRejectedException(error.Message); }

                    int? affected;
                    try
                    {
                        affected = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                            SQLQueries.UPDATE_МоделиОборудования_ШаблонТелефона, parameters, transaction,
                            commandType: CommandType.Text, cancellationToken: token));
                    }
                    catch (System.Data.Common.DbException)
                    {
                        throw new InvalidOperationException("Не удалось подтвердить результат записи шаблона.");
                    }
                    if (affected == 0)
                        throw new PhoneTemplateWriteRejectedException(
                            "Модель или её шаблон были изменены либо удалены. Перечитайте данные и проверьте свой ввод.",
                            baselineConflict: true);
                    if (affected != 1)
                        throw new InvalidOperationException("База данных не подтвердила изменение ровно одного шаблона.");
                    return id;
                }, ct);
        }
        catch (PhoneTemplateWriteRejectedException)
        {
            boundary.OnKnownNoEffect();
            throw;
        }
        catch (Exception error) when (error is InventoryOperationPendingException or InventoryOperationReservationException)
        {
            boundary.OnKnownNoEffect();
            throw;
        }
        catch (Exception error) when (error is System.Data.Common.DbException or InvalidOperationException or TimeoutException or IOException)
        {
            // Текст команды и диагностические сообщения SQL могут содержать параметры шаблона.
            InventoryOperationReceipt? observed = null;
            if (!ct.IsCancellationRequested)
            {
                try { observed = await InventoryOperationJournal.ReadAttemptAsync(db, attempt, ct); }
                catch (Exception readError) when (readError is System.Data.Common.DbException or InvalidOperationException or TimeoutException or IOException) { }
            }
            if (observed is { State: InventoryOperationState.Confirmed, ResultEntityId: var resultId } && resultId == id)
                return observed;
            throw new InvalidOperationException("Не удалось подтвердить результат записи шаблона.");
        }

        return RequireReceipt(receipt);

        InventoryOperationReceipt RequireReceipt(InventoryOperationReceipt outcome)
        {
            if (outcome.State == InventoryOperationState.Rejected)
                throw new PhoneTemplateWriteRejectedException("Запись ранее отклонена. Перечитайте шаблон.");
            if (outcome.State != InventoryOperationState.Confirmed || outcome.ResultEntityId != id)
                throw new InventoryOperationPendingException();
            return outcome;
        }
    }

    private static string PhoneTemplateScope(int id) => ModelConflictKey(id);

    private static InventoryOperationJournal.Attempt PhoneTemplateAttempt(Employee actor, int id, Guid attemptId) =>
        InventoryOperationJournal.ForEmployee(actor, attemptId, PhoneTemplateScope(id), PhoneTemplateWriteKind);

    private static Employee RequireTemplateWriter(Employee? actor, int expectedEmployeeId) =>
        actor is not null && actor.Id == expectedEmployeeId && actor.HasRole(Роли.Дежурный_инженер)
            ? actor : throw new PhoneTemplateWriteRejectedException("Текущий сотрудник или его право сохранения шаблона не подтверждены.");

    internal sealed class PhoneTemplateWriteRejectedException(string message, bool baselineConflict = false)
        : InvalidOperationException(message)
    {
        internal bool BaselineConflict { get; } = baselineConflict;
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

/// <summary>Фазы подтверждения попытки модели до свежей авторизации и до отправки DML.</summary>
/// <param name="PrepareAsync">Подтверждает долговечную попытку до авторизации записи.</param>
/// <param name="BeforeDispatch">Отмечает достижение границы первой возможной изменяющей команды.</param>
/// <param name="OnKnownNoEffect">Отмечает доказанный сервером отсутствующий эффект условного UPDATE.</param>
public sealed record EquipmentModelWriteBoundary(
    Func<CancellationToken, Task> PrepareAsync, Action BeforeDispatch, Action OnKnownNoEffect);
