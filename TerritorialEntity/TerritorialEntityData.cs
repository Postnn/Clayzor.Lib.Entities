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

    private const string MoveScope = "territory/tree";
    private const string MoveKind = "territory.move";

    /// <summary>Исход собственной попытки переноса с ключами из серверного журнала.</summary>
    /// <param name="SourceId">Ключ переносимой территории.</param>
    /// <param name="TargetId">Ключ выбранного родителя или null для корня.</param>
    /// <param name="State">Подтверждённое состояние исхода.</param>
    public sealed record MoveOutcome(int SourceId, int? TargetId, InventoryOperationState State);

    private sealed record MoveOutcomeRow(Guid AttemptId, int EmployeeId, string ConflictKey, string CommandKind,
        byte State, int? ResultEntityId, string? RelatedScope);

    /// <summary>Читает собственный последний исход переноса после повторной проверки сотрудника и SQL-доступа.</summary>
    public static async Task<MoveOutcome?> ReadLatestMoveOutcomeAsync(DbManager db, string? principal, int employeeId,
        CancellationToken ct)
    {
        var actor = await RequireMoveActorAsync(db, principal, employeeId, ct);
        var rows = (await db.QueryRequiredAsync<MoveOutcomeRow>(TerritorialEntitySql.LatestOwnMove, new
        {
            EmployeeId = actor.Id,
            ConflictKey = MoveScope,
            CommandKind = MoveKind,
            Unknown = (byte)InventoryOperationState.Unknown,
        }, ct)).ToArray();
        if (rows.Length == 0) return null;
        if (rows is not [var row] || row.AttemptId == Guid.Empty || row.EmployeeId != actor.Id ||
            row.ConflictKey != MoveScope || row.CommandKind != MoveKind ||
            !TryReadMoveScope(row.RelatedScope, out var from, out var to) ||
            MoveAttempt(actor, row.AttemptId, from!, to).OtherScopes?.Single() != row.RelatedScope ||
            row.State > (byte)InventoryOperationState.Rejected ||
            row.ResultEntityId != (row.State == (byte)InventoryOperationState.Confirmed ? from!.Id : (int?)null))
            throw new InvalidOperationException("Не удалось подтвердить запись журнала переноса территории.");

        return new MoveOutcome(from!.Id, to?.Id, (InventoryOperationState)row.State);
    }

    private static bool TryReadMoveScope(string? scope, out MoveNode? from, out MoveNode? to)
    {
        from = to = null;
        var parts = scope?.Split('/');
        if (parts is null || parts.Length is not (7 or 10) || parts[0] != "territory" || parts[1] != "move")
            return false;
        from = ReadNode(2);
        if (from is null) return false;
        if (parts.Length == 7) return parts[6] == "root";
        to = ReadNode(6);
        return to is not null && to.Id != from.Id;

        MoveNode? ReadNode(int offset)
        {
            if (!ReadInt(parts[offset], out var id) || id <= 0 ||
                !ReadInt(parts[offset + 2], out var left) || !ReadInt(parts[offset + 3], out var right) || right <= left)
                return null;
            if (parts[offset + 1] == "root") return new MoveNode(id, null, left, right);
            return ReadInt(parts[offset + 1], out var parent) && parent > 0 ? new MoveNode(id, parent, left, right) : null;
        }

        static bool ReadInt(string text, out int value) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Проверяет единый серверный слот дерева, включая попытки других сотрудников.</summary>
    public static Task<bool> IsMovePendingAsync(DbManager db, CancellationToken ct) =>
        InventoryOperationJournal.IsScopePendingAsync(db, MoveScope, ct);

    /// <summary>Читает исход точной собственной попытки у текущего сотрудника с правом переноса.</summary>
    public static async Task<InventoryOperationReceipt?> ReadMoveReceiptAsync(DbManager db, string? principal,
        int employeeId, Guid attemptId, MoveNode source, MoveNode? target, CancellationToken ct)
    {
        var actor = await RequireMoveActorAsync(db, principal, employeeId, ct);
        return await InventoryOperationJournal.ReadAttemptAsync(db, MoveAttempt(actor, attemptId, source, target), ct);
    }

    /// <summary>Учитывает прежние неподтверждённые переносы после проверки доступа к дереву.</summary>
    public static async Task ImportLegacyMoveOperationsAsync(DbManager db, string? principal, int employeeId,
        IReadOnlyCollection<(Guid AttemptId, MoveNode From, MoveNode? To)> evidence, CancellationToken ct)
    {
        if (evidence.Count == 0) return;
        var actor = await RequireMoveActorAsync(db, principal, employeeId, ct);
        var attempts = new List<InventoryOperationJournal.Attempt>(evidence.Count);
        foreach (var (id, from, to) in evidence.Distinct().OrderBy(item => item.AttemptId))
        {
            if (!ValidNode(from) || to is not null && (!ValidNode(to) || to.Id == from.Id))
                throw new TerritorialEntityRuleException("Некорректный снимок прежнего переноса.");
            attempts.Add(MoveAttempt(actor, id, from, to));
        }

        await InventoryOperationJournal.ImportLegacyAsync(db, attempts, ct);

        static bool ValidNode(MoveNode? node) => node is { Id: > 0 } && node.ParentId is not <= 0 &&
            node.Left >= 0 && node.Right > node.Left;
    }

    internal static bool IsKnownMoveRejection(int number) => number is 229 or 547 or 1205 or 3609 or 50000;

    /// <summary>Фиксирует изменение родителя и квитанцию одной транзакцией. Границы L/R обслуживает штатный SQL-триггер.</summary>
    public static async Task<InventoryOperationReceipt> ReparentAsync(DbManager db, string? principal, int employeeId,
        Guid attemptId, MoveNode source, MoveNode? target, CancellationToken ct)
    {
        if (!CanReparent(source, target)) throw new TerritorialEntityRuleException("Недопустимый перенос территории.");
        var actor = await RequireMoveActorAsync(db, principal, employeeId, ct);
        var attempt = MoveAttempt(actor, attemptId, source, target);
        if (await InventoryOperationJournal.ReadAttemptAsync(db, attempt, ct) is { } previous)
            return RequireMoveResult(previous, source.Id);

        InventoryOperationReceipt receipt;
        try
        {
            receipt = await InventoryOperationJournal.RunSqlAsync<TerritorialEntityRuleException>(db, attempt,
                async (connection, transaction, token) =>
                {
                    var currentActor = await Employee.GetCurrentAsync(connection, transaction, principal, token);
                    var access = currentActor?.Id == employeeId
                        ? await connection.QuerySingleAsync<TerritorialEntityAccess>(new CommandDefinition(
                            TerritorialEntitySql.Access, transaction: transaction, cancellationToken: token))
                        : null;
                    if (access?.TerritoryUpdate != true)
                        throw new TerritorialEntityRuleException("Сотрудник или право переноса территории не подтверждены.");

                    Task<MoveNode?> Read(int id) => ReadSingleAsync<MoveNode>(connection, transaction,
                        TerritorialEntitySql.ReadMoveNode, new { Id = id }, token);
                    var current = await Read(source.Id);
                    var parent = target is null ? null : await Read(target.Id);
                    if (current != source || parent != target) throw Changed();
                    try
                    {
                        await connection.ExecuteAsync(new CommandDefinition(TerritorialEntitySql.Reparent,
                            new { source.Id, ParentId = target?.Id }, transaction, cancellationToken: token));
                    }
                    catch (Microsoft.Data.SqlClient.SqlException error) when (IsKnownMoveRejection(error.Number))
                    {
                        // The coordinator marks a rejection only after it has confirmed rollback of the transaction.
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
                    return source.Id;
                }, ct);
        }
        catch (Exception error) when (!ct.IsCancellationRequested &&
            error is not (TerritorialEntityRuleException or InventoryOperationPendingException or InventoryOperationReservationException))
        {
            var observed = await InventoryOperationJournal.ReadAttemptAsync(db, attempt, ct);
            if (observed is not { State: InventoryOperationState.Confirmed, ResultEntityId: var id } || id != source.Id)
                throw;
            return observed;
        }
        return RequireMoveResult(receipt, source.Id);
    }

    private static async Task<Employee> RequireMoveActorAsync(DbManager db, string? principal, int employeeId, CancellationToken ct)
    {
        var actor = await Employee.GetCurrentAsync(db, principal, ct);
        if (actor is null || actor.Id != employeeId || !(await ReadAccessAsync(db, actor, ct)).TerritoryUpdate)
            throw Denied();
        return actor;
    }

    private static InventoryOperationJournal.Attempt MoveAttempt(Employee actor, Guid attemptId, MoveNode source, MoveNode? target)
    {
        static string NodeKey(MoveNode node) =>
            FormattableString.Invariant($"{node.Id}/{(node.ParentId is { } parent ? FormattableString.Invariant($"{parent}") : "root")}/{node.Left}/{node.Right}");
        var targetKey = target is null ? "root" : NodeKey(target);
        return InventoryOperationJournal.ForEmployee(actor, attemptId, MoveScope, MoveKind,
            [$"territory/move/{NodeKey(source)}/{targetKey}"]);
    }

    private static InventoryOperationReceipt RequireMoveResult(InventoryOperationReceipt receipt, int sourceId)
    {
        if (receipt.State == InventoryOperationState.Rejected)
            throw new TerritorialEntityRuleException("Эта попытка переноса уже отклонена.");
        if (receipt.State != InventoryOperationState.Confirmed || receipt.ResultEntityId != sourceId)
            throw new InventoryOperationPendingException();
        return receipt;
    }

    /// <summary>Читает исход собственной попытки карточки по её точному маршруту.</summary>
    public static async Task<InventoryOperationReceipt?> ReadTerritoryWriteReceiptAsync(DbManager db, string? principal,
        int employeeId, Guid attemptId, string operation, int territoryId, int? parentId, CancellationToken ct)
    {
        var actor = await RequireTerritoryCardWriterAsync(db, principal, employeeId, operation, ct);
        var attempt = TerritoryCardAttempt(actor, attemptId, operation, territoryId, parentId);
        var receipt = await InventoryOperationJournal.ReadAttemptAsync(db, attempt, ct);
        return receipt is { State: InventoryOperationState.Confirmed } && !IsTerritoryResult(receipt, territoryId)
            ? null : receipt;
    }

    /// <summary>Учитывает только прежние метаданные операций из доступной области дерева.</summary>
    public static async Task ImportLegacyTerritoryCardOperationsAsync(DbManager db, string? principal, int employeeId,
        IReadOnlyCollection<(Guid AttemptId, string Operation, int TerritoryId, int? ParentId)> evidence, CancellationToken ct)
    {
        if (evidence.Count == 0) return;
        var actor = await Employee.GetCurrentAsync(db, principal, ct);
        if (actor is null || actor.Id != employeeId) throw Denied();
        var access = await ReadAccessAsync(db, actor, ct);
        var attempts = new List<InventoryOperationJournal.Attempt>(evidence.Count);
        foreach (var (attemptId, operation, id, parentId) in evidence.Distinct().OrderBy(item => item.AttemptId))
        {
            var validKey = operation switch
            {
                "territory.create" => id == 0 && parentId is not <= 0,
                "territory.update" or "territory.delete" => id > 0 && parentId is not <= 0,
                _ => false,
            };
            if (attemptId == Guid.Empty || !validKey)
                throw new TerritorialEntityRuleException("Прежняя попытка записи территории требует сверки.");
            if (!CanWriteTerritoryCard(access, operation)) throw Denied();
            attempts.Add(TerritoryCardAttempt(actor, attemptId, operation, id, parentId));
        }
        await InventoryOperationJournal.ImportLegacyAsync(db, attempts, ct);
    }

    /// <summary>Обновляет поля территории. Parent и вложенные множества принадлежат базе.</summary>
    public static async Task<TerritorialEntityRecord> UpdateTerritoryAsync(DbManager db, string? principal,
        int employeeId, Guid attemptId, TerritorialEntityRecord original, TerritorialEntityRecord draft, CancellationToken ct = default)
    {
        if (draft.Id <= 0 || draft.Id != original.Id || draft.ParentId != original.ParentId)
            throw new TerritorialEntityRuleException("Исходная территория или родитель изменились.");
        CheckValidation(TerritorialEntityRules.Validate(draft));
        TerritorialEntityRecord? written = null;
        var receipt = await RunTerritoryCardWriteAsync(db, principal, employeeId, attemptId, "territory.update",
            draft.Id, null, async (connection, transaction, token) =>
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
        return written ?? await ReadConfirmedTerritoryCardAsync(db, receipt, ct);
    }

    /// <summary>Создаёт территорию штатным INSERT и принимает только возвращённый и проверенный SQL ключ.</summary>
    public static async Task<TerritorialEntityRecord> CreateTerritoryAsync(DbManager db, string? principal,
        int employeeId, Guid attemptId, TerritorialEntityRecord draft, CancellationToken ct = default)
    {
        if (draft.Id != 0 || draft.ParentId is <= 0)
            throw new TerritorialEntityRuleException("Некорректный ключ новой территории или родителя.");
        CheckValidation(TerritorialEntityRules.Validate(draft));
        TerritorialEntityRecord? written = null;
        var receipt = await RunTerritoryCardWriteAsync(db, principal, employeeId, attemptId, "territory.create",
            0, draft.ParentId, async (connection, transaction, token) =>
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
        return written ?? await ReadConfirmedTerritoryCardAsync(db, receipt, ct);
    }

    /// <summary>Удаляет проверенный исходный снимок штатной процедурой без замещающей территории.</summary>
    public static Task<InventoryOperationReceipt> DeleteTerritoryAsync(DbManager db, string? principal,
        int employeeId, Guid attemptId, TerritorialEntityRecord original, CancellationToken ct = default)
    {
        if (original.Id <= 0) throw new TerritorialEntityRuleException("Некорректный ключ территории.");
        return RunTerritoryCardWriteAsync(db, principal, employeeId, attemptId, "territory.delete",
            original.Id, null, async (connection, transaction, token) =>
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
            if (await connection.ExecuteScalarAsync<int>(new CommandDefinition(SQLQueries.SELECT_ActiveJournalTransaction,
                    transaction: transaction, cancellationToken: token)) != 1)
                throw new InvalidOperationException("Транзакция удаления территории не подтверждена.");
            if (await ReadSingleAsync<TerritorialEntityRecord>(connection, transaction, read, key, token) is not null)
                throw new TerritorialEntityRuleException("База не подтвердила удаление территории.");
            return original.Id;
        }, ct);
    }

    private static async Task<InventoryOperationReceipt> RunTerritoryCardWriteAsync(DbManager db, string? principal,
        int employeeId, Guid attemptId, string operation, int id, int? parentId,
        Func<DbConnection, DbTransaction, CancellationToken, Task<int?>> write, CancellationToken ct)
    {
        var actor = await RequireTerritoryCardWriterAsync(db, principal, employeeId, operation, ct);
        var attempt = TerritoryCardAttempt(actor, attemptId, operation, id, parentId);
        if (await InventoryOperationJournal.ReadAttemptAsync(db, attempt, ct) is { } previous)
            return EnsureTerritoryResult(previous, id);

        InventoryOperationReceipt receipt;
        try
        {
            receipt = await InventoryOperationJournal.RunSqlAsync<TerritorialEntityRuleException>(db, attempt,
                async (connection, transaction, token) =>
                {
                    var current = await Employee.GetCurrentAsync(connection, transaction, principal, token);
                    var access = current?.Id == employeeId
                        ? await connection.QuerySingleAsync<TerritorialEntityAccess>(new CommandDefinition(
                            TerritorialEntitySql.Access, transaction: transaction, cancellationToken: token))
                        : null;
                    if (access is null || !CanWriteTerritoryCard(access, operation))
                        throw new TerritorialEntityRuleException("Текущий сотрудник или право записи территории не подтверждены.");
                    return await write(connection, transaction, token);
                }, ct);
        }
        catch (Exception exception) when (!ct.IsCancellationRequested &&
            exception is not (TerritorialEntityRuleException or InventoryOperationReservationException or InventoryOperationPendingException))
        {
            InventoryOperationReceipt? observed;
            try { observed = await InventoryOperationJournal.ReadAttemptAsync(db, attempt, ct); }
            catch (Exception readError) when (readError is not OperationCanceledException)
            {
                observed = null;
            }
            if (observed is not { State: InventoryOperationState.Confirmed } || !IsTerritoryResult(observed, id))
                throw new InvalidOperationException("Результат записи территории требует сверки.");
            return observed;
        }
        return EnsureTerritoryResult(receipt, id);
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

    private static InventoryOperationJournal.Attempt TerritoryCardAttempt(Employee actor, Guid attemptId, string operation,
        int id, int? parentId)
    {
        var cardScope = operation == "territory.create"
            ? FormattableString.Invariant($"territory/card/new/{parentId ?? 0}")
            : FormattableString.Invariant($"territory/card/{id}");
        return InventoryOperationJournal.ForEmployee(actor, attemptId, "territory/tree", operation, [cardScope]);
    }

    private static bool IsTerritoryResult(InventoryOperationReceipt receipt, int id) =>
        receipt.ResultEntityId is > 0 && (id == 0 || receipt.ResultEntityId == id);

    private static InventoryOperationReceipt EnsureTerritoryResult(InventoryOperationReceipt receipt, int id)
    {
        if (receipt.State == InventoryOperationState.Rejected)
            throw new TerritorialEntityRuleException("Эта попытка записи территории уже отклонена.");
        if (receipt.State != InventoryOperationState.Confirmed || !IsTerritoryResult(receipt, id))
            throw new InventoryOperationPendingException();
        return receipt;
    }

    private static async Task<TerritorialEntityRecord> ReadConfirmedTerritoryCardAsync(DbManager db,
        InventoryOperationReceipt receipt, CancellationToken ct) =>
        await ReadTerritoryAsync(db, receipt.ResultEntityId!.Value, ct) ??
        throw new InvalidOperationException("База подтвердила запись. Актуальная карточка недоступна для чтения.");

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
