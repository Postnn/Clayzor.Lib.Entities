using System.Data;
using System.Data.Common;
using System.Globalization;
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
    /// <summary>Разрешено создавать территорию.</summary>
    public bool TerritoryInsert { get; init; }
    /// <summary>Разрешено вызвать штатную процедуру удаления территории.</summary>
    public bool TerritoryDelete { get; init; }
    /// <summary>Разрешено создавать телефонный код.</summary>
    public bool PhoneInsert { get; init; }
    /// <summary>Разрешено изменять реквизиты телефонного кода.</summary>
    public bool PhoneUpdate { get; init; }
    /// <summary>Разрешено удалять телефонный код штатным DELETE.</summary>
    public bool PhoneDelete { get; init; }
}

/// <summary>Чтение и подтверждённые операции записи территории и телефонного кода.</summary>
public static partial class TerritorialEntityData
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

    /// <summary>Читает личное состояние дерева территории по CLID.</summary>
    public static async Task<TerritoryViewState?> LoadViewStateAsync(DbManager db, int clid, CancellationToken ct) =>
        TerritoryViewState.Read((await db.QueryRequiredAsync<string>(TerritorialEntitySql.LoadViewState, new { Clid = clid }, ct)).SingleOrDefault());

    /// <summary>Сохраняет личное состояние дерева через пользовательское представление.</summary>
    public static Task SaveViewStateAsync(DbManager db, int clid, TerritoryViewState state, CancellationToken ct) =>
        db.RunDbAsync(connection => connection.ExecuteAsync(new CommandDefinition(TerritorialEntitySql.SaveViewState,
            new { Clid = clid, Value = state.Serialize() }, cancellationToken: ct)));

    /// <summary>Возвращает упорядоченную цепочку предков внутри разрешённой области.</summary>
    public static async Task<IReadOnlyList<string>> ReadPathAsync(DbManager db, int id, IReadOnlyList<int>? roots, CancellationToken ct) =>
        (await db.QueryRequiredAsync<int>(TerritorialEntitySql.TreePath, new { Id = id, Restricted = roots is not null, Roots = roots?.ToArray() ?? [] }, ct))
            .Select(key => key.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();

    /// <summary>Проверяет каждый возвращаемый ID в текущей области дерева, включая незагруженные узлы.</summary>
    public static async Task<bool> SelectionInScopeAsync(DbManager db, IReadOnlyCollection<int> ids, IReadOnlyList<int>? roots, CancellationToken ct)
    {
        if (ids.Count > 1000 || ids.Any(id => id <= 0)) return false;
        if (ids.Count == 0) return true;
        var expected = ids.ToHashSet();
        var actual = await db.QueryRequiredAsync<int>(TerritorialEntitySql.TreeSelectedIds,
            new { Ids = expected.ToArray(), Restricted = roots is not null, Roots = roots?.ToArray() ?? [] }, ct);
        return expected.SetEquals(actual);
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

    /// <summary>Снимок территории и её границ для безопасного переноса.</summary>
    public sealed record MoveNode(int Id, int? ParentId, int Left, int Right);

    /// <summary>Отклоняет перенос в себя, потомка или к прежнему родителю.</summary>
    public static bool CanReparent(MoveNode source, MoveNode? target) =>
        source.Id > 0 && source.Left < source.Right && source.ParentId != target?.Id &&
        (target is null || target.Id > 0 && target.Id != source.Id && target.Left < target.Right &&
            !(source.Left <= target.Left && target.Right <= source.Right));

    internal static bool IsKnownMoveRejection(int number) => number is 229 or 547 or 1205 or 3609 or 50000;

    /// <summary>Изменяет родителя в транзакции. Границы L/R обслуживает штатный SQL-триггер.</summary>
    public static async Task ReparentAsync(DbManager db, string? principal, int employeeId,
        MoveNode source, MoveNode? target, CancellationToken ct)
    {
        if (!CanReparent(source, target)) throw new TerritorialEntityRuleException("Недопустимый перенос территории.");
        await RequireMoveActorAsync(db, principal, employeeId, ct);
        await WriteAsync(db, async (connection, transaction) =>
        {
            var currentActor = await Employee.GetCurrentAsync(connection, transaction, principal, ct);
            var access = currentActor?.Id == employeeId
                ? await connection.QuerySingleAsync<TerritorialEntityAccess>(new CommandDefinition(
                    TerritorialEntitySql.Access, transaction: transaction, cancellationToken: ct))
                : null;
            if (access?.TerritoryUpdate != true)
                throw new TerritorialEntityRuleException("Сотрудник или право переноса территории не подтверждены.");

            Task<MoveNode?> Read(int id) => ReadSingleAsync<MoveNode>(connection, transaction,
                TerritorialEntitySql.ReadMoveNode, new { Id = id }, ct);
            var current = await Read(source.Id);
            var parent = target is null ? null : await Read(target.Id);
            if (current != source || parent != target) throw Changed();
            try
            {
                await connection.ExecuteAsync(new CommandDefinition(TerritorialEntitySql.Reparent,
                    new { source.Id, ParentId = target?.Id }, transaction, cancellationToken: ct));
            }
            catch (Microsoft.Data.SqlClient.SqlException error) when (IsKnownMoveRejection(error.Number))
            {
                throw new TerritorialEntityRuleException(error.Number switch
                {
                    229 => "Нет разрешения на изменение родителя территории.",
                    1205 => "Перенос отменён из-за одновременного изменения. Обновите дерево.",
                    _ => "База отклонила перенос. Проверьте родителя, связи и телефонный код территории."
                });
            }
            var saved = await Read(source.Id);
            if (saved is null || saved.ParentId != target?.Id)
                throw new TerritorialEntityRuleException("База не подтвердила перенос территории.");
            return true;
        }, ct);
    }

    private static async Task<Employee> RequireMoveActorAsync(DbManager db, string? principal, int employeeId, CancellationToken ct)
    {
        var actor = await Employee.GetCurrentAsync(db, principal, ct);
        if (actor is null || actor.Id != employeeId || !(await ReadAccessAsync(db, actor, ct)).TerritoryUpdate)
            throw Denied();
        return actor;
    }

    /// <summary>Обновляет поля территории. Parent и вложенные множества принадлежат базе.</summary>
    public static async Task<TerritorialEntityRecord> UpdateTerritoryAsync(DbManager db, string? principal,
        int employeeId, TerritorialEntityRecord original, TerritorialEntityRecord draft, CancellationToken ct = default)
    {
        if (draft.Id <= 0 || draft.Id != original.Id || draft.ParentId != original.ParentId)
            throw new TerritorialEntityRuleException("Исходная территория или родитель изменились.");
        CheckValidation(TerritorialEntityRules.Validate(draft));
        TerritorialEntityRecord? written = null;
        await RunTerritoryCardWriteAsync(db, principal, employeeId, "territory.update",
            async (connection, transaction, token) =>
        {
            var key = new { draft.Id };
            var current = await ReadSingleAsync<TerritorialEntityRecord>(connection, transaction,
                string.Format(TerritorialEntitySql.TerritoryRead, RowLock), key, token);
            if (current != original) throw Changed();
            await connection.ExecuteAsync(new CommandDefinition(TerritorialEntitySql.TerritoryUpdate, new
            {
                draft.Id, Type = (int)draft.Type, Name = Ansi(draft.Name, 80), AlternateName = Ansi(draft.AlternateName, 80),
                Caption = Ansi(draft.Caption, 50), AlternateCaption = Ansi(draft.AlternateCaption, 50),
                Abbreviation = Ansi(draft.Abbreviation, 4), draft.CountryClassification,
                CountryCode = Ansi(draft.CountryCode, 20), Direction = Ansi(draft.Direction, 300),
            }, transaction, cancellationToken: token));
            var saved = await ReadSingleAsync<TerritorialEntityRecord>(connection, transaction,
                string.Format(TerritorialEntitySql.TerritoryRead, ""), key, token);
            if (saved is null || saved with { ChangedBy = draft.ChangedBy, ChangedByName = draft.ChangedByName, ChangedAt = draft.ChangedAt } != draft)
                throw new TerritorialEntityRuleException("База не подтвердила сохранение всех реквизитов территории.");
            written = saved;
            return saved.Id;
        }, ct);
        return written!;
    }

    /// <summary>Создаёт территорию штатным INSERT и возвращает проверенную запись.</summary>
    public static async Task<TerritorialEntityRecord> CreateTerritoryAsync(DbManager db, string? principal,
        int employeeId, TerritorialEntityRecord draft, CancellationToken ct = default)
    {
        if (draft.Id != 0 || draft.ParentId is <= 0)
            throw new TerritorialEntityRuleException("Некорректный ключ новой территории или родителя.");
        CheckValidation(TerritorialEntityRules.Validate(draft));
        TerritorialEntityRecord? written = null;
        await RunTerritoryCardWriteAsync(db, principal, employeeId, "territory.create",
            async (connection, transaction, token) =>
        {
            if (draft.ParentId is { } parent && await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                    TerritorialEntitySql.ReadMoveNode, new { Id = parent }, transaction, cancellationToken: token)) != parent)
                throw new TerritorialEntityRuleException("Родительская территория больше недоступна.");

            var id = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(TerritorialEntitySql.TerritoryInsert, new
            {
                draft.ParentId, Type = (int)draft.Type, Name = Ansi(draft.Name, 80), AlternateName = Ansi(draft.AlternateName, 80),
                Caption = Ansi(draft.Caption, 50), AlternateCaption = Ansi(draft.AlternateCaption, 50),
                Abbreviation = Ansi(draft.Abbreviation, 4), draft.CountryClassification,
                CountryCode = Ansi(draft.CountryCode, 20), Direction = Ansi(draft.Direction, 300),
            }, transaction, cancellationToken: token));
            if (id is not > 0)
                throw new TerritorialEntityRuleException("SQL не вернул ключ новой территории. Создание отменено.");

            var saved = await ReadSingleAsync<TerritorialEntityRecord>(connection, transaction,
                string.Format(TerritorialEntitySql.TerritoryRead, ""), new { Id = id.Value }, token);
            if (saved is null || saved with { ChangedBy = draft.ChangedBy, ChangedByName = draft.ChangedByName, ChangedAt = draft.ChangedAt }
                != draft with { Id = id.Value })
                throw new TerritorialEntityRuleException("База не подтвердила создание территории с указанными реквизитами.");
            written = saved;
            return saved.Id;
        }, ct);
        return written!;
    }

    /// <summary>Удаляет проверенный исходный снимок штатной процедурой без замещающей территории.</summary>
    public static async Task DeleteTerritoryAsync(DbManager db, string? principal,
        int employeeId, TerritorialEntityRecord original, CancellationToken ct = default)
    {
        if (original.Id <= 0) throw new TerritorialEntityRuleException("Некорректный ключ территории.");
        await RunTerritoryCardWriteAsync(db, principal, employeeId, "territory.delete",
            async (connection, transaction, token) =>
        {
            var key = new { original.Id };
            var read = string.Format(TerritorialEntitySql.TerritoryRead, RowLock);
            if (await ReadSingleAsync<TerritorialEntityRecord>(connection, transaction, read, key, token) != original)
                throw Changed();
            await connection.ExecuteAsync(new CommandDefinition(TerritorialEntitySql.TerritoryDelete, new
            {
                КодТерриторииУдаляемой = original.Id,
                КодТерриторииЗамещающей = 0,
                Выполнить = 1,
            }, transaction, commandType: CommandType.StoredProcedure, cancellationToken: token));
            if (await ReadSingleAsync<TerritorialEntityRecord>(connection, transaction, read, key, token) is not null)
                throw new TerritorialEntityRuleException("База не подтвердила удаление территории.");
            return original.Id;
        }, ct);
    }

    private static async Task RunTerritoryCardWriteAsync(DbManager db, string? principal,
        int employeeId, string operation, Func<DbConnection, DbTransaction, CancellationToken, Task<int?>> write,
        CancellationToken ct)
    {
        await RequireTerritoryCardWriterAsync(db, principal, employeeId, operation, ct);
        await WriteAsync(db, async (connection, transaction) =>
        {
            var current = await Employee.GetCurrentAsync(connection, transaction, principal, ct);
            var access = current?.Id == employeeId
                ? await connection.QuerySingleAsync<TerritorialEntityAccess>(new CommandDefinition(
                    TerritorialEntitySql.Access, transaction: transaction, cancellationToken: ct))
                : null;
            if (access is null || !CanWriteTerritoryCard(access, operation))
                throw new TerritorialEntityRuleException("Текущий сотрудник или право записи территории не подтверждены.");
            return await write(connection, transaction, ct);
        }, ct);
    }

    private static async Task<Employee> RequireTerritoryCardWriterAsync(DbManager db, string? principal, int employeeId,
        string operation, CancellationToken ct)
    {
        var actor = await Employee.GetCurrentAsync(db, principal, ct);
        if (actor is null || actor.Id != employeeId ||
            !CanWriteTerritoryCard(await ReadAccessAsync(db, actor, ct), operation))
            throw Denied();
        return actor;
    }

    private static bool CanWriteTerritoryCard(TerritorialEntityAccess access, string operation) => operation switch
    {
        "territory.create" => access.TerritoryInsert,
        "territory.update" => access.TerritoryUpdate,
        "territory.delete" => access.TerritoryDelete,
        _ => false,
    };

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
    private static TerritorialEntityRuleException Changed() =>
        new("Запись изменилась или была удалена. Обновите форму перед повторным действием.", baselineConflict: true);
    private static UnauthorizedAccessException Denied() => new("Для этой операции нет актуального разрешения.");

    private static void CheckValidation(IReadOnlyList<string> errors)
    {
        if (errors.Count != 0) throw new TerritorialEntityRuleException(string.Join(Environment.NewLine, errors));
    }
}
