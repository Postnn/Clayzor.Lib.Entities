using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

internal sealed record EquipmentAtsHeader(Guid AttemptId, int EmployeeId, int EquipmentId, byte Origin,
    DateTime ExpectedChangedAtUtc, int? OwnerId, string ContextFingerprint, byte State);

internal sealed record EquipmentAtsStep(int StepIndex, byte Kind, int TargetId, bool Deleting, byte State)
{
    internal const byte Xorcom = 1;
    internal const byte Ccm = 2;
    internal const byte Planned = 0;
    internal const byte Dispatching = 1;
    internal const byte Confirmed = 2;
}

internal sealed record EquipmentAtsSnapshot(EquipmentAtsHeader Header, IReadOnlyList<EquipmentAtsStep> Steps);

internal sealed class EquipmentAtsContextChangedException()
    : InvalidOperationException("Карточка, телефонные связи или станции изменились. Шаг АТС не отправлен.");

/// <summary>Хранит неизменный серверный план настройки IP-телефона и фазы отправки шагов АТС.</summary>
internal static partial class EquipmentAtsWorkflow
{
    private const byte Configure = (byte)EquipmentPhoneSyncOrigin.ExplicitConfigure;
    private const byte Open = 0;
    private const byte Completed = 1;
    internal const byte Abandoned = 2;

    internal static Task<EquipmentAtsSnapshot> OpenConfigureAsync(DbManager db, string? principal, int employeeId,
        int equipmentId, Guid attemptId, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(attemptId, Guid.Empty);
        return WithAccessAsync(db, principal, employeeId, equipmentId, async (connection, transaction, equipment) =>
        {
            if (await ReadAsync(connection, transaction, employeeId, equipmentId, attemptId, ct) is { } previous)
                return previous;

            var context = await ReadContextAsync(connection, transaction, equipmentId, ct);
            var scopes = context.Phones.Select(id => Scope("tel", id))
                .Concat(context.Steps.Where(step => step.Kind == EquipmentAtsStep.Ccm)
                    .Select(step => Scope("equipment", step.TargetId)))
                .Append(Scope("equipment", equipmentId)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            foreach (var scope in scopes)
            {
                if (await InventoryOperationJournal.IsScopePendingForUpdateAsync(connection, transaction, scope, attemptId, ct))
                    throw new InventoryOperationPendingException(blockedBeforeReservation: true);
            }

            var header = new EquipmentAtsHeader(attemptId, employeeId, equipmentId, Configure,
                equipment.ChangedAtUtc, equipment.OwnerId, context.Fingerprint, Open);
            var parameters = new DynamicParameters(header);
            parameters.Add(nameof(header.ExpectedChangedAtUtc), header.ExpectedChangedAtUtc, DbType.DateTime2);
            await WriteOneAsync(connection, transaction, SQLQueries.INSERT_EquipmentAtsWorkflow, parameters, ct);

            foreach (var scope in scopes)
                await WriteOneAsync(connection, transaction, SQLQueries.INSERT_EquipmentAtsScope,
                    new { AttemptId = attemptId, ConflictKey = scope, OpenSlot = Guid.Empty }, ct);
            foreach (var step in context.Steps)
                await WriteOneAsync(connection, transaction, SQLQueries.INSERT_EquipmentAtsStep,
                    new { AttemptId = attemptId, step.StepIndex, step.Kind, step.TargetId, step.Deleting }, ct);
            if (context.Steps.Length == 0)
            {
                await FinishAsync(connection, transaction, attemptId, ct);
                header = header with { State = Completed };
            }
            return new EquipmentAtsSnapshot(header, context.Steps);
        }, ct);
    }

    internal static Task<EquipmentAtsSnapshot?> ReadConfigureAsync(DbManager db, string? principal, int employeeId,
        int equipmentId, Guid? attemptId, CancellationToken ct) =>
        WithAccessAsync(db, principal, employeeId, equipmentId,
            (connection, transaction, _) => ReadAsync(connection, transaction, employeeId, equipmentId, attemptId, ct), ct);

    internal static Task<bool> BeginConfigureStepAsync(DbManager db, string? principal, int employeeId, int equipmentId,
        Guid attemptId, EquipmentAtsStep requested, CancellationToken ct) =>
        WithAccessAsync(db, principal, employeeId, equipmentId, async (connection, transaction, equipment) =>
        {
            var snapshot = await RequireAsync(connection, transaction, employeeId, equipmentId, attemptId, ct);
            var step = RequireStep(snapshot, requested);
            if (step.State == EquipmentAtsStep.Confirmed) return false;
            if (snapshot.Header.State != Open || step.State != EquipmentAtsStep.Planned ||
                snapshot.Steps.Any(item => item.StepIndex < step.StepIndex && item.State != EquipmentAtsStep.Confirmed))
                throw new InventoryOperationPendingException();

            var context = await ReadContextAsync(connection, transaction, equipmentId, ct);
            if (!ContextMatches(snapshot.Header, equipment, context) || !context.Steps.Any(item => SameStep(item, step)))
                throw new EquipmentAtsContextChangedException();

            await ChangeStepAsync(connection, transaction, attemptId, step, EquipmentAtsStep.Dispatching, ct);
            return true;
        }, ct);

    internal static Task ConfirmConfigureStepAsync(DbManager db, string? principal, int employeeId, int equipmentId,
        Guid attemptId, EquipmentAtsStep requested, CancellationToken ct) =>
        WithAccessAsync(db, principal, employeeId, equipmentId, async (connection, transaction, _) =>
        {
            var snapshot = await RequireAsync(connection, transaction, employeeId, equipmentId, attemptId, ct);
            var step = RequireStep(snapshot, requested);
            if (step.State == EquipmentAtsStep.Confirmed) return true;
            if (snapshot.Header.State != Open || step.State != EquipmentAtsStep.Dispatching)
                throw new InventoryOperationPendingException();

            await ChangeStepAsync(connection, transaction, attemptId, step, EquipmentAtsStep.Confirmed, ct);
            if (!snapshot.Steps.Any(item => item.StepIndex != step.StepIndex && item.State != EquipmentAtsStep.Confirmed))
                await FinishAsync(connection, transaction, attemptId, ct);
            return true;
        }, ct);

    internal static Task<bool> TryAbandonUndispatchedConfigureAsync(DbManager db, string? principal, int employeeId,
        int equipmentId, Guid attemptId, CancellationToken ct) =>
        WithAccessAsync(db, principal, employeeId, equipmentId, async (connection, transaction, _) =>
        {
            var snapshot = await RequireAsync(connection, transaction, employeeId, equipmentId, attemptId, ct);
            if (snapshot.Header.State == Abandoned) return true;
            if (snapshot.Header.State != Open || snapshot.Steps.Any(step => step.State != EquipmentAtsStep.Planned))
                return false;

            await WriteOneAsync(connection, transaction, SQLQueries.UPDATE_EquipmentAtsAbandoned,
                new { AttemptId = attemptId }, ct);
            return true;
        }, ct);

    internal static Task<bool> MatchesConfigureContextAsync(DbManager db, string? principal, int employeeId,
        int equipmentId, Guid attemptId, CancellationToken ct) =>
        WithAccessAsync(db, principal, employeeId, equipmentId, async (connection, transaction, equipment) =>
        {
            var snapshot = await RequireAsync(connection, transaction, employeeId, equipmentId, attemptId, ct);
            return snapshot.Header.State != Abandoned && ContextMatches(snapshot.Header, equipment,
                await ReadContextAsync(connection, transaction, equipmentId, ct));
        }, ct);

    private static bool ContextMatches(EquipmentAtsHeader header, Equipment equipment, ConfigureContext context) =>
        header.OwnerId == equipment.OwnerId && header.ExpectedChangedAtUtc == equipment.ChangedAtUtc &&
        header.ContextFingerprint == context.Fingerprint;

    private sealed record ConfigureContext(string Fingerprint, int[] Phones, EquipmentAtsStep[] Steps);

    private static async Task<ConfigureContext> ReadContextAsync(DbConnection connection, DbTransaction transaction,
        int equipmentId, CancellationToken ct)
    {
        var phones = (await connection.QueryAsync<EquipmentPhoneLink>(Command(
            SQLQueries.SELECT_ТелефонныеНомераОборудования, new { EquipmentId = equipmentId }, transaction, ct)))
            .OrderBy(link => link.PhoneId).ToArray();
        if (phones.Any(link => link.PhoneId <= 0 || link.EquipmentId != equipmentId) ||
            phones.Select(link => link.PhoneId).Distinct().Count() != phones.Length)
            throw new InvalidOperationException("Не удалось однозначно определить телефонные связи для настройки АТС.");

        var stations = new List<PhoneNumberLookup>(phones.Length);
        var steps = new List<EquipmentAtsStep>();
        foreach (var link in phones)
        {
            var phone = await connection.QuerySingleOrDefaultAsync<PhoneNumberLookup>(Command(
                SQLQueries.SELECT_ТелефонныйНомерПоИдентификатору, new { Id = link.PhoneId }, transaction, ct));
            if (phone?.Id != link.PhoneId)
                throw new InvalidOperationException("Телефонный номер для настройки АТС недоступен.");
            stations.Add(phone);
            if (!string.IsNullOrWhiteSpace(phone.XorcomName))
                steps.Add(new EquipmentAtsStep(steps.Count, EquipmentAtsStep.Xorcom, link.PhoneId, false, EquipmentAtsStep.Planned));
        }
        if (steps.Count == 0 && stations.Any(station => station.StationId == 0))
        {
            var targets = phones.Where(link => link.ConnectionEquipmentId is > 0)
                .Select(link => link.ConnectionEquipmentId!.Value).Distinct().Order().DefaultIfEmpty(equipmentId);
            foreach (var target in targets)
                steps.Add(new EquipmentAtsStep(steps.Count, EquipmentAtsStep.Ccm, target, false, EquipmentAtsStep.Planned));
        }

        var context = phones.Zip(stations, (link, station) => new
        {
            link.PhoneId, link.ConnectionEquipmentId, link.AutoDialNumber, link.AutoDialDelay, link.IsDefault,
            station.StationId, station.XorcomName,
        });
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(context)));
        return new ConfigureContext(fingerprint, phones.Select(phone => phone.PhoneId).ToArray(), steps.ToArray());
    }

    private static async Task<EquipmentAtsSnapshot?> ReadAsync(DbConnection connection, DbTransaction transaction,
        int employeeId, int equipmentId, Guid? attemptId, CancellationToken ct)
    {
        var header = attemptId is Guid id
            ? await connection.QuerySingleOrDefaultAsync<EquipmentAtsHeader>(Command(SQLQueries.SELECT_EquipmentAtsWorkflowForUpdate,
                new { AttemptId = id }, transaction, ct))
            : await connection.QuerySingleOrDefaultAsync<EquipmentAtsHeader>(Command(SQLQueries.SELECT_EquipmentAtsActiveConfigure,
                new { EmployeeId = employeeId, EquipmentId = equipmentId }, transaction, ct));
        if (header is null) return null;
        if (header.EmployeeId != employeeId || header.EquipmentId != equipmentId || header.Origin != Configure)
            throw new UnauthorizedAccessException("Настройка АТС не принадлежит текущему сотруднику и оборудованию.");
        var steps = await connection.QueryAsync<EquipmentAtsStep>(Command(SQLQueries.SELECT_EquipmentAtsSteps,
            new { header.AttemptId }, transaction, ct));
        return new EquipmentAtsSnapshot(header, steps.ToArray());
    }

    private static async Task<EquipmentAtsSnapshot> RequireAsync(DbConnection connection, DbTransaction transaction,
        int employeeId, int equipmentId, Guid attemptId, CancellationToken ct) =>
        await ReadAsync(connection, transaction, employeeId, equipmentId, attemptId, ct) ??
        throw new InvalidOperationException("Не найден точный серверный план настройки АТС.");

    private static EquipmentAtsStep RequireStep(EquipmentAtsSnapshot snapshot, EquipmentAtsStep requested) =>
        snapshot.Steps.SingleOrDefault(step => SameStep(step, requested)) ??
        throw new UnauthorizedAccessException("Цель не совпадает с серверной командой АТС.");

    private static bool SameStep(EquipmentAtsStep left, EquipmentAtsStep right) =>
        left.StepIndex == right.StepIndex && left.Kind == right.Kind && left.TargetId == right.TargetId && left.Deleting == right.Deleting;

    private static Task ChangeStepAsync(DbConnection connection, DbTransaction transaction, Guid attemptId,
        EquipmentAtsStep step, byte next, CancellationToken ct) =>
        WriteOneAsync(connection, transaction, SQLQueries.UPDATE_EquipmentAtsStepState, new
        {
            AttemptId = attemptId, step.StepIndex, step.Kind, step.TargetId, step.Deleting,
            ExpectedState = step.State, NextState = next,
        }, ct);

    private static Task FinishAsync(DbConnection connection, DbTransaction transaction, Guid attemptId, CancellationToken ct) =>
        WriteOneAsync(connection, transaction, SQLQueries.UPDATE_EquipmentAtsCompleted, new { AttemptId = attemptId }, ct);

    private static async Task WriteOneAsync(DbConnection connection, DbTransaction transaction, string sql,
        object arguments, CancellationToken ct)
    {
        if (await connection.ExecuteScalarAsync<int>(Command(sql, arguments, transaction, ct)) != 1)
            throw new InventoryOperationPendingException();
    }

    private static Task<T> WithAccessAsync<T>(DbManager db, string? principal, int employeeId, int equipmentId,
        Func<DbConnection, DbTransaction, Equipment, Task<T>> action, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(equipmentId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(employeeId);
        return db.RunDbAsync(async connection =>
        {
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var actor = await Employee.GetCurrentWithRolesAsync(connection, transaction, principal, ct);
            if (actor?.Id != employeeId)
                throw new UnauthorizedAccessException("Сотрудник для настройки АТС не подтверждён.");
            var equipment = await connection.QuerySingleOrDefaultAsync<Equipment>(Command(SQLQueries.SELECT_ID_Оборудование,
                new { Id = equipmentId }, transaction, ct));
            if (equipment?.Id != equipmentId || equipment.EquipmentTypeId != (int)ТипыОборудования.IP_телефон ||
                !actor.HasRole(Роли.Специалист_по_инвентаризации_оборудования, EmployeeRoleScope.PersonOrGlobal, equipment.OwnerId))
                throw new UnauthorizedAccessException("Настройка доступна для сохранённого IP-телефона с правом по его владельцу.");

            var result = await action(connection, transaction, equipment);
            await transaction.CommitAsync(CancellationToken.None);
            return result;
        });
    }

    private static CommandDefinition Command(string sql, object arguments, DbTransaction transaction, CancellationToken ct) =>
        new(sql, arguments, transaction, commandType: CommandType.Text, cancellationToken: ct);

    private static string Scope(string kind, int id) => kind + "/" + id.ToString(CultureInfo.InvariantCulture);
}
