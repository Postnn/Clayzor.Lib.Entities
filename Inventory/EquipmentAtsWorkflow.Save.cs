using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

internal sealed record EquipmentAtsSaveCandidate(byte Kind, int TargetId, bool Deleting)
{
    internal const byte Phone = 1;
    internal const byte Ccm = 2;
}

internal sealed record EquipmentAtsSaveSnapshot(EquipmentAtsHeader Header, bool Prepared, IReadOnlyList<EquipmentAtsStep> Steps);

internal sealed class EquipmentAtsSaveConflictException()
    : InvalidOperationException("Сохранённые телефонные связи не совпали с зарезервированными целями АТС.");

internal static partial class EquipmentAtsWorkflow
{
    internal const string SaveCommand = "equipment-phone-save";
    private const byte Saved = (byte)EquipmentPhoneSyncOrigin.EquipmentSave;

    internal static async Task EnlistSavedAsync(DbConnection connection, DbTransaction transaction,
        InventoryOperationJournal.Attempt attempt, int equipmentId, IReadOnlyCollection<EquipmentPhoneLink> original,
        IReadOnlyCollection<EquipmentPhoneLink> submitted,
        CancellationToken ct)
    {
        var primaryScope = Scope("equipment", equipmentId);
        if (attempt.CommandKind != SaveCommand || attempt.ConflictKey != primaryScope ||
            await InventoryOperationJournal.ReadAttemptInTransactionAsync(connection, transaction, attempt, ct) is not
                { State: InventoryOperationState.Reserved })
            throw new InventoryOperationPendingException();

        var equipment = await ReadSavedEquipmentAsync(connection, transaction, equipmentId, ct);
        var links = await ReadSavedLinksAsync(connection, transaction, equipmentId, ct);
        if (!links.Select(link => link.PhoneId).SequenceEqual(submitted.Select(link => link.PhoneId).Order()))
            throw new EquipmentAtsSaveConflictException();

        var candidates = EquipmentPhoneSaveBuilder.AtsCandidates(equipmentId, original, links);
        var scopes = candidates.Select(candidate => Scope(candidate.Kind == EquipmentAtsSaveCandidate.Phone ? "tel" : "equipment",
                candidate.TargetId)).Append(primaryScope).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (scopes.Any(scope => scope != primaryScope && attempt.OtherScopes?.Contains(scope, StringComparer.Ordinal) != true))
            throw new EquipmentAtsSaveConflictException();

        var parameters = new DynamicParameters(new
        {
            attempt.AttemptId, attempt.EmployeeId, EquipmentId = equipmentId, equipment.OwnerId,
            ContextFingerprint = SavedLinksFingerprint(links), PlanPrepared = candidates.Length == 0,
        });
        parameters.Add("ExpectedChangedAtUtc", equipment.ChangedAtUtc, DbType.DateTime2);
        await WriteOneAsync(connection, transaction, SQLQueries.INSERT_EquipmentAtsSavedWorkflow, parameters, ct);

        if (candidates.Length == 0)
        {
            await FinishAsync(connection, transaction, attempt.AttemptId, ct);
            return;
        }

        // Исходные слоты SQL всё ещё удерживаются. Передача и подтверждение SQL будут зафиксированы вместе.
        foreach (var scope in scopes)
            await WriteOneAsync(connection, transaction, SQLQueries.INSERT_EquipmentAtsScope,
                new { attempt.AttemptId, ConflictKey = scope, OpenSlot = Guid.Empty }, ct);
        foreach (var target in candidates)
            await WriteOneAsync(connection, transaction, SQLQueries.INSERT_EquipmentAtsSaveCandidate,
                new { attempt.AttemptId, target.Kind, target.TargetId, target.Deleting }, ct);
    }

    internal static Task<EquipmentAtsSaveSnapshot?> ReadSavedAsync(DbManager db, string? principal, int employeeId,
        int equipmentId, Guid? attemptId, CancellationToken ct) =>
        WithSavedAccessAsync(db, principal, employeeId, equipmentId,
            (connection, transaction, _) => ReadSavedAsync(connection, transaction, employeeId, equipmentId, attemptId, ct), ct);

    internal static Task<bool> BeginSavedStepAsync(DbManager db, string? principal, int employeeId, int equipmentId,
        Guid attemptId, EquipmentAtsStep requested, CancellationToken ct) =>
        WithSavedAccessAsync(db, principal, employeeId, equipmentId, async (connection, transaction, equipment) =>
        {
            var snapshot = await RequireSavedAsync(connection, transaction, employeeId, equipmentId, attemptId, ct);
            var step = RequireStep(new EquipmentAtsSnapshot(snapshot.Header, snapshot.Steps), requested);
            if (step.State == EquipmentAtsStep.Confirmed) return false;
            if (!snapshot.Prepared || snapshot.Header.State != Open || step.State != EquipmentAtsStep.Planned ||
                snapshot.Steps.Any(item => item.StepIndex < step.StepIndex && item.State != EquipmentAtsStep.Confirmed))
                throw new InventoryOperationPendingException();
            if (snapshot.Header.OwnerId != equipment.OwnerId || snapshot.Header.ExpectedChangedAtUtc != equipment.ChangedAtUtc)
                throw new EquipmentAtsContextChangedException();

            var candidates = await ReadSavedCandidatesAsync(connection, transaction, attemptId, ct);
            var context = await ReadSavedPlanContextAsync(connection, transaction, equipmentId, candidates, ct);
            if (snapshot.Header.ContextFingerprint != context.Fingerprint || !context.Steps.Any(item => SameStep(item, step)))
                throw new EquipmentAtsContextChangedException();

            await ChangeStepAsync(connection, transaction, attemptId, step, EquipmentAtsStep.Dispatching, ct);
            return true;
        }, ct);

    internal static Task ConfirmSavedStepAsync(DbManager db, string? principal, int employeeId, int equipmentId,
        Guid attemptId, EquipmentAtsStep requested, CancellationToken ct) =>
        WithSavedAccessAsync(db, principal, employeeId, equipmentId, async (connection, transaction, _) =>
        {
            var snapshot = await RequireSavedAsync(connection, transaction, employeeId, equipmentId, attemptId, ct);
            var step = RequireStep(new EquipmentAtsSnapshot(snapshot.Header, snapshot.Steps), requested);
            if (step.State == EquipmentAtsStep.Confirmed) return true;
            if (!snapshot.Prepared || snapshot.Header.State != Open || step.State != EquipmentAtsStep.Dispatching)
                throw new InventoryOperationPendingException();

            await ChangeStepAsync(connection, transaction, attemptId, step, EquipmentAtsStep.Confirmed, ct);
            if (!snapshot.Steps.Any(item => item.StepIndex != step.StepIndex && item.State != EquipmentAtsStep.Confirmed))
                await FinishAsync(connection, transaction, attemptId, ct);
            return true;
        }, ct);

    internal static Task<bool> TryAbandonSavedAsync(DbManager db, string? principal, int employeeId, int equipmentId,
        Guid attemptId, CancellationToken ct) =>
        WithSavedAccessAsync(db, principal, employeeId, equipmentId, async (connection, transaction, _) =>
        {
            var snapshot = await RequireSavedAsync(connection, transaction, employeeId, equipmentId, attemptId, ct);
            if (snapshot.Header.State == Abandoned) return true;
            if (snapshot.Header.State != Open || snapshot.Steps.Any(step => step.State != EquipmentAtsStep.Planned))
                return false;

            await WriteOneAsync(connection, transaction, SQLQueries.UPDATE_EquipmentAtsAbandoned,
                new { AttemptId = attemptId }, ct);
            return true;
        }, ct);

    internal static Task<bool> MatchesSavedContextAsync(DbManager db, string? principal, int employeeId, int equipmentId,
        Guid attemptId, CancellationToken ct) =>
        WithSavedAccessAsync(db, principal, employeeId, equipmentId, async (connection, transaction, equipment) =>
        {
            var snapshot = await RequireSavedAsync(connection, transaction, employeeId, equipmentId, attemptId, ct);
            var header = snapshot.Header;
            if (header.State == Abandoned || header.OwnerId != equipment.OwnerId || header.ExpectedChangedAtUtc != equipment.ChangedAtUtc)
                return false;

            var candidates = await ReadSavedCandidatesAsync(connection, transaction, attemptId, ct);
            if (!snapshot.Prepared || candidates.Length == 0)
                return header.ContextFingerprint == SavedLinksFingerprint(await ReadSavedLinksAsync(connection, transaction, equipmentId, ct));

            return header.ContextFingerprint == (await ReadSavedPlanContextAsync(connection, transaction, equipmentId, candidates, ct)).Fingerprint;
        }, ct);

    private static async Task<EquipmentAtsSaveSnapshot?> ReadSavedAsync(DbConnection connection, DbTransaction transaction,
        int employeeId, int equipmentId, Guid? attemptId, CancellationToken ct)
    {
        var header = attemptId is Guid id
            ? await connection.QuerySingleOrDefaultAsync<EquipmentAtsHeader>(Command(SQLQueries.SELECT_EquipmentAtsWorkflowForUpdate,
                new { AttemptId = id }, transaction, ct))
            : await connection.QuerySingleOrDefaultAsync<EquipmentAtsHeader>(Command(SQLQueries.SELECT_EquipmentAtsActiveSave,
                new { EmployeeId = employeeId, EquipmentId = equipmentId }, transaction, ct));
        if (header is null) return null;
        if (header.EmployeeId != employeeId || header.EquipmentId != equipmentId || header.Origin != Saved)
            throw new UnauthorizedAccessException("Попытка синхронизации не принадлежит текущему сотруднику и оборудованию.");

        var prepared = await connection.QuerySingleAsync<bool>(Command(SQLQueries.SELECT_EquipmentAtsSavePlanPrepared,
            new { header.AttemptId }, transaction, ct));
        var steps = prepared
            ? (await connection.QueryAsync<EquipmentAtsStep>(Command(SQLQueries.SELECT_EquipmentAtsSteps,
                new { header.AttemptId }, transaction, ct))).ToArray() : [];
        return new EquipmentAtsSaveSnapshot(header, prepared, steps);
    }

    private static async Task<EquipmentAtsSaveSnapshot> RequireSavedAsync(DbConnection connection, DbTransaction transaction,
        int employeeId, int equipmentId, Guid attemptId, CancellationToken ct) =>
        await ReadSavedAsync(connection, transaction, employeeId, equipmentId, attemptId, ct) ??
        throw new InvalidOperationException("Не найден серверный план синхронизации сохранённых телефонных связей.");

    private static async Task<EquipmentAtsSaveCandidate[]> ReadSavedCandidatesAsync(DbConnection connection,
        DbTransaction transaction, Guid attemptId, CancellationToken ct) =>
        (await connection.QueryAsync<EquipmentAtsSaveCandidate>(Command(SQLQueries.SELECT_EquipmentAtsSaveCandidates,
            new { AttemptId = attemptId }, transaction, ct))).ToArray();

    private static async Task<EquipmentPhoneLink[]> ReadSavedLinksAsync(DbConnection connection, DbTransaction transaction,
        int equipmentId, CancellationToken ct)
    {
        var links = (await connection.QueryAsync<EquipmentPhoneLink>(Command(SQLQueries.SELECT_ТелефонныеНомераОборудования,
            new { EquipmentId = equipmentId }, transaction, ct))).OrderBy(link => link.PhoneId).ToArray();
        if (links.Any(link => link.PhoneId <= 0 || link.EquipmentId != equipmentId) ||
            links.Select(link => link.PhoneId).Distinct().Count() != links.Length)
            throw new InvalidOperationException("Не удалось однозначно определить сохранённые телефонные связи.");
        return links;
    }

    private static string SavedLinksFingerprint(IReadOnlyList<EquipmentPhoneLink> links)
    {
        var data = links.Select(link => new
        {
            link.PhoneId, link.ConnectionEquipmentId, link.AutoDialNumber, link.AutoDialDelay, link.IsDefault,
        });
        return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(data)));
    }

    private static async Task<Equipment> ReadSavedEquipmentAsync(DbConnection connection, DbTransaction transaction,
        int equipmentId, CancellationToken ct) =>
        await connection.QuerySingleOrDefaultAsync<Equipment>(Command(SQLQueries.SELECT_ID_Оборудование,
            new { Id = equipmentId }, transaction, ct)) is { } equipment && equipment.Id == equipmentId
            ? equipment : throw new InvalidOperationException("Сохранённое оборудование недоступно для сверки АТС.");

    private static Task<T> WithSavedAccessAsync<T>(DbManager db, string? principal, int employeeId, int equipmentId,
        Func<DbConnection, DbTransaction, Equipment, Task<T>> action, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(equipmentId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(employeeId);
        return db.RunDbAsync(async connection =>
        {
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var actor = await Employee.GetCurrentWithRolesAsync(connection, transaction, principal, ct);
            if (actor?.Id != employeeId || !actor.HasRole(Роли.Специалист_по_инвентаризации_оборудования))
                throw new UnauthorizedAccessException("Сотрудник для синхронизации оборудования не подтверждён.");

            var equipment = await ReadSavedEquipmentAsync(connection, transaction, equipmentId, ct);
            var result = await action(connection, transaction, equipment);
            await transaction.CommitAsync(CancellationToken.None);
            return result;
        });
    }
}
