using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Clayzor.Lib.DALC;
using Clayzor.Lib.Entities.Inventory;
using Dapper;

namespace Clayzor.Lib.Entities.TerritorialEntity;

/// <summary>Наблюдаемые права на операции карточек под текущей SQL-идентичностью.</summary>
public sealed record TerritorialEntityAccess
{
    /// <summary>Разрешено изменять реквизиты территории.</summary>
    public bool TerritoryUpdate { get; init; }
    /// <summary>Разрешено создавать телефонный код.</summary>
    public bool PhoneInsert { get; init; }
    /// <summary>Разрешено изменять реквизиты телефонного кода.</summary>
    public bool PhoneUpdate { get; init; }
    /// <summary>Разрешено удалять телефонный код штатным DELETE.</summary>
    public bool PhoneDelete { get; init; }
}

/// <summary>Чтение и подтверждённые операции записи территории и телефонного кода.</summary>
public static class TerritorialEntityData
{
    private const string RowLock = "WITH (UPDLOCK,HOLDLOCK)";
    private static readonly Regex LookupTokens = new(
        "\"([^\"]+)\"|[-*'0-9A-ZА-ЯŠŽÕÄÖÜÉÀÈÙÂÊÎÔÛÇËÏŸÆæŒœßŇñ_]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly int[] PhoneTerritoryTypes =
    [
        (int)ТипыТерриитории.Страны, (int)ТипыТерриитории.Области_Штаты, (int)ТипыТерриитории.Населённые_пункты,
    ];

    /// <summary>Основной SELECT для схемы ClayTreeView.</summary>
    public static string TreeSource => TerritorialEntitySql.TreeSource;

    /// <summary>Читает территорию по числовому ключу. SQL-ошибка пробрасывается вызывающему коду.</summary>
    public static async Task<TerritorialEntityRecord?> ReadTerritoryAsync(DbManager db, int id, CancellationToken ct = default) =>
        (await db.QueryRequiredAsync<TerritorialEntityRecord>(string.Format(TerritorialEntitySql.TerritoryRead, ""), new { Id = id }, ct)).SingleOrDefault();

    /// <summary>Читает телефонный код по исходной строке.</summary>
    public static async Task<PhoneCodeEntityRecord?> ReadPhoneAsync(DbManager db, string code, CancellationToken ct = default) =>
        (await db.QueryRequiredAsync<PhoneCodeEntityRecord>(string.Format(TerritorialEntitySql.PhoneRead, ""), new { Code = Ansi(code, 20) }, ct)).SingleOrDefault();

    /// <summary>Читает справочник типов территорий в порядке подтверждённых ключей.</summary>
    public static Task<IEnumerable<TerritorialEntityOption>> ReadTerritoryTypesAsync(DbManager db, CancellationToken ct = default) =>
        db.QueryRequiredAsync<TerritorialEntityOption>(TerritorialEntitySql.TerritoryTypes, null, ct);

    /// <summary>Читает справочник типов телефонной связи, включая тип 0.</summary>
    public static Task<IEnumerable<TerritorialEntityOption>> ReadPhoneTypesAsync(DbManager db, CancellationToken ct = default) =>
        db.QueryRequiredAsync<TerritorialEntityOption>(TerritorialEntitySql.PhoneTypes, null, ct);

    /// <summary>Возвращает актуальный снимок прав после сопоставления Windows и SQL principals.</summary>
    public static async Task<TerritorialEntityAccess> ReadAccessAsync(DbManager db, string? principal, CancellationToken ct = default)
    {
        var employee = await Employee.GetCurrentWithRolesAsync(db, principal, ct);
        return await ReadAccessAsync(db, employee, ct);
    }

    /// <summary>Читает SQL-права для доверенного снимка сотрудника, уже загруженного для интерфейса.</summary>
    public static async Task<TerritorialEntityAccess> ReadAccessAsync(DbManager db, Employee? employee, CancellationToken ct = default)
    {
        if (employee is null) return new();
        var rights = (await db.QueryRequiredAsync<TerritorialEntityAccess>(TerritorialEntitySql.Access, null, ct)).Single();
        return employee.HasRole(Роли.Администратор_тарифов_связи) ? rights : rights with
        {
            PhoneInsert = false, PhoneUpdate = false, PhoneDelete = false,
        };
    }

    /// <summary>Находит территорию без фильтра типов для отображения исторического значения.</summary>
    public static async Task<TerritorialEntityLookup?> FindTerritoryAsync(DbManager db, int id, CancellationToken ct = default) =>
        (await db.QueryRequiredAsync<TerritorialEntityLookup>(TerritorialEntitySql.LookupById, new { Id = id }, ct)).SingleOrDefault();

    /// <summary>Ищет территории разрешённых типов по коду страны или словам в двух названиях.</summary>
    public static Task<IEnumerable<TerritorialEntityLookup>> SearchPhoneTerritoriesAsync(
        DbManager db, string search, int limit, CancellationToken ct = default)
    {
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
        search = search.Trim();
        var parameters = new DynamicParameters(new { Limit = limit, Types = PhoneTerritoryTypes });
        var sql = new System.Text.StringBuilder(TerritorialEntitySql.LookupSearch);
        if (search.Length > 0 && search.All(char.IsAsciiDigit))
        {
            if (search.Length > 20) throw new ArgumentException("Телефонный код страны не должен превышать 20 символов.", nameof(search));
            parameters.Add("CountryCode", search, DbType.AnsiString, size: 20);
            sql.Append(TerritorialEntitySql.CountryCodeSearch);
        }
        else if (search.Length > 0)
        {
            var words = LookupTokens.Matches(search)
                .Select(match => match.Groups[1].Success ? match.Groups[1].Value : match.Value).ToArray();
            if (words.Length == 0) words = [search];
            if (words.Length > 80) throw new ArgumentException("Слишком много отдельных слов в поиске.", nameof(search));
            for (var i = 0; i < words.Length; i++)
            {
                var escaped = words[i].Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
                parameters.Add($"word{i}", $"%{escaped}%", DbType.AnsiString);
            }
            sql.Append(TerritorialEntitySql.LookupWords(words.Length));
        }
        sql.Append(TerritorialEntitySql.LookupOrder);
        return db.QueryRequiredAsync<TerritorialEntityLookup>(sql.ToString(), parameters, ct);
    }

    /// <summary>Загружает один уровень с заданными корнями или все корни при отсутствии ограничения.</summary>
    public static Task<IEnumerable<TerritorialEntityTreeRow>> ReadTreeLevelAsync(
        DbManager db, int? parentId, IReadOnlyList<int>? roots, CancellationToken ct = default) =>
        db.QueryRequiredAsync<TerritorialEntityTreeRow>(TerritorialEntitySql.TreeLevel, new
        {
            ParentId = parentId, Restricted = roots is not null, Roots = roots?.ToArray() ?? [],
        }, ct);

    /// <summary>Читает полный счётчик и ограниченный результат фильтра ClayTree.</summary>
    /// <param name="db">Подключение приложения.</param>
    /// <param name="treePredicate">Предикат из серверного построителя фильтра ClayTree с фиксированной схемой ID и текста.</param>
    /// <param name="parameters">Значения параметров предиката.</param>
    /// <param name="roots">Разрешённые корни или null для полного дерева.</param>
    /// <param name="limit">Число совпадений без предков.</param>
    /// <param name="ct">Токен отмены.</param>
    public static Task<TerritorialEntityTreeSearch> ReadTreeFilterAsync(
        DbManager db, string treePredicate, DynamicParameters parameters, IReadOnlyList<int>? roots,
        int limit, CancellationToken ct = default)
    {
        var args = new DynamicParameters(parameters);
        args.Add("Restricted", roots is not null);
        args.Add("Roots", roots?.ToArray() ?? []);
        args.Add("Limit", limit);
        return db.RunDbAsync(async connection =>
        {
            var command = new CommandDefinition(TerritorialEntitySql.TreeFilter(treePredicate), args, cancellationToken: ct);
            using var results = await connection.QueryMultipleAsync(command);
            var total = await results.ReadSingleAsync<int>();
            var rows = (await results.ReadAsync<TerritorialEntityTreeRow>()).AsList();
            return new TerritorialEntityTreeSearch(total, rows);
        });
    }

    /// <summary>Обновляет поля территории. Parent и вложенные множества принадлежат базе.</summary>
    public static async Task<TerritorialEntityRecord> UpdateTerritoryAsync(DbManager db, string? principal,
        TerritorialEntityRecord original, TerritorialEntityRecord draft, CancellationToken ct = default)
    {
        if (draft.Id <= 0 || draft.Id != original.Id || draft.ParentId != original.ParentId)
            throw new TerritorialEntityRuleException("Исходная территория или родитель изменились.");
        CheckValidation(TerritorialEntityRules.Validate(draft));
        if (!(await ReadAccessAsync(db, principal, ct)).TerritoryUpdate) throw Denied();

        return await WriteAsync(db, async (connection, transaction) =>
        {
            var key = new { draft.Id };
            var current = await ReadSingleAsync<TerritorialEntityRecord>(connection, transaction,
                string.Format(TerritorialEntitySql.TerritoryRead, RowLock), key, ct);
            if (current != original) throw Changed();
            await connection.ExecuteAsync(new CommandDefinition(TerritorialEntitySql.TerritoryUpdate, new
            {
                draft.Id, Type = (int)draft.Type, Name = Ansi(draft.Name, 80), AlternateName = Ansi(draft.AlternateName, 80),
                Caption = Ansi(draft.Caption, 50), AlternateCaption = Ansi(draft.AlternateCaption, 50),
                Abbreviation = Ansi(draft.Abbreviation, 4), draft.CountryClassification,
                CountryCode = Ansi(draft.CountryCode, 20), Direction = Ansi(draft.Direction, 300),
            }, transaction, cancellationToken: ct));
            var saved = await ReadSingleAsync<TerritorialEntityRecord>(connection, transaction,
                string.Format(TerritorialEntitySql.TerritoryRead, ""), key, ct);
            if (saved is null || saved with { ChangedBy = draft.ChangedBy, ChangedByName = draft.ChangedByName, ChangedAt = draft.ChangedAt } != draft)
                throw new TerritorialEntityRuleException("База не подтвердила сохранение всех реквизитов территории.");
            return saved;
        }, ct);
    }

    /// <summary>Вставляет или обновляет телефонный код и подтверждает сохранённый строковый ключ в той же транзакции.</summary>
    public static async Task<PhoneCodeEntityRecord> SavePhoneAsync(DbManager db, string? principal,
        PhoneCodeEntityRecord? original, PhoneCodeEntityRecord draft, CancellationToken ct = default)
    {
        if (original is not null && !string.Equals(original.Code, draft.Code, StringComparison.Ordinal))
            throw new TerritorialEntityRuleException("Телефонный код существующей записи изменять нельзя.");
        CheckValidation(TerritorialEntityRules.Validate(draft));
        var rights = await ReadAccessAsync(db, principal, ct);
        if (!(original is null ? rights.PhoneInsert : rights.PhoneUpdate)) throw Denied();

        return await WriteAsync(db, async (connection, transaction) =>
        {
            var key = new { Code = Ansi(draft.Code, 20) };
            var current = await ReadSingleAsync<PhoneCodeEntityRecord>(connection, transaction,
                string.Format(TerritorialEntitySql.PhoneRead, RowLock), key, ct);
            if (original is null && current is not null) throw new TerritorialEntityRuleException("Такой телефонный код уже существует.");
            if (current != original) throw Changed();
            var territory = await connection.QuerySingleAsync<int>(new CommandDefinition(TerritorialEntitySql.PhoneTerritoryCheck,
                new { draft.TerritoryId, Types = PhoneTerritoryTypes }, transaction, cancellationToken: ct));
            if (territory != 1) throw new TerritorialEntityRuleException("Выбранная территория отсутствует или не подходит для телефонного кода.");
            var phoneType = await connection.QuerySingleAsync<int>(new CommandDefinition(TerritorialEntitySql.PhoneTypeCheck,
                new { draft.TypeId }, transaction, cancellationToken: ct));
            if (phoneType != 1) throw new TerritorialEntityRuleException("Тип телефонной связи не найден.");
            await connection.ExecuteAsync(new CommandDefinition(original is null ? TerritorialEntitySql.PhoneInsert : TerritorialEntitySql.PhoneUpdate,
                new { key.Code, draft.TerritoryId, Length = (byte)draft.Length, draft.TypeId, Comment = Ansi(draft.Comment, 100) }, transaction, cancellationToken: ct));

            var saved = await ReadSingleAsync<PhoneCodeEntityRecord>(connection, transaction,
                string.Format(TerritorialEntitySql.PhoneRead, ""), key, ct);
            if (saved is null || saved with { ChangedBy = draft.ChangedBy, ChangedByName = draft.ChangedByName, ChangedAt = draft.ChangedAt } != draft)
                throw new TerritorialEntityRuleException("База не подтвердила сохранение телефонного кода.");
            return saved;
        }, ct);
    }

    /// <summary>Удаляет существующий код при совпадении снимка, оставляя проверку связей штатной схеме.</summary>
    public static async Task DeletePhoneAsync(DbManager db, string? principal, PhoneCodeEntityRecord original, CancellationToken ct = default)
    {
        if (!(await ReadAccessAsync(db, principal, ct)).PhoneDelete) throw Denied();
        await WriteAsync(db, async (connection, transaction) =>
        {
            var key = new { Code = Ansi(original.Code, 20) };
            var current = await ReadSingleAsync<PhoneCodeEntityRecord>(connection, transaction,
                string.Format(TerritorialEntitySql.PhoneRead, RowLock), key, ct);
            if (current != original) throw Changed();
            await connection.ExecuteAsync(new CommandDefinition(TerritorialEntitySql.PhoneDelete, key, transaction, cancellationToken: ct));
            if (await ReadSingleAsync<PhoneCodeEntityRecord>(connection, transaction,
                string.Format(TerritorialEntitySql.PhoneRead, ""), key, ct) is not null)
                throw new TerritorialEntityRuleException("База не подтвердила удаление телефонного кода.");
            return true;
        }, ct);
    }

    private static Task<T?> ReadSingleAsync<T>(DbConnection connection, DbTransaction transaction, string sql, object args, CancellationToken ct) =>
        connection.QuerySingleOrDefaultAsync<T>(new CommandDefinition(sql, args, transaction, cancellationToken: ct));

    private static Task<T> WriteAsync<T>(DbManager db, Func<DbConnection, DbTransaction, Task<T>> action, CancellationToken ct) =>
        db.RunDbAsync(async connection =>
        {
            await using var transaction = await connection.BeginTransactionAsync(ct);
            var result = await action(connection, transaction);
            await transaction.CommitAsync(CancellationToken.None);
            return result;
        });

    private static DbString Ansi(string value, int length) => new() { Value = value, IsAnsi = true, Length = length };
    private static TerritorialEntityRuleException Changed() => new("Запись изменилась или была удалена. Обновите форму перед повторным действием.");
    private static UnauthorizedAccessException Denied() => new("Для этой операции нет актуального разрешения.");

    private static void CheckValidation(IReadOnlyList<string> errors)
    {
        if (errors.Count != 0) throw new TerritorialEntityRuleException(string.Join(Environment.NewLine, errors));
    }
}
