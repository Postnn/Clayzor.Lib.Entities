using System.Data;
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

    /// <summary>Подтверждает место и опционально переносит учётное расположение одной транзакцией.</summary>
    public static async Task<DateTime> ConfirmAsync(DbManager db, string? principal, LocationFactSnapshot expected,
        string notes, bool broken, bool move, CancellationToken ct)
    {
        if (Validate(expected.Equipment.FactName, notes, broken, move) is { } validation) throw new LocationFactConflict(validation);
        if (!await Employee.HasRoleAsync(db, principal, RequiredRole, cancellationToken: ct))
            throw new UnauthorizedAccessException("Нет права подтверждать инвентаризацию рабочего места.");

        return await db.RunDbAsync(async connection =>
        {
            await using var tx = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var current = await ReadSnapshotAsync(connection, tx, expected.Site.Id, expected.Equipment.EquipmentId, ct);
            if (current != expected) throw new LocationFactConflict("Оборудование или расположение изменилось. Обновите данные перед подтверждением.");
            if (move && !current.DifferentLocation) throw new LocationFactConflict("Перемещение больше не требуется.");
            var access = await connection.QuerySingleAsync<WriteAccess>(new CommandDefinition(SQLQueries.SELECT_LocationFactWriteAccess,
                transaction: tx, cancellationToken: ct));
            if (!access.CanConfirm || move && !access.CanMove) throw new UnauthorizedAccessException("Для выбранной операции нет прав в базе.");

            if (current.LastCheckedAt >= access.NowUtc)
                throw new LocationFactConflict("Время предыдущей проверки не позволяет записать новый факт. Обновите список.");
            if (move)
            {
                var from = access.NowUtc.AddSeconds(-1);
                if (current.Equipment.BookedFromUtc >= from)
                    throw new LocationFactConflict("Учётное расположение только что изменилось. Обновите список.");
                var parameters = new DynamicParameters();
                parameters.Add("КодОборудования", current.Equipment.EquipmentId);
                parameters.Add("От", from, DbType.DateTime);
                parameters.Add("КодРасположения", current.Site.Id);
                parameters.Add("СрокВозврата", null, DbType.DateTime);
                parameters.Add("Примечания", notes, DbType.AnsiString, size: 300);
                await connection.ExecuteAsync(new CommandDefinition(SQLQueries.INSERT_ОборудованиеРасположение,
                    parameters, tx, cancellationToken: ct));
                var placed = await ReadContextAsync(connection, tx, current.Equipment.EquipmentId, WriteLocks, ct);
                if (placed?.BookedFromUtc != from || placed.BookedLocationId != current.Site.Id)
                    throw new LocationFactConflict("База не подтвердила учётное перемещение.");
            }

            var affected = await connection.QuerySingleAsync<int>(new CommandDefinition(SQLQueries.INSERT_LocationFact, new
            {
                CheckedAt = access.NowUtc, current.Equipment.EquipmentId, LocationId = current.Site.Id,
                FactName = new DbString { Value = current.Equipment.FactName, Length = 300, IsAnsi = true },
                Broken = broken ? 1 : 0,
                Notes = new DbString { Value = notes, Length = 500, IsAnsi = true },
            }, tx, cancellationToken: ct));
            if (affected != 1) throw new LocationFactConflict("База не подтвердила запись инвентаризации.");
            await tx.CommitAsync(CancellationToken.None);
            return DateTime.SpecifyKind(access.NowUtc, DateTimeKind.Utc);
        });
    }

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
