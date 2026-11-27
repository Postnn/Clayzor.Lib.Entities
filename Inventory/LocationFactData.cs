using System.Data;
using System.Data.Common;
using System.Globalization;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Действующее рабочее расположение.</summary>
public sealed record LocationFactSite(int Id, string Name, bool IsWarehouse);

/// <summary>Строка учётного или фактического оборудования.</summary>
public sealed record LocationFactRow(int Id, string Name, DateTime? CheckedAt, int? LocationId, string? LocationName, bool Broken, string Notes)
{
    /// <summary>Проверка за текущий календарный день UTC.</summary>
    public bool CheckedOn(DateTime utc) => CheckedAt?.Date == utc.Date;

    /// <summary>Примечание с отметкой о неремонтопригодности.</summary>
    public string DisplayNotes => Broken ? "Сломано, ремонту не подлежит. " + Notes : Notes;
}

/// <summary>Сопоставленное поисковой функцией оборудование.</summary>
public sealed record LocationFactMatch(int Id, string MatchText, string Name, string? SerialNumber, string? OwnerName, string? InventoryNumber);

/// <summary>Снимок текущих учётных данных оборудования для осознанного подтверждения.</summary>
public sealed record LocationFactEquipment(int EquipmentId, int? OwnerId, string OwnerName, string FactName,
    string InventoryNumber, int? BookedLocationId, string BookedLocationName, DateTime? BookedFromUtc, DateTime EquipmentChangedAt);

/// <summary>Снимок операции до подтверждения.</summary>
public sealed record LocationFactSnapshot(LocationFactSite Site, LocationFactEquipment Equipment, DateTime? LastCheckedAt, bool Broken, string Notes)
{
    /// <summary>Указанное место отличается от текущего учётного.</summary>
    public bool DifferentLocation => Equipment.BookedLocationId != Site.Id;
}

/// <summary>Проверяемый отказ перед подтверждением операции.</summary>
public sealed class LocationFactConflict(string message) : InvalidOperationException(message);

/// <summary>Чтение и атомарное подтверждение инвентаризации рабочего места.</summary>
public static class LocationFactData
{
    private const string WriteLocks = "WITH (UPDLOCK,HOLDLOCK)";
    /// <summary>Роль, проверяемая триггером фактической инвентаризации.</summary>
    public const Роли RequiredRole = Роли.Специалист_по_инвентаризации_оборудования;

    /// <summary>Читает список рабочего места; отсутствие и отказ SQL отличимы.</summary>
    public static async Task<IReadOnlyList<LocationFactRow>> ListAsync(DbManager db, int locationId, bool itOnly, int? responsibleId, CancellationToken ct) =>
        (await db.QueryRequiredAsync<LocationFactRow>(SQLQueries.SELECT_LocationFactRows,
            new { LocationId = locationId, ItOnly = itOnly, ResponsibleId = responsibleId }, ct)).ToList();

    /// <summary>Ищет оборудование вне зависимости от выбранного места.</summary>
    public static Task<IEnumerable<LocationFactMatch>> SearchAsync(DbManager db, string? term, bool itOnly, int limit, int? equipmentId, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(term) ? Task.FromResult(Enumerable.Empty<LocationFactMatch>()) :
        db.QueryRequiredAsync<LocationFactMatch>(SQLQueries.SELECT_LocationFactEquipment,
            new { Search = term.Trim(), ItOnly = itOnly, Limit = limit, EquipmentId = equipmentId }, ct);

    /// <summary>Проверяет выбранное расположение по фактическому признаку рабочего места.</summary>
    public static async Task<LocationFactSite?> ReadSiteAsync(DbManager db, int locationId, CancellationToken ct) =>
        (await db.QueryRequiredAsync<LocationFactSite>(SelectSite(""), new { LocationId = locationId }, ct)).SingleOrDefault();

    /// <summary>Содержимое окна подтверждения, включая независимо читаемые флаг и примечание.</summary>
    public static Task<LocationFactSnapshot> ReadForConfirmAsync(DbManager db, int locationId, int equipmentId, CancellationToken ct) =>
        db.RunDbAsync(connection => ReadSnapshotAsync(connection, null, locationId, equipmentId, ct));

    /// <summary>Проверяет размеры факта и учётного периода до отправки SQL.</summary>
    public static string? Validate(string name, string notes, bool broken, bool move) =>
        name.Length > 300 ? "Название оборудования длиннее 300 символов." :
        notes.Length > 500 ? "Примечание длиннее 500 символов." :
        move && notes.Length > 300 ? "Для перемещения примечание должно содержать не более 300 символов." :
        broken && notes.TrimEnd(' ').Length < 10 ? "Требуется описать неисправность и причину невозможности ремонта." : null;

    private const string ConfirmKind = "confirm-location";

    /// <summary>Подтверждает место и опционально переносит учётное расположение, атомарно сохраняя серверный исход.</summary>
    public static async Task<DateTime> ConfirmAsync(DbManager db, string? principal, int expectedEmployee, Guid attemptId,
        LocationFactSnapshot expected, string notes, bool broken, bool move, CancellationToken ct)
    {
        if (Validate(expected.Equipment.FactName, notes, broken, move) is { } validation)
            throw new LocationFactConflict(validation);
        var equipmentId = expected.Equipment.EquipmentId;
        var locationId = expected.Site.Id;
        if (equipmentId <= 0 || locationId <= 0)
            throw new LocationFactConflict("Не удалось определить оборудование или рабочее место.");

        var actor = await RequireActorAsync(db, principal, expectedEmployee, ct);
        var attempt = InventoryOperationJournal.ForEmployee(actor, attemptId, Scope(equipmentId), ConfirmKind);
        if (await InventoryOperationJournal.ReadAttemptAsync(db, attempt, ct) is { } previous)
            return RequireConfirmed(previous, await ReadCheckedAtAsync(db, attemptId, equipmentId, locationId, ct));

        await db.RunDbAsync(connection => ValidateWriteAsync(connection, null, expected, move, ct));
        DateTime? checkedAt = null;
        try
        {
            var receipt = await InventoryOperationJournal.RunSqlAsync<FactRejectedException>(db, attempt,
                async (connection, transaction, token) =>
                {
                    WriteAccess access;
                    try
                    {
                        EnsureActor(await Employee.GetCurrentWithRolesAsync(connection, transaction, principal, token), expectedEmployee);
                        access = await ValidateWriteAsync(connection, transaction, expected, move, token);
                    }
                    catch (Exception error) when (error is LocationFactConflict or UnauthorizedAccessException)
                    {
                        throw new FactRejectedException(error);
                    }

                    if (move)
                    {
                        var from = access.NowUtc.AddSeconds(-1);
                        var parameters = new DynamicParameters();
                        parameters.Add("КодОборудования", equipmentId);
                        parameters.Add("От", from, DbType.DateTime);
                        parameters.Add("КодРасположения", locationId);
                        parameters.Add("СрокВозврата", null, DbType.DateTime);
                        parameters.Add("Примечания", notes, DbType.AnsiString, size: 300);
                        await connection.ExecuteAsync(new CommandDefinition(SQLQueries.INSERT_ОборудованиеРасположение,
                            parameters, transaction, cancellationToken: token));
                        var placed = await ReadContextAsync(connection, transaction, equipmentId, WriteLocks, token);
                        if (placed?.BookedFromUtc != from || placed.BookedLocationId != locationId)
                            throw new FactRejectedException(new LocationFactConflict("База не подтвердила учётное перемещение."));
                    }

                    var affected = await connection.QuerySingleAsync<int>(new CommandDefinition(SQLQueries.INSERT_LocationFact, new
                    {
                        CheckedAt = access.NowUtc, EquipmentId = equipmentId, LocationId = locationId,
                        FactName = new DbString { Value = expected.Equipment.FactName, Length = 300, IsAnsi = true },
                        Broken = broken ? 1 : 0,
                        Notes = new DbString { Value = notes, Length = 500, IsAnsi = true },
                    }, transaction, cancellationToken: token));
                    if (affected != 1)
                        throw new FactRejectedException(new LocationFactConflict("База не подтвердила запись инвентаризации."));

                    var recorded = await connection.ExecuteAsync(new CommandDefinition(SQLQueries.INSERT_ClayzorLocationFactReceipt,
                        new { AttemptId = attemptId, EquipmentId = equipmentId, LocationId = locationId, CheckedAtUtc = access.NowUtc },
                        transaction, cancellationToken: token));
                    if (recorded is not (1 or -1))
                        throw new InvalidOperationException("Не удалось связать факт с серверной попыткой.");
                    checkedAt = access.NowUtc;
                    return equipmentId;
                }, ct);
            return RequireConfirmed(receipt, checkedAt ?? await ReadCheckedAtAsync(db, attemptId, equipmentId, locationId, ct));
        }
        catch (FactRejectedException error) when (error.InnerException is LocationFactConflict conflict)
        {
            throw new LocationFactConflict(conflict.Message);
        }
        catch (FactRejectedException error) when (error.InnerException is UnauthorizedAccessException denied)
        {
            throw new UnauthorizedAccessException(denied.Message, denied);
        }

        DateTime RequireConfirmed(InventoryOperationReceipt receipt, DateTime? time)
        {
            if (receipt.State == InventoryOperationState.Rejected)
                throw new LocationFactConflict("Эта попытка проверки уже отклонена. Обновите текущие данные.");
            if (receipt.State != InventoryOperationState.Confirmed || receipt.ResultEntityId != equipmentId || time is null)
                throw new InventoryOperationPendingException();
            return DateTime.SpecifyKind(time.Value, DateTimeKind.Utc);
        }
    }

    /// <summary>Ищет серверное подтверждение точной попытки без повторного подтверждения или перемещения.</summary>
    public static async Task<DateTime?> ReadConfirmedAsync(DbManager db, string? principal, int expectedEmployee,
        Guid attemptId, int equipmentId, int locationId, CancellationToken ct)
    {
        var actor = await RequireActorAsync(db, principal, expectedEmployee, ct);
        var attempt = InventoryOperationJournal.ForEmployee(actor, attemptId, Scope(equipmentId), ConfirmKind);
        if (await InventoryOperationJournal.ReadAttemptAsync(db, attempt, ct) is not
            { State: InventoryOperationState.Confirmed, ResultEntityId: var confirmedEquipment } ||
            confirmedEquipment != equipmentId)
            return null;
        var checkedAt = await ReadCheckedAtAsync(db, attemptId, equipmentId, locationId, ct);
        return checkedAt is null ? null : DateTime.SpecifyKind(checkedAt.Value, DateTimeKind.Utc);
    }

    /// <summary>Проверяет блокировку оборудования и собственный последний подтверждённый факт выбранного места.</summary>
    public static async Task<(DateTime? Latest, bool HasPending)> ReadJournalAsync(DbManager db, string? principal,
        int expectedEmployee, int equipmentId, int locationId, CancellationToken ct)
    {
        var actor = await RequireActorAsync(db, principal, expectedEmployee, ct);
        if (await ReadSiteAsync(db, locationId, ct) is not { Id: var actualSiteId } || actualSiteId != locationId)
            throw new LocationFactConflict("Рабочее место недоступно.");
        var latest = await db.RunDbAsync(connection => connection.QuerySingleOrDefaultAsync<DateTime?>(
            new CommandDefinition(SQLQueries.SELECT_ClayzorLocationFactLatestReceipt,
                new { EquipmentId = equipmentId, LocationId = locationId, EmployeeId = actor.Id, CommandKind = ConfirmKind },
                cancellationToken: ct)));
        return (latest is null ? null : DateTime.SpecifyKind(latest.Value, DateTimeKind.Utc),
            await InventoryOperationJournal.IsScopePendingAsync(db, Scope(equipmentId), ct));
    }

    /// <summary>Переносит неподтверждённые факты только для доступных мест и оборудования.</summary>
    public static async Task ImportLegacyOperationsAsync(DbManager db, string? principal, int employeeId,
        IReadOnlyCollection<(Guid AttemptId, int SiteId, int EquipmentId)> evidence, CancellationToken ct)
    {
        if (evidence.Count == 0) return;
        var actor = await RequireActorAsync(db, principal, employeeId, ct);
        var attempts = new List<InventoryOperationJournal.Attempt>(evidence.Count);
        var verified = new HashSet<(int SiteId, int EquipmentId)>();
        foreach (var (attemptId, siteId, equipmentId) in evidence.Distinct().OrderBy(item => item.AttemptId))
        {
            if (siteId <= 0) throw new LocationFactConflict("Место прежней проверки недоступно.");
            var attempt = InventoryOperationJournal.ForEmployee(actor, attemptId, Scope(equipmentId), ConfirmKind);
            if (verified.Add((siteId, equipmentId)))
                await ReadForConfirmAsync(db, siteId, equipmentId, ct);
            attempts.Add(attempt);
        }

        await InventoryOperationJournal.ImportLegacyAsync(db, attempts, ct);
    }

    internal static async Task<Employee> RequirePresenceEditorAsync(DbManager db, string? principal, int employeeId,
        LocationFactSnapshot expected, CancellationToken ct)
    {
        var actor = await RequireActorAsync(db, principal, employeeId, ct);
        await db.RunDbAsync(connection => ValidateWriteAsync(connection, null, expected, move: false, ct));
        return actor;
    }

    private static async Task<WriteAccess> ValidateWriteAsync(DbConnection connection, DbTransaction? transaction,
        LocationFactSnapshot expected, bool move, CancellationToken ct)
    {
        var current = await ReadSnapshotAsync(connection, transaction, expected.Site.Id, expected.Equipment.EquipmentId, ct);
        if (current != expected)
            throw new LocationFactConflict("Оборудование или расположение изменилось. Обновите данные перед подтверждением.");
        if (move && !current.DifferentLocation)
            throw new LocationFactConflict("Перемещение больше не требуется.");
        var access = await connection.QuerySingleAsync<WriteAccess>(new CommandDefinition(SQLQueries.SELECT_LocationFactWriteAccess,
            transaction: transaction, cancellationToken: ct));
        if (!access.CanConfirm || move && !access.CanMove)
            throw new UnauthorizedAccessException("Для выбранной операции нет прав в базе.");
        if (current.LastCheckedAt >= access.NowUtc)
            throw new LocationFactConflict("Время предыдущей проверки не позволяет записать новый факт. Обновите список.");
        if (move && current.Equipment.BookedFromUtc >= access.NowUtc.AddSeconds(-1))
            throw new LocationFactConflict("Учётное расположение только что изменилось. Обновите список.");
        return access;
    }

    private static Task<DateTime?> ReadCheckedAtAsync(DbManager db, Guid attemptId, int equipmentId, int locationId,
        CancellationToken ct) => db.RunDbAsync(connection => connection.QuerySingleOrDefaultAsync<DateTime?>(
            new CommandDefinition(SQLQueries.SELECT_ClayzorLocationFactReceipt,
                new { AttemptId = attemptId, EquipmentId = equipmentId, LocationId = locationId }, cancellationToken: ct)));

    private static async Task<Employee> RequireActorAsync(DbManager db, string? principal, int expectedEmployee, CancellationToken ct)
    {
        var actor = await Employee.GetCurrentWithRolesAsync(db, principal, ct);
        EnsureActor(actor, expectedEmployee);
        return actor!;
    }

    private static void EnsureActor(Employee? actor, int expectedEmployee)
    {
        if (actor is null || actor.Id != expectedEmployee || !actor.HasRole(RequiredRole))
            throw new UnauthorizedAccessException("Нет права подтверждать инвентаризацию рабочего места.");
    }

    private static string Scope(int equipmentId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(equipmentId);
        return "equipment/" + equipmentId.ToString(CultureInfo.InvariantCulture);
    }

    private sealed class FactRejectedException(Exception error) : InvalidOperationException(error.Message, error);

    private static async Task<LocationFactSnapshot> ReadSnapshotAsync(System.Data.Common.DbConnection connection,
        IDbTransaction? transaction, int locationId, int equipmentId, CancellationToken ct)
    {
        var locking = transaction is null ? "" : WriteLocks;
        var site = await connection.QuerySingleOrDefaultAsync<LocationFactSite>(new CommandDefinition(SelectSite(locking),
            new { LocationId = locationId }, transaction, cancellationToken: ct));
        if (site is null) throw new LocationFactConflict("Расположение закрыто или больше недоступно.");
        var equipment = await ReadContextAsync(connection, transaction, equipmentId, locking, ct);
        if (equipment is null) throw new LocationFactConflict("Оборудование списано или недоступно.");
        if (equipment.OwnerId is null or <= 0)
            throw new LocationFactConflict($"Невозможно провести инвентаризацию, т.к. у оборудования #{equipmentId} нет владельца. Необходимо указать владельца оборудования.");
        var actual = await connection.QuerySingleOrDefaultAsync<EquipmentActualInventory>(new CommandDefinition(
            SQLQueries.SELECT_ID_РеальноеРасположениеОборудования_ПоследнееРасположение, new { EquipmentId = equipmentId }, transaction, cancellationToken: ct));
        return new(site, equipment, actual?.CheckedAt, actual?.IsBeyondRepair > 0, actual?.Notes ?? "");
    }

    private static string SelectSite(string hints) => string.Format(CultureInfo.InvariantCulture, SQLQueries.SELECT_LocationFactSite, hints);

    private static Task<LocationFactEquipment?> ReadContextAsync(System.Data.Common.DbConnection connection,
        IDbTransaction? transaction, int equipmentId, string hints, CancellationToken ct) =>
        connection.QuerySingleOrDefaultAsync<LocationFactEquipment>(new CommandDefinition(string.Format(CultureInfo.InvariantCulture,
            SQLQueries.SELECT_LocationFactContext, hints), new { EquipmentId = equipmentId }, transaction, cancellationToken: ct));

    private sealed record WriteAccess(DateTime NowUtc, bool CanConfirm, bool CanMove);
}
