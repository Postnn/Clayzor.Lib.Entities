using System.Data.Common;
using System.Text.RegularExpressions;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Три пользовательских поля FMTN с неизменёнными строковыми значениями.</summary>
public sealed record SimFmtnFields(string Prefix, string ShortNumber, string Description)
{
    /// <summary>Проверка формата legacy. Допустимые поля не преобразуются в числа.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (Prefix is "" or "0") errors.Add("Необходимо указать префикс FMTN!");
        else if (Prefix.Length > 11) errors.Add("Префикс FMTN не должен превышать 11 символов.");
        else if (Prefix.Any(c => !char.IsAsciiDigit(c) && c is not ('(' or ')' or '_')))
            errors.Add("Префикс FMTN может содержать только цифры, круглые скобки и знак подчеркивания");
        if (ShortNumber.Length > 20) errors.Add("Короткий номер не должен превышать 20 символов.");
        else if (ShortNumber.Length > 0 && !Regex.IsMatch(ShortNumber, @"\A\d+\z", RegexOptions.CultureInvariant))
            errors.Add("Короткий номер может содержать только цифры");
        if (Description.Length > 50) errors.Add("Описание не должно превышать 50 символов.");
        return errors;
    }

    /// <summary>Проверяет ограничение активного триггера для подтверждённого обычного SQL-контекста.</summary>
    public bool FitsWritePolicy => Prefix.Length == 11 && Prefix.All(char.IsAsciiDigit) &&
        ShortNumber.Length == 4 && ShortNumber.StartsWith("51", StringComparison.Ordinal) && ShortNumber.All(char.IsAsciiDigit);
}

/// <summary>Сохранённые поля и аудит записи.</summary>
public sealed record SimFmtn
{
    /// <summary>Исходный строковый ключ.</summary>
    public string Prefix { get; init; } = "";
    /// <summary>Короткий номер.</summary>
    public string ShortNumber { get; init; } = "";
    /// <summary>Описание из БД с учётом триггера.</summary>
    public string Description { get; init; } = "";
    /// <summary>Автор последнего изменения.</summary>
    public int ChangedBy { get; init; }
    /// <summary>Подпись автора.</summary>
    public string? ChangedByName { get; init; }
    /// <summary>Дата изменения UTC.</summary>
    public DateTime ChangedAt { get; init; }
    /// <summary>Поля редактора без аудита.</summary>
    public SimFmtnFields Fields => new(Prefix, ShortNumber, Description);
}

/// <summary>Эффективные разрешения на представление FMTN.</summary>
public sealed record SimFmtnAccess
{
    /// <summary>Чтение.</summary>
    public bool CanRead { get; init; }
    /// <summary>Создание.</summary>
    public bool CanInsert { get; init; }
    /// <summary>Обновление.</summary>
    public bool CanUpdate { get; init; }
    /// <summary>Удаление.</summary>
    public bool CanDelete { get; init; }
}

/// <summary>Доказанный отказ до подтверждения транзакции FMTN.</summary>
public sealed class SimFmtnConflict(string message) : InvalidOperationException(message);

/// <summary>Операции над FMTN через штатное представление и триггер.</summary>
public static class SimFmtnData
{
    private const string Lock = "WITH (UPDLOCK,HOLDLOCK)";

    /// <summary>Получает права для проверенного снимка сотрудника.</summary>
    public static async Task<SimFmtnAccess> ReadAccessAsync(DbManager db, Employee? user, CancellationToken ct = default)
    {
        if (user?.HasRole(Роли.Администратор_тарифов_связи) != true) return new();
        return (await db.QueryRequiredAsync<SimFmtnAccess>(SQLQueries.SELECT_SimFmtnAccess, cancellationToken: ct)).Single();
    }

    /// <summary>Читает один снимок, различая отсутствие и ошибку.</summary>
    public static async Task<SimFmtn?> ReadAsync(DbManager db, string key, CancellationToken ct = default) =>
        (await db.QueryRequiredAsync<SimFmtn>(string.Format(SQLQueries.SELECT_SimFmtn, ""), new { Key = Ansi(key, 20) }, ct)).SingleOrDefault();

    /// <summary>Отдельно вставляет или обновляет и подтверждает актуальный ключ до commit.</summary>
    public static async Task<SimFmtn> SaveAsync(DbManager db, string? principal, SimFmtn? original, SimFmtnFields draft, CancellationToken ct = default)
    {
        ValidateSave(original, draft);
        var access = await GetFreshAccessAsync(db, principal, ct);
        if (!(original is null ? access.CanInsert : access.CanUpdate)) throw new UnauthorizedAccessException("Нет права на запись FMTN.");

        return await db.RunDbAsync(async connection =>
        {
            await using var transaction = await connection.BeginTransactionAsync(ct);
            var saved = await SaveRowsAsync(connection, transaction, original, draft, ct);
            await transaction.CommitAsync(CancellationToken.None);
            return saved;
        });
    }

    /// <summary>Удаляет проверенную строку и убеждается в её отсутствии до commit.</summary>
    public static async Task DeleteAsync(DbManager db, string? principal, SimFmtn original, CancellationToken ct = default)
    {
        if (!original.Fields.FitsWritePolicy) throw PolicyError();
        if (!(await GetFreshAccessAsync(db, principal, ct)).CanDelete) throw new UnauthorizedAccessException("Нет права на удаление FMTN.");
        await db.RunDbAsync(async connection =>
        {
            await using var transaction = await connection.BeginTransactionAsync(ct);
            await DeleteRowAsync(connection, transaction, original, ct);
            await transaction.CommitAsync(CancellationToken.None);
            return true;
        });
    }

    private static async Task<SimFmtn> SaveRowsAsync(DbConnection connection, DbTransaction transaction,
        SimFmtn? original, SimFmtnFields draft, CancellationToken ct)
    {
        var key = original?.Prefix ?? draft.Prefix;
        EnsureOriginal(await ReadRowAsync(connection, transaction, key, Lock, ct), original);
        await connection.ExecuteAsync(new CommandDefinition(original is null ? SQLQueries.INSERT_SimFmtn : SQLQueries.UPDATE_SimFmtn,
            new { Key = Ansi(key, 20), Prefix = Ansi(draft.Prefix, 20), ShortNumber = Ansi(draft.ShortNumber, 20), Description = Ansi(draft.Description, 50) },
            transaction, cancellationToken: ct));
        var saved = await ReadRowAsync(connection, transaction, draft.Prefix, "", ct);
        if (saved is null || saved.Prefix != draft.Prefix || saved.ShortNumber != draft.ShortNumber)
            throw new SimFmtnConflict("База не подтвердила сохранение FMTN.");
        return saved;
    }

    private static async Task DeleteRowAsync(DbConnection connection, DbTransaction transaction, SimFmtn original, CancellationToken ct)
    {
        EnsureOriginal(await ReadRowAsync(connection, transaction, original.Prefix, Lock, ct), original);
        await connection.ExecuteAsync(new CommandDefinition(SQLQueries.DELETE_SimFmtn, new { Key = Ansi(original.Prefix, 20) },
            transaction, cancellationToken: ct));
        if (await ReadRowAsync(connection, transaction, original.Prefix, "", ct) is not null)
            throw new SimFmtnConflict("База не подтвердила удаление FMTN.");
    }

    private static Task<SimFmtn?> ReadRowAsync(DbConnection connection, DbTransaction transaction, string key,
        string locking, CancellationToken ct) => connection.QuerySingleOrDefaultAsync<SimFmtn>(new CommandDefinition(
            string.Format(SQLQueries.SELECT_SimFmtn, locking), new { Key = Ansi(key, 20) }, transaction, cancellationToken: ct));

    private static async Task<SimFmtnAccess> ReadWriteAccessAsync(DbConnection connection, DbTransaction transaction,
        string? principal, int employeeId, CancellationToken ct)
    {
        var actor = await Employee.GetCurrentWithRolesAsync(connection, transaction, principal, ct);
        if (actor is null || actor.Id != employeeId || !actor.HasRole(Роли.Администратор_тарифов_связи))
            throw new SimFmtnConflict("Права сотрудника изменились. Запись не отправлена.");
        return await connection.QuerySingleAsync<SimFmtnAccess>(new CommandDefinition(
            SQLQueries.SELECT_SimFmtnAccess, transaction: transaction, cancellationToken: ct));
    }

    private static Employee RequireActor(Employee? actor, int employeeId) =>
        actor is not null && actor.Id == employeeId && actor.HasRole(Роли.Администратор_тарифов_связи) ? actor :
            throw new UnauthorizedAccessException("Сотрудник или право доступа к FMTN не подтверждены.");

    private static bool ValidPrefixKey(string? key) => key is { Length: > 0 and <= 20 } &&
        key.All(c => char.IsAsciiDigit(c) || c is '(' or ')' or '_');

    private static void EnsureOriginal(SimFmtn? current, SimFmtn? original)
    {
        if (current != original)
            throw new SimFmtnConflict(original is null ? "Префикс уже существует." : "Запись изменилась или удалена. Обновите форму.");
    }

    private static void ValidateSave(SimFmtn? original, SimFmtnFields draft)
    {
        var errors = draft.Validate();
        if (errors.Count != 0) throw new SimFmtnConflict(string.Join(" ", errors));
        if (!draft.FitsWritePolicy || original is not null && !original.Fields.FitsWritePolicy) throw PolicyError();
    }

    private static async Task<SimFmtnAccess> GetFreshAccessAsync(DbManager db, string? principal, CancellationToken ct) =>
        await ReadAccessAsync(db, await Employee.GetCurrentWithRolesAsync(db, principal, ct), ct);

    private static DbString Ansi(string value, int size) => new() { Value = value, IsAnsi = true, Length = size };
    private static SimFmtnConflict PolicyError() => new("База разрешает изменять FMTN Ставрополя: 11 цифр и короткий номер 51XX.");
}
