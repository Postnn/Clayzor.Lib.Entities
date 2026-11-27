using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Globalization;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Сохранение порта подтверждённо отклонено без эффекта.</summary>
public sealed class LocalNetworkPortRejectedException(string message, Exception? inner = null)
    : InvalidOperationException(message, inner);

/// <summary>Запись порта локальной сети из vwЛокальнаяСеть.</summary>
public sealed class LocalNetworkPort
{
    /// <summary>Идентификатор порта.</summary>
    [Column(LocalNetworkPortColumns.КодЛокальнойСети)]
    public int Id { get; set; }

    /// <summary>Идентификатор оборудования.</summary>
    [Column(LocalNetworkPortColumns.КодОборудования)]
    public int EquipmentId { get; set; }

    /// <summary>Номер порта.</summary>
    [Column(LocalNetworkPortColumns.Порт)]
    public byte Port { get; set; }

    /// <summary>Признак поддержки PoE.</summary>
    [Column(LocalNetworkPortColumns.PoE)]
    public byte Poe { get; set; }

    /// <summary>Идентификатор заказчика.</summary>
    [Column(LocalNetworkPortColumns.КодЛицаЗаказчика)]
    public int PersonCustomerId { get; set; }

    /// <summary>Идентификатор сотрудника.</summary>
    [Column(LocalNetworkPortColumns.КодСотрудника)]
    public int? EmployeeId { get; set; }

    /// <summary>Идентификатор розетки.</summary>
    [Column(LocalNetworkPortColumns.КодРозетки)]
    public int? SocketId { get; set; }

    /// <summary>Исходный текст поля «Подключено».</summary>
    [Column(LocalNetworkPortColumns.Подключено)]
    public string Connected { get; set; } = string.Empty;

    /// <summary>Исходный текст поля «Примечание».</summary>
    [Column(LocalNetworkPortColumns.Примечания)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Календарный срок возврата без UTC-преобразования.</summary>
    [Column(LocalNetworkPortColumns.СрокВозврата)]
    public DateTime? ReturnDate { get; set; }

    /// <summary>Идентификатор изменившего сотрудника.</summary>
    [Column(LocalNetworkPortColumns.Изменил)]
    public int ChangedById { get; set; }

    /// <summary>Отображаемое имя изменившего сотрудника.</summary>
    [Column(EmployeeColumns.Сотрудник)]
    public string ChangedByName { get; set; } = string.Empty;

    /// <summary>Время изменения в UTC.</summary>
    [Column(LocalNetworkPortColumns.Изменено)]
    public DateTime ChangedAtUtc { get; set; }

    /// <summary>Загружает существующий доступный порт.</summary>
    public static async Task<LocalNetworkPort?> GetAsync(
        DbManager db,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return null;
        var rows = await db.QueryRequiredAsync<LocalNetworkPort>(SQLQueries.SELECT_ID_ЛокальнаяСеть, new { Id = id }, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return rows.SingleOrDefault();
    }

    /// <summary>Возвращает сетевое имя оборудования порта.</summary>
    public static async Task<string?> GetEquipmentNetworkNameAsync(
        DbManager db,
        int equipmentId,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.QueryRequiredAsync<string>(SQLQueries.SELECT_СетевоеИмяОборудованияПоИдентификатору,
            new { EquipmentId = equipmentId }, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return rows.FirstOrDefault();
    }

    /// <summary>Возвращает соседние порты; freeOnly ограничивает список незанятыми.</summary>
    public static async Task<IReadOnlyList<LocalNetworkNeighborPort>> GetNeighborsAsync(
        DbManager db,
        int id,
        bool freeOnly,
        CancellationToken cancellationToken = default)
    {
        var sql = freeOnly ? SQLQueries.SELECT_СоседниеПортыНеЗанятые : SQLQueries.SELECT_СоседниеПортыВсе;
        var rows = await db.QueryRequiredAsync<LocalNetworkNeighborPort>(sql, new { КодЛокальнойСети = id }, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return rows.ToList();
    }

    /// <summary>Находит ближайший офис последней записи расположения оборудования.</summary>
    public static async Task<int?> GetOfficeLocationIdAsync(
        DbManager db,
        int equipmentId,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.QueryRequiredAsync<int>(SQLQueries.SELECT_ОфисПоКодуОборудования,
            new { КодОборудования = equipmentId }, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return rows.FirstOrDefault() is > 0 and var id ? id : null;
    }

    /// <summary>Проверяет обязательного заказчика и связь розетки с сотрудником или подключённым текстом.</summary>
    public string? Validate()
    {
        if (PersonCustomerId <= 0)
            return "Не выбран заказчик.";
        if (SocketId is > 0 && EmployeeId is null && string.IsNullOrEmpty(Connected))
            return "Вы указали розетку. В этом случае необходимо указать сотрудника или что подключено.";
        return null;
    }

    /// <summary>Атомарно сохраняет обычное редактирование, переключение или обмен с проверкой актуальных записей.</summary>
    public static async Task SaveAsync(
        DbManager db,
        string? principal,
        int expectedEmployeeId,
        LocalNetworkPort draft,
        DateTime expectedChangedAtUtc,
        Guid attemptId,
        int? switchToId = null,
        int? exchangeWithId = null,
        CancellationToken ct = default)
    {
        var validation = draft.Validate();
        if (validation is not null)
            throw new LocalNetworkPortRejectedException(validation);
        if (switchToId is not null && exchangeWithId is not null)
            throw new LocalNetworkPortRejectedException("Одновременно можно выбрать только переключение или обмен.");
        var targetId = switchToId ?? exchangeWithId;
        if (draft.Id <= 0 || targetId <= 0 || targetId == draft.Id)
            throw new LocalNetworkPortRejectedException("Выбранный порт недопустим. Обновите список.");

        var actor = RequireEmployee(await Employee.GetCurrentAsync(db, principal, ct), expectedEmployeeId);
        var current = await GetAsync(db, draft.Id, ct);
        if (current?.Id != draft.Id)
            CheckSaveStatus(1);
        var attempt = CreateSaveAttempt(actor, attemptId, draft.Id, switchToId, exchangeWithId);
        var previous = await InventoryOperationJournal.ReadAttemptAsync(db, attempt, ct);
        if (previous is { State: InventoryOperationState.Confirmed, ResultEntityId: var resultId } && resultId == draft.Id)
            return;
        if (previous is { State: InventoryOperationState.Rejected })
            throw new LocalNetworkPortRejectedException("Попытка сохранения порта ранее отклонена.");
        if (previous is not null)
            throw new InventoryOperationPendingException();
        if (current!.ChangedAtUtc != expectedChangedAtUtc)
            CheckSaveStatus(1);

        DateTime? expectedTargetChangedAt = null;
        if (targetId is { } target)
        {
            var targetPort = await GetAsync(db, target, ct);
            if (targetPort?.Id != target)
                CheckSaveStatus(3);
            expectedTargetChangedAt = targetPort!.ChangedAtUtc;
        }

        var sql = switchToId is not null
            ? SQLQueries.SWITCH_ЛокальнаяСеть
            : exchangeWithId is not null
                ? SQLQueries.EXCHANGE_ЛокальнаяСеть
                : SQLQueries.UPDATE_ЛокальнаяСеть;
        var parameters = new DynamicParameters(new
        {
            draft.Id,
            TargetId = targetId ?? 0,
            PersonCustomerId = draft.PersonCustomerId,
            EmployeeId = draft.EmployeeId,
            SocketId = draft.SocketId,
            Connected = draft.Connected?.Trim() ?? string.Empty,
            Description = draft.Description?.Trim() ?? string.Empty,
            ReturnDate = draft.ReturnDate?.Date,
        });
        parameters.Add("ExpectedChangedAt", expectedChangedAtUtc, DbType.DateTime);
        parameters.Add("ExpectedTargetChangedAt", expectedTargetChangedAt, DbType.DateTime);

        var receipt = await InventoryOperationJournal.RunSqlAsync<LocalNetworkPortRejectedException>(db, attempt, WriteAsync, ct);
        if (receipt.State == InventoryOperationState.Rejected)
            throw new LocalNetworkPortRejectedException("Попытка сохранения порта ранее отклонена.");
        if (receipt.State != InventoryOperationState.Confirmed || receipt.ResultEntityId != draft.Id)
            throw new InvalidOperationException("Сервер не подтвердил результат сохранения порта.");

        async Task<int?> WriteAsync(DbConnection connection, DbTransaction transaction, CancellationToken token)
        {
            RequireEmployee(await Employee.GetCurrentAsync(connection, transaction, principal, token), expectedEmployeeId);
            var status = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(sql, parameters, transaction,
                commandType: CommandType.Text, cancellationToken: token));
            CheckSaveStatus(status);
            return draft.Id;
        }
    }

    private static Employee RequireEmployee(Employee? actor, int employeeId) =>
        actor is not null && actor.Id == employeeId ? actor
            : throw new LocalNetworkPortRejectedException("Не удалось подтвердить доступ к сохранению порта.");

    private static void CheckSaveStatus(int? status)
    {
        if (status == 0)
            return;
        if (status == 1)
            throw new LocalNetworkPortRejectedException("Запись локальной сети изменилась или недоступна. Обновите данные.");
        if (status == 2)
            throw new LocalNetworkPortRejectedException("Не выбран заказчик.");
        if (status == 3)
            throw new LocalNetworkPortRejectedException("Выбранный порт недопустим или уже занят. Обновите список.");
        throw new InvalidOperationException("Не получен корректный результат сохранения локальной сети. Обновите данные и проверьте состояние записи.");
    }

    /// <summary>Проверяет блокировку порта из-за незавершённой серверной попытки записи или PoE.</summary>
    public static Task<bool> ReadWritePendingAsync(DbManager db, int portId, CancellationToken ct) =>
        InventoryOperationJournal.IsScopePendingAsync(db, PortScope(portId), ct);

    /// <summary>Читает точный исход сохранения для авторизованного сотрудника и видимого порта.</summary>
    public static async Task<InventoryOperationReceipt?> ReadSaveReceiptAsync(DbManager db, string? principal, int employeeId,
        int portId, int? switchToId, int? exchangeWithId, Guid attemptId, CancellationToken ct)
    {
        var actor = await Employee.GetCurrentAsync(db, principal, ct);
        if (actor is null || actor.Id != employeeId || await GetAsync(db, portId, ct) is null)
            return null;
        return await InventoryOperationJournal.ReadAttemptAsync(db,
            CreateSaveAttempt(actor, attemptId, portId, switchToId, exchangeWithId), ct);
    }

    private static InventoryOperationJournal.Attempt CreateSaveAttempt(Employee actor, Guid id, int portId,
        int? switchToId, int? exchangeWithId) =>
        InventoryOperationJournal.ForEmployee(actor, id, PortScope(portId),
            switchToId is not null ? "net.switch" : exchangeWithId is not null ? "net.exchange" : "net.save",
            (switchToId ?? exchangeWithId) is { } target ? [PortScope(target)] : []);

    /// <summary>Переносит неподтверждённые сведения прежнего редактора порта после проверки сотрудника и связанных портов.</summary>
    public static async Task ImportLegacyOperationsAsync(DbManager db, string? principal, int employeeId, int portId,
        IReadOnlyCollection<(Guid AttemptId, string Kind, int? SwitchToId, int? ExchangeWithId)> evidence, CancellationToken ct)
    {
        if (evidence.Count == 0) return;

        var actor = await Employee.GetCurrentAsync(db, principal, ct);
        if (actor is null || actor.Id != employeeId || await GetAsync(db, portId, ct) is null)
            throw new UnauthorizedAccessException("Не удалось подтвердить сотрудника и порт прежней операции.");

        HashSet<int>? neighbors = null;
        var attempts = new List<InventoryOperationJournal.Attempt>(evidence.Count);
        foreach (var (attemptId, kind, switchToId, exchangeWithId) in evidence.Distinct().OrderBy(item => item.AttemptId))
        {
            if (attemptId == Guid.Empty || (switchToId is not null && exchangeWithId is not null) ||
                switchToId is <= 0 || exchangeWithId is <= 0 || switchToId == portId || exchangeWithId == portId)
                throw new InvalidOperationException("Не удалось подтвердить область прежней операции с портом.");

            if (kind == "net.poe" && switchToId is null && exchangeWithId is null)
            {
                attempts.Add(InventoryOperationJournal.ForEmployee(actor, attemptId, PortScope(portId), "net.poe"));
                continue;
            }
            if (kind != "net.save")
                throw new InvalidOperationException("Не удалось подтвердить вид прежней операции с портом.");
            if ((switchToId ?? exchangeWithId) is { } target)
            {
                neighbors ??= (await GetNeighborsAsync(db, portId, false, cancellationToken: ct))
                    .Select(port => port.Id).ToHashSet();
                if (!neighbors.Contains(target))
                    throw new InvalidOperationException("Соседний порт прежней операции не подтверждён.");
            }
            attempts.Add(CreateSaveAttempt(actor, attemptId, portId, switchToId, exchangeWithId));
        }

        await InventoryOperationJournal.ImportLegacyAsync(db, attempts, ct);
    }

    /// <summary>Читает точный исход PoE для авторизованного сотрудника и видимого порта.</summary>
    public static async Task<InventoryOperationReceipt?> ReadPoeReceiptAsync(DbManager db, string? principal, int employeeId,
        int portId, Guid attemptId, CancellationToken ct)
    {
        var actor = await Employee.GetCurrentAsync(db, principal, ct);
        if (actor is null || actor.Id != employeeId || await GetAsync(db, portId, ct) is null)
            return null;
        return await InventoryOperationJournal.ReadAttemptAsync(db,
            InventoryOperationJournal.ForEmployee(actor, attemptId, PortScope(portId), "net.poe"), ct);
    }

    /// <summary>Перезапускает PoE после авторизации и долговечной фиксации начала внешней команды.</summary>
    public static async Task ResetPoeJournaledAsync(DbManager db, string? principal, int employeeId, int portId,
        Guid attemptId, CancellationToken ct)
    {
        var actor = await AuthorizeAsync(ct);
        var attempt = InventoryOperationJournal.ForEmployee(actor, attemptId, PortScope(portId), "net.poe");
        var receipt = await InventoryOperationJournal.RunExternalAsync<PoeAuthorizationException>(
            db, attempt, async token =>
            {
                await AuthorizeAsync(token);
                token.ThrowIfCancellationRequested();
                await ResetPoeAsync(db, portId);
                return portId;
            }, ct);
        if (receipt.State == InventoryOperationState.Rejected)
            throw new UnauthorizedAccessException("Эта попытка перезапуска PoE уже отклонена.");

        async Task<Employee> AuthorizeAsync(CancellationToken token)
        {
            var employee = await Employee.GetCurrentAsync(db, principal, token);
            if (employee is null || employee.Id != employeeId || (await GetAsync(db, portId, token))?.Poe != 1)
                throw new PoeAuthorizationException();
            return employee;
        }
    }

    private static string PortScope(int id) => "net/" + id.ToString(CultureInfo.InvariantCulture);

    private sealed class PoeAuthorizationException()
        : UnauthorizedAccessException("Не удалось подтвердить доступ к перезапуску PoE этого порта.");

    /// <summary>Выполняет процедуру PoE с установленным ограничением 120 секунд.</summary>
    internal static Task ResetPoeAsync(DbManager db, int id) => db.ExecuteAsync(
        SQLQueries.EXEC_ResetPoEport,
        new { КодЛокальнойСети = id },
        commandType: CommandType.Text,
        commandTimeout: 120);
}

/// <summary>Соседний порт с готовой legacy-подписью.</summary>
public sealed class LocalNetworkNeighborPort : ILookupEntity
{
    /// <summary>Идентификатор порта.</summary>
    [Column(LocalNetworkPortColumns.КодЛокальнойСети)]
    public int Id { get; set; }

    /// <summary>Подпись «СетевоеИмя, порт:N».</summary>
    [Column(LocalNetworkPortColumns.Строка)]
    public string Name { get; set; } = string.Empty;
}
