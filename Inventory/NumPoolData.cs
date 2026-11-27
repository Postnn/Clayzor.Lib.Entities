using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Шапка карточки с исходным строковым номером ТфОП и аудитом.</summary>
public sealed record NumPoolHeader
{
    public string Number { get; init; } = "";
    public int? CapacityId { get; init; }
    public string? IncomingFor { get; init; }
    public string? IncomingTo { get; init; }
    public decimal? Control { get; init; } = 0;
    public string? Notes { get; init; }
    public int ChangedBy { get; init; }
    public string? ChangedByName { get; init; }
    public DateTime? ChangedAt { get; init; }
}

/// <summary>Виды разрешённых справочников и связей номера ТфОП.</summary>
public enum NumPoolKind { Capacity, Connection, Employee, Customer, Phone }

/// <summary>Справочное значение для добавления в карточку.</summary>
public sealed record NumPoolOption(int Id, string Name);

/// <summary>Связь с независимым ключом, признаком Private и аудитом.</summary>
public sealed record NumPoolLink(int Id, string Name, byte Private, int ChangedBy, string? ChangedByName, DateTime? ChangedAt);

/// <summary>Снимок пяти частей карточки номера ТфОП.</summary>
public sealed record NumPoolSnapshot(NumPoolHeader Header, IReadOnlyList<NumPoolLink> Connections, IReadOnlyList<NumPoolLink> Employees,
    IReadOnlyList<NumPoolLink> Customers, IReadOnlyList<NumPoolLink> Phones)
{
    public static NumPoolSnapshot Empty { get; } = new(new(), [], [], [], []);

    public IReadOnlyList<NumPoolLink> Links(NumPoolKind kind) => kind switch
    {
        NumPoolKind.Connection => Connections, NumPoolKind.Employee => Employees,
        NumPoolKind.Customer => Customers, NumPoolKind.Phone => Phones,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public NumPoolSnapshot SetLinks(NumPoolKind kind, IReadOnlyList<NumPoolLink> links) => kind switch
    {
        NumPoolKind.Connection => this with { Connections = links }, NumPoolKind.Employee => this with { Employees = links },
        NumPoolKind.Customer => this with { Customers = links }, NumPoolKind.Phone => this with { Phones = links },
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

/// <summary>Разрешения SQL на операции с родительской записью.</summary>
public sealed record NumPoolAccess(bool CanRead, bool CanInsert, bool CanUpdate, bool CanDelete);

/// <summary>Проверяемый отказ до подтверждения транзакции.</summary>
public sealed class NumPoolConflict(string message) : InvalidOperationException(message);

/// <summary>Сервер не смог доказать исход записи. Повтор должен ждать точной сверки.</summary>
public sealed class NumPoolUnknownException(Exception inner) : InvalidOperationException("Исход записи номера ТфОП требует сверки.", inner);

/// <summary>Чтение и атомарные операции над всей карточкой ТфОП.</summary>
public static class NumPoolData
{
    private const int BatchSize = 300;
    private const string PoolWriteScope = "numpool";
    private const string NumberScopePrefix = "numpool-number/";
    private const string SaveKind = "numpool.save";
    private const string DeleteKind = "numpool.delete";
    private static readonly LinkTable[] Tables =
    [
        new(NumPoolKind.Connection, "vwПодключенияТелефонииНомераТфОП", "КодПодключенияТелефонии"),
        new(NumPoolKind.Employee, "vwНомераТфОП_Сотрудники", "КодСотрудника"),
        new(NumPoolKind.Customer, "vwНомераТфОП_Лица", "КодЛица"),
        new(NumPoolKind.Phone, "vwНомераТфОП_Телефоны", "КодТелефонногоНомера"),
    ];

    /// <summary>Запрещает пустое значение и точный legacy-сентинел, сохраняя нули внутри ключа.</summary>
    public static bool ValidNumber(string? number) => number is { Length: > 0 and <= 20 } && number != "0";

    /// <summary>Проверяет ограничения полей, подтверждённые схемой SQL.</summary>
    public static IReadOnlyList<string> Validate(NumPoolHeader header)
    {
        var errors = new List<string>();
        if (!ValidNumber(header.Number)) errors.Add("Укажите номер ТфОП длиной до 20 символов.");
        if (header.CapacityId is null or <= 0) errors.Add("Выберите номерную ёмкость.");
        if (header.Control is not decimal control || control < byte.MinValue || control > byte.MaxValue || decimal.Truncate(control) != control)
            errors.Add("Укажите целое значение контроля от 0 до 255.");
        if (header.IncomingFor?.Length > 300 || header.IncomingTo?.Length > 50 || header.Notes?.Length > 300)
            errors.Add("Превышена допустимая длина входящих настроек или примечаний.");
        return errors;
    }

    /// <summary>Сравнивает редактируемое содержание; переименование справочника не меняет связь.</summary>
    public static bool SameValues(NumPoolSnapshot left, NumPoolSnapshot right) =>
        HeaderValues(left.Header) == HeaderValues(right.Header) && Tables.All(table =>
            LinkValues(left.Links(table.Kind)).SequenceEqual(LinkValues(right.Links(table.Kind))));

    /// <summary>Сравнивает исходную версию и семантическое содержимое для защиты от чужих записей.</summary>
    public static bool SameVersion(NumPoolSnapshot left, NumPoolSnapshot right) =>
        left.Header.ChangedAt == right.Header.ChangedAt && SameValues(left, right);

    private static (string, int?, string?, string?, decimal?, string?) HeaderValues(NumPoolHeader header) =>
        (header.Number, header.CapacityId, header.IncomingFor, header.IncomingTo, header.Control, header.Notes);

    private static IEnumerable<(int Id, byte Private)> LinkValues(IEnumerable<NumPoolLink> links) =>
        links.Select(link => (link.Id, link.Private)).OrderBy(link => link.Id);

    /// <summary>Вычисляет узкий diff. Изменение Private проходит как удаление и добавление той же связи.</summary>
    internal static (IReadOnlyList<int> Removed, IReadOnlyList<NumPoolLink> Added) Diff(IReadOnlyList<NumPoolLink> original, IReadOnlyList<NumPoolLink> draft)
    {
        var before = original.ToDictionary(link => link.Id, link => link.Private);
        var after = draft.ToDictionary(link => link.Id, link => link.Private);
        return (original.Where(link => !after.TryGetValue(link.Id, out var value) || value != link.Private).Select(link => link.Id).ToList(),
            draft.Where(link => !before.TryGetValue(link.Id, out var value) || value != link.Private).ToList());
    }

    /// <summary>Читает права доверенного сотрудника. Триггеры БД дополнительно ограничивают записи.</summary>
    public static async Task<NumPoolAccess> ReadAccessAsync(DbManager db, Employee? actor, CancellationToken ct) => actor is null
        ? new(false, false, false, false)
        : (await db.QueryRequiredAsync<NumPoolAccess>(SQLQueries.SELECT_NumPoolAccess, cancellationToken: ct)).Single();

    /// <summary>Загружает родителя, подключения, сотрудников, заказчиков и телефоны одним пакетом.</summary>
    public static Task<NumPoolSnapshot?> ReadAsync(DbManager db, string number, CancellationToken ct) =>
        db.RunDbAsync(connection => ReadAsync(connection, null, number, ct));

    /// <summary>Поиск или восстановление значения допустимого справочника.</summary>
    public static Task<IEnumerable<NumPoolOption>> SearchAsync(DbManager db, NumPoolKind kind, string search, int? id, int limit, CancellationToken ct) =>
        db.QueryRequiredAsync<NumPoolOption>(Format(SQLQueries.SELECT_NumPoolOptions, Source(kind)),
            new { Id = id, Pattern = $"%{search}%", Limit = limit }, ct);

    /// <summary>Сохраняет шапку и diff четырёх типов связей одной транзакцией.</summary>
    public static async Task<NumPoolSnapshot> SaveAsync(DbManager db, string? principal, NumPoolSnapshot? original,
        NumPoolSnapshot draft, CancellationToken ct)
    {
        EnsureValidProposal(original, draft);
        var access = await ReadFreshAccessAsync(db, principal, ct);
        if (!access.CanRead || !(original is null ? access.CanInsert : access.CanUpdate))
            throw new UnauthorizedAccessException("У вас нет права сохранить номер ТфОП.");

        return await db.RunDbAsync(async connection =>
        {
            await using var tx = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var saved = await SaveInTransactionAsync(connection, tx, original, draft, ct);
            await tx.CommitAsync(CancellationToken.None);
            return saved;
        });
    }

    /// <summary>Удаляет четыре набора и родителя в установленном порядке.</summary>
    public static async Task DeleteAsync(DbManager db, string? principal, NumPoolSnapshot original, CancellationToken ct)
    {
        var access = await ReadFreshAccessAsync(db, principal, ct);
        if (!access.CanRead || !access.CanDelete) throw new UnauthorizedAccessException("У вас нет права удалить номер ТфОП.");
        await db.RunDbAsync(async connection =>
        {
            await using var tx = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await DeleteInTransactionAsync(connection, tx, original, ct);
            await tx.CommitAsync(CancellationToken.None);
            return true;
        });
    }

    /// <summary>Сохраняет карточку и точный результат попытки в одной транзакции.</summary>
    public static async Task<NumPoolSnapshot> SaveJournaledAsync(DbManager db, string? principal, int employeeId,
        NumPoolSnapshot? original, NumPoolSnapshot draft, Guid attemptId, CancellationToken ct)
    {
        EnsureValidProposal(original, draft);
        var (actor, access) = await AuthorizedReadAsync(db, principal, employeeId, ct);
        EnsureWriteAccess(access, original is null, deleting: false);
        var attempt = CreateAttempt(actor, draft.Header.Number, original, draft, attemptId, deleting: false);
        var current = await ReadAsync(db, draft.Header.Number, ct);
        if (await InventoryOperationJournal.ReadAttemptAsync(db, attempt, ct) is { } previous)
        {
            EnsureConfirmed(previous);
            return current ?? throw new InvalidOperationException("Сохранение подтверждено, но номер ТфОП сейчас недоступен.");
        }
        EnsureOriginal(original, current);

        NumPoolSnapshot? saved = null;
        await RunJournaledAsync(db, attempt, principal, employeeId, original is null, deleting: false,
            async (connection, tx, token) => { saved = await SaveInTransactionAsync(connection, tx, original, draft, token); }, ct);
        return saved ?? await ReadAsync(db, draft.Header.Number, ct)
            ?? throw new InvalidOperationException("Сохранение подтверждено, но номер ТфОП сейчас недоступен.");
    }

    /// <summary>Удаляет карточку и записывает подтверждение в той же транзакции.</summary>
    public static async Task DeleteJournaledAsync(DbManager db, string? principal, int employeeId,
        NumPoolSnapshot original, Guid attemptId, CancellationToken ct)
    {
        var (actor, access) = await AuthorizedReadAsync(db, principal, employeeId, ct);
        EnsureWriteAccess(access, inserting: false, deleting: true);
        var attempt = CreateAttempt(actor, original.Header.Number, original, null, attemptId, deleting: true);
        if (await InventoryOperationJournal.ReadAttemptAsync(db, attempt, ct) is { } previous)
        {
            EnsureConfirmed(previous);
            return;
        }
        EnsureOriginal(original, await ReadAsync(db, original.Header.Number, ct));
        await RunJournaledAsync(db, attempt, principal, employeeId, inserting: false, deleting: true,
            (connection, tx, token) => DeleteInTransactionAsync(connection, tx, original, token), ct);
    }

    /// <summary>Проверяет блокировку записи в справочник после подтверждения SQL-доступа.</summary>
    public static async Task<bool> ReadWritePendingAsync(DbManager db, string? principal, int employeeId, CancellationToken ct)
    {
        await AuthorizedReadAsync(db, principal, employeeId, ct);
        return await InventoryOperationJournal.IsScopePendingAsync(db, PoolWriteScope, ct);
    }

    /// <summary>Переносит только сведения о прежних попытках с проверенными номерами и телефонными связями.</summary>
    public static async Task ImportLegacyOperationsAsync(DbManager db, string? principal, int employeeId, string? routeNumber,
        IReadOnlyCollection<(Guid AttemptId, string Kind, string Number, IReadOnlyCollection<int> PhoneIds)> evidence,
        CancellationToken ct)
    {
        if (evidence.Count == 0) return;
        var (actor, access) = await AuthorizedReadAsync(db, principal, employeeId, ct);
        if (routeNumber is not null && (!ValidNumber(routeNumber) ||
            await ReadAsync(db, routeNumber, ct) is { } current && current.Header.Number != routeNumber))
            throw new NumPoolConflict("Не удалось подтвердить ключ прежней операции.");

        var attempts = new List<InventoryOperationJournal.Attempt>(evidence.Count);
        var linkedPhones = new HashSet<int>();
        foreach (var (attemptId, kind, number, phoneIds) in evidence)
        {
            var deleting = kind == DeleteKind;
            if (attemptId == Guid.Empty || kind is not (SaveKind or DeleteKind) ||
                !ValidNumber(number) || number.Any(char.IsControl) ||
                routeNumber is not null && number != routeNumber || routeNumber is null && deleting ||
                phoneIds.Any(id => id <= 0) || phoneIds.Distinct().Count() != phoneIds.Count)
                throw new NumPoolConflict("Не удалось подтвердить область прежней операции с номером ТфОП.");
            EnsureWriteAccess(access, routeNumber is null, deleting);
            attempts.Add(CreateAttempt(actor, number, phoneIds, attemptId, deleting));
            linkedPhones.UnionWith(phoneIds);
        }

        foreach (var keys in linkedPhones.Chunk(BatchSize))
        {
            var actual = await db.QueryRequiredAsync<int>(
                Format(SQLQueries.SELECT_NumPoolLinkKeys, Source(NumPoolKind.Phone)), new { Keys = keys }, ct);
            if (!keys.ToHashSet().SetEquals(actual))
                throw new NumPoolConflict("Телефонные связи прежней операции не подтверждены.");
        }
        await InventoryOperationJournal.ImportLegacyAsync(db, attempts, ct);
    }

    /// <summary>Читает точный исход своей попытки, сверяя вид действия и ключ отправленного номера.</summary>
    public static async Task<InventoryOperationReceipt?> ReadWriteReceiptAsync(DbManager db, string? principal, int employeeId,
        string number, bool deleting, Guid attemptId, CancellationToken ct)
    {
        if (!ValidNumber(number) || attemptId == Guid.Empty) return null;
        var (actor, _) = await AuthorizedReadAsync(db, principal, employeeId, ct);
        var attempt = InventoryOperationJournal.ForEmployee(actor, attemptId, PoolWriteScope, deleting ? DeleteKind : SaveKind);
        var receipt = await InventoryOperationJournal.ReadAttemptAsync(db, attempt, ct);
        if (receipt is null) return null;
        var scopes = await db.RunDbAsync(connection => connection.QueryAsync<string>(new CommandDefinition(
            SQLQueries.SELECT_ClayzorOperationScope, new { AttemptId = attemptId }, cancellationToken: ct)));
        return scopes.Contains(NumberScope(number), StringComparer.Ordinal) ? receipt : null;
    }

    private static async Task RunJournaledAsync(DbManager db, InventoryOperationJournal.Attempt attempt, string? principal,
        int employeeId, bool inserting, bool deleting, Func<DbConnection, DbTransaction, CancellationToken, Task> write,
        CancellationToken ct)
    {
        try
        {
            var receipt = await InventoryOperationJournal.RunSqlAsync<NumPoolConflict>(db, attempt,
                async (connection, tx, token) =>
                {
                    var actor = await Employee.GetCurrentAsync(connection, tx, principal, token);
                    if (actor?.Id != employeeId)
                        throw new NumPoolConflict("Текущий сотрудник не подтверждён.");
                    var access = await connection.QuerySingleAsync<NumPoolAccess>(
                        new CommandDefinition(SQLQueries.SELECT_NumPoolAccess, transaction: tx, cancellationToken: token));
                    EnsureWriteAccess(access, inserting, deleting);
                    await write(connection, tx, token);
                    return null;
                }, ct);
            EnsureConfirmed(receipt);
        }
        catch (Exception error) when (error is not NumPoolConflict and not InventoryOperationPendingException
            and not OperationCanceledException)
        {
            throw new NumPoolUnknownException(error);
        }
    }

    private static void EnsureConfirmed(InventoryOperationReceipt receipt)
    {
        if (receipt.State == InventoryOperationState.Rejected)
            throw new NumPoolConflict("Предыдущая запись номера ТфОП отклонена.");
        if (receipt.State != InventoryOperationState.Confirmed)
            throw new InventoryOperationPendingException();
    }

    private static async Task<(Employee Actor, NumPoolAccess Access)> AuthorizedReadAsync(DbManager db, string? principal,
        int employeeId, CancellationToken ct)
    {
        var actor = await Employee.GetCurrentAsync(db, principal, ct);
        if (actor is null || actor.Id != employeeId) throw new UnauthorizedAccessException("Текущий сотрудник не подтверждён.");
        var access = await ReadAccessAsync(db, actor, ct);
        if (!access.CanRead) throw new UnauthorizedAccessException("Нет доступа к справочнику номеров ТфОП.");
        return (actor, access);
    }

    private static void EnsureWriteAccess(NumPoolAccess access, bool inserting, bool deleting)
    {
        if (!access.CanRead || !(deleting ? access.CanDelete : inserting ? access.CanInsert : access.CanUpdate))
            throw new NumPoolConflict(deleting ? "У вас нет права удалить номер ТфОП." : "У вас нет права сохранить номер ТфОП.");
    }

    private static InventoryOperationJournal.Attempt CreateAttempt(Employee actor, string number, NumPoolSnapshot? original,
        NumPoolSnapshot? current, Guid attemptId, bool deleting)
    {
        // varchar сравнивается по collation БД. До INSERT разные строки клиента могут обозначать один ключ.
        var (removed, added) = Diff(original?.Phones ?? [], current?.Phones ?? []);
        return CreateAttempt(actor, number, removed.Concat(added.Select(link => link.Id)), attemptId, deleting);
    }

    private static InventoryOperationJournal.Attempt CreateAttempt(Employee actor, string number, IEnumerable<int> changedPhoneIds,
        Guid attemptId, bool deleting)
    {
        var related = changedPhoneIds.Select(id =>
            "tel/" + id.ToString(CultureInfo.InvariantCulture)).Append(NumberScope(number)).Distinct(StringComparer.Ordinal).ToArray();
        return InventoryOperationJournal.ForEmployee(actor, attemptId, PoolWriteScope, deleting ? DeleteKind : SaveKind, related);
    }

    private static string NumberScope(string number) =>
        NumberScopePrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(number)));

    private static void EnsureValidProposal(NumPoolSnapshot? original, NumPoolSnapshot draft)
    {
        var errors = Validate(draft.Header);
        if (errors.Count != 0) throw new NumPoolConflict(string.Join(" ", errors));
        if (original is not null && (original.Header.Number != draft.Header.Number || original.Header.CapacityId != draft.Header.CapacityId))
            throw new NumPoolConflict("Номер и номерная ёмкость существующей карточки неизменны.");
        foreach (var table in Tables)
        {
            var links = draft.Links(table.Kind);
            if (links.Any(link => link.Id <= 0) || links.Select(link => link.Id).Distinct().Count() != links.Count)
                throw new NumPoolConflict("В списке использования есть неверный или повторяющийся ключ.");
            if (links.Any(link => link.Private > 1 || table.Kind != NumPoolKind.Employee && link.Private != 0))
                throw new NumPoolConflict("Недопустимый признак Private.");
        }
    }

    private static async Task<NumPoolSnapshot> SaveInTransactionAsync(DbConnection connection, DbTransaction tx,
        NumPoolSnapshot? original, NumPoolSnapshot draft, CancellationToken ct)
    {
        var number = draft.Header.Number;
        var current = await ReadAsync(connection, tx, number, ct);
        EnsureOriginal(original, current);
        if (original is null)
            await EnsureKeysAsync(connection, tx, NumPoolKind.Capacity, [draft.Header.CapacityId!.Value], ct);
        var header = await SaveHeaderAsync(connection, tx, original?.Header, draft.Header, ct);
        foreach (var table in Tables)
        {
            var (removed, added) = Diff(current?.Links(table.Kind) ?? [], draft.Links(table.Kind));
            await SaveLinksAsync(connection, tx, number, table, removed, added, ct);
        }
        var saved = await ReadAsync(connection, tx, number, ct);
        if (saved is null || !SameValues(saved, draft with { Header = header }))
            throw new NumPoolConflict("База не подтвердила полное сохранение номера и его использования.");
        return saved;
    }

    private static async Task DeleteInTransactionAsync(DbConnection connection, DbTransaction tx, NumPoolSnapshot original,
        CancellationToken ct)
    {
        var number = original.Header.Number;
        EnsureOriginal(original, await ReadAsync(connection, tx, number, ct));
        var id = new { Id = Ansi(number, 20) };
        foreach (var table in Tables)
            await connection.ExecuteAsync(new CommandDefinition(Format(SQLQueries.DELETE_NumPoolAllLinks, table.Name), id, tx, cancellationToken: ct));
        await connection.ExecuteAsync(new CommandDefinition(SQLQueries.DELETE_NumPoolHeader, id, tx, cancellationToken: ct));
        if (await ReadAsync(connection, tx, number, ct) is not null)
            throw new NumPoolConflict("База не подтвердила удаление номера ТфОП.");
    }

    private static async Task<NumPoolSnapshot?> ReadAsync(DbConnection connection, DbTransaction? tx, string number, CancellationToken ct)
    {
        var sql = string.Join(Environment.NewLine, Format(SQLQueries.SELECT_NumPoolHeader, tx is null ? "" : "WITH (UPDLOCK,HOLDLOCK)"),
            SQLQueries.SELECT_NumPoolConnections, SQLQueries.SELECT_NumPoolEmployees, SQLQueries.SELECT_NumPoolCustomers, SQLQueries.SELECT_NumPoolPhones);
        using var batch = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { Id = Ansi(number, 20) }, tx, cancellationToken: ct));
        var header = await batch.ReadSingleOrDefaultAsync<NumPoolHeader>();
        var connections = (await batch.ReadAsync<NumPoolLink>()).AsList();
        var employees = (await batch.ReadAsync<NumPoolLink>()).AsList();
        var customers = (await batch.ReadAsync<NumPoolLink>()).AsList();
        var phones = (await batch.ReadAsync<NumPoolLink>()).AsList();
        return header is null ? null : new(header, connections, employees, customers, phones);
    }

    internal static async Task<NumPoolHeader> SaveHeaderAsync(DbConnection connection, DbTransaction tx,
        NumPoolHeader? original, NumPoolHeader draft, CancellationToken ct)
    {
        if (original is null)
        {
            var created = draft with { IncomingFor = draft.IncomingFor ?? " ", IncomingTo = draft.IncomingTo ?? " ", Notes = draft.Notes ?? " " };
            await WriteHeaderAsync(connection, tx, SQLQueries.INSERT_NumPoolHeader, created, ct);
            return created;
        }
        if (HeaderValues(original) != HeaderValues(draft))
            await WriteHeaderAsync(connection, tx, SQLQueries.UPDATE_NumPoolHeader, draft, ct);
        return draft;
    }

    private static Task<int> WriteHeaderAsync(DbConnection connection, DbTransaction tx, string sql, NumPoolHeader header, CancellationToken ct) =>
        connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = Ansi(header.Number, 20), header.CapacityId, IncomingFor = Ansi(header.IncomingFor, 300),
            IncomingTo = Ansi(header.IncomingTo, 50), Notes = Ansi(header.Notes, 300), Control = (byte)header.Control!.Value,
        }, tx, cancellationToken: ct));

    private static async Task SaveLinksAsync(DbConnection connection, DbTransaction tx, string number, LinkTable table,
        IReadOnlyList<int> removed, IReadOnlyList<NumPoolLink> added, CancellationToken ct)
    {
        foreach (var keys in removed.Chunk(BatchSize))
            await connection.ExecuteAsync(new CommandDefinition(Format(SQLQueries.DELETE_NumPoolLinks, table.Name, table.Key),
                new { Id = Ansi(number, 20), Keys = keys }, tx, cancellationToken: ct));
        foreach (var batch in added.Chunk(BatchSize))
        {
            await EnsureKeysAsync(connection, tx, table.Kind, batch.Select(link => link.Id).ToArray(), ct);
            var (sql, parameters) = BuildLinkInsert(number, table, batch);
            await connection.ExecuteAsync(new CommandDefinition(sql, parameters, tx, cancellationToken: ct));
        }
    }

    internal static (string Sql, DynamicParameters Parameters) BuildLinkInsert(string number, NumPoolKind kind, NumPoolLink[] batch) =>
        BuildLinkInsert(number, Tables.Single(table => table.Kind == kind), batch);

    private static (string Sql, DynamicParameters Parameters) BuildLinkInsert(string number, LinkTable table, NumPoolLink[] batch)
    {
        var isEmployee = table.Kind == NumPoolKind.Employee;
        var parameters = new DynamicParameters(new { Id = Ansi(number, 20) });
        var values = new string[batch.Length];
        for (var i = 0; i < batch.Length; i++)
        {
            parameters.Add($"Link{i}", batch[i].Id);
            if (isEmployee) parameters.Add($"Private{i}", batch[i].Private, DbType.Byte);
            values[i] = isEmployee ? $"(@Id,@Link{i},@Private{i})" : $"(@Id,@Link{i})";
        }
        return (Format(SQLQueries.INSERT_NumPoolLinks, table.Name, table.Key, isEmployee ? ",Private" : "", string.Join(',', values)), parameters);
    }

    private static async Task EnsureKeysAsync(DbConnection connection, DbTransaction tx, NumPoolKind kind, int[] keys, CancellationToken ct)
    {
        var ids = await connection.QueryAsync<int>(new CommandDefinition(Format(SQLQueries.SELECT_NumPoolLinkKeys, Source(kind)),
            new { Keys = keys }, tx, cancellationToken: ct));
        if (!keys.ToHashSet().SetEquals(ids)) throw new NumPoolConflict("Выбранная ёмкость или связь больше недоступна.");
    }

    private static void EnsureOriginal(NumPoolSnapshot? original, NumPoolSnapshot? current)
    {
        if (original is null ? current is not null : current is null || !SameVersion(original, current))
            throw new NumPoolConflict("Номер или его использование изменились. Обновите карточку перед записью.");
    }

    private static async Task<NumPoolAccess> ReadFreshAccessAsync(DbManager db, string? principal, CancellationToken ct) =>
        await ReadAccessAsync(db, await Employee.GetCurrentAsync(db, principal, ct), ct);

    private static string Source(NumPoolKind kind) => kind switch
    {
        NumPoolKind.Capacity => SQLQueries.NumPoolCapacitySource, NumPoolKind.Connection => SQLQueries.NumPoolConnectionSource,
        NumPoolKind.Employee => SQLQueries.NumPoolEmployeeSource, NumPoolKind.Customer => SQLQueries.NumPoolCustomerSource,
        NumPoolKind.Phone => SQLQueries.NumPoolPhoneSource, _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static DbString Ansi(string? value, int length) => new() { Value = value, IsAnsi = true, Length = length };
    private static string Format(string format, params object[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
    private sealed record LinkTable(NumPoolKind Kind, string Name, string Key);
}
