using System.Data;
using System.Data.Common;
using System.Globalization;
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

/// <summary>Чтение и атомарные операции над всей карточкой ТфОП.</summary>
public static class NumPoolData
{
    private const int BatchSize = 300;
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
        var errors = Validate(draft.Header);
        if (errors.Count != 0) throw new NumPoolConflict(string.Join(" ", errors));
        if (original is not null && (original.Header.Number != draft.Header.Number || original.Header.CapacityId != draft.Header.CapacityId))
            throw new NumPoolConflict("Номер и номерная ёмкость существующей карточки неизменны.");
        var access = await ReadFreshAccessAsync(db, principal, ct);
        if (!access.CanRead || !(original is null ? access.CanInsert : access.CanUpdate))
            throw new UnauthorizedAccessException("У вас нет права сохранить номер ТфОП.");

        return await db.RunDbAsync(async connection =>
        {
            await using var tx = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var number = draft.Header.Number;
            var current = await ReadAsync(connection, tx, number, ct);
            EnsureOriginal(original, current);
            if (original is null)
                await EnsureKeysAsync(connection, tx, NumPoolKind.Capacity, [draft.Header.CapacityId!.Value], ct);
            var header = await SaveHeaderAsync(connection, tx, original?.Header, draft.Header, ct);

            foreach (var table in Tables)
            {
                var links = draft.Links(table.Kind);
                if (links.Any(link => link.Id <= 0) || links.Select(link => link.Id).Distinct().Count() != links.Count)
                    throw new NumPoolConflict("В списке использования есть неверный или повторяющийся ключ.");
                var (removed, added) = Diff(current?.Links(table.Kind) ?? [], links);
                if (added.Any(link => link.Private > 1 || table.Kind != NumPoolKind.Employee && link.Private != 0))
                    throw new NumPoolConflict("Недопустимый признак Private.");
                await SaveLinksAsync(connection, tx, number, table, removed, added, ct);
            }

            var saved = await ReadAsync(connection, tx, number, ct);
            if (saved is null || !SameValues(saved, draft with { Header = header }))
                throw new NumPoolConflict("База не подтвердила полное сохранение номера и его использования.");
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
            var number = original.Header.Number;
            EnsureOriginal(original, await ReadAsync(connection, tx, number, ct));
            var id = new { Id = Ansi(number, 20) };
            foreach (var table in Tables)
                await connection.ExecuteAsync(new CommandDefinition(Format(SQLQueries.DELETE_NumPoolAllLinks, table.Name), id, tx, cancellationToken: ct));
            await connection.ExecuteAsync(new CommandDefinition(SQLQueries.DELETE_NumPoolHeader, id, tx, cancellationToken: ct));
            if (await ReadAsync(connection, tx, number, ct) is not null)
                throw new NumPoolConflict("База не подтвердила удаление номера ТфОП.");
            await tx.CommitAsync(CancellationToken.None);
            return true;
        });
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
