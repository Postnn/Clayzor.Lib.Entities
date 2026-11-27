using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

internal static partial class EquipmentAtsWorkflow
{
    internal static Task<EquipmentAtsSaveSnapshot> PrepareSavedAsync(DbManager db, string? principal, int employeeId,
        int equipmentId, Guid attemptId, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(attemptId, Guid.Empty);
        return WithSavedAccessAsync(db, principal, employeeId, equipmentId, async (connection, transaction, equipment) =>
        {
            var snapshot = await RequireSavedAsync(connection, transaction, employeeId, equipmentId, attemptId, ct);
            var header = snapshot.Header;
            if (header.State == Abandoned) return snapshot;
            if (header.State is not (Open or Completed))
                throw new InventoryOperationPendingException();
            if (snapshot.Prepared) return snapshot;
            if (header.State != Open || snapshot.Steps.Count != 0)
                throw new InventoryOperationPendingException();
            if (header.OwnerId != equipment.OwnerId || header.ExpectedChangedAtUtc != equipment.ChangedAtUtc)
                throw new EquipmentAtsContextChangedException();

            var links = await ReadSavedLinksAsync(connection, transaction, equipmentId, ct);
            var linksFingerprint = SavedLinksFingerprint(links);
            if (!string.Equals(header.ContextFingerprint, linksFingerprint, StringComparison.Ordinal))
                throw new EquipmentAtsContextChangedException();

            var candidates = await ReadSavedCandidatesAsync(connection, transaction, attemptId, ct);
            var plan = await ReadSavedPlanContextAsync(connection, transaction, links, linksFingerprint, candidates, ct);
            foreach (var step in plan.Steps)
            {
                await WriteOneAsync(connection, transaction, SQLQueries.INSERT_EquipmentAtsStep,
                    new { AttemptId = attemptId, step.StepIndex, step.Kind, step.TargetId, step.Deleting }, ct);
            }
            await WriteOneAsync(connection, transaction, SQLQueries.UPDATE_EquipmentAtsSavePlanPrepared,
                new { AttemptId = attemptId, ContextFingerprint = plan.Fingerprint }, ct);
            if (plan.Steps.Length == 0)
                await FinishAsync(connection, transaction, attemptId, ct);

            return new EquipmentAtsSaveSnapshot(header with
            {
                ContextFingerprint = plan.Fingerprint,
                State = plan.Steps.Length == 0 ? Completed : Open,
            }, true, plan.Steps);
        }, ct);
    }

    private sealed record SavedPlanContext(string Fingerprint, EquipmentAtsStep[] Steps);

    private static async Task<SavedPlanContext> ReadSavedPlanContextAsync(DbConnection connection, DbTransaction transaction,
        int equipmentId, EquipmentAtsSaveCandidate[] candidates, CancellationToken ct)
    {
        var links = await ReadSavedLinksAsync(connection, transaction, equipmentId, ct);
        return await ReadSavedPlanContextAsync(connection, transaction, links, SavedLinksFingerprint(links), candidates, ct);
    }

    private static async Task<SavedPlanContext> ReadSavedPlanContextAsync(DbConnection connection, DbTransaction transaction,
        IReadOnlyCollection<EquipmentPhoneLink> links, string linksFingerprint, EquipmentAtsSaveCandidate[] candidates,
        CancellationToken ct)
    {
        if (candidates.Any(candidate => candidate.TargetId <= 0 ||
                candidate.Kind is not (EquipmentAtsSaveCandidate.Phone or EquipmentAtsSaveCandidate.Ccm) ||
                candidate.Kind == EquipmentAtsSaveCandidate.Ccm && candidate.Deleting) ||
            candidates.Select(candidate => (candidate.Kind, candidate.TargetId)).Distinct().Count() != candidates.Length)
            throw new EquipmentAtsContextChangedException();

        var affectedPhones = candidates.Where(candidate => candidate.Kind == EquipmentAtsSaveCandidate.Phone)
            .OrderBy(candidate => candidate.TargetId).ToArray();
        if (affectedPhones.Length == 0)
            throw new EquipmentAtsContextChangedException(); // Пустой план завершается ещё в транзакции сохранения.

        var ccmTargets = candidates.Where(candidate => candidate.Kind == EquipmentAtsSaveCandidate.Ccm)
            .OrderBy(candidate => candidate.TargetId).Select(candidate => candidate.TargetId).ToArray();
        var currentPhoneIds = links.Select(link => link.PhoneId).ToHashSet();
        var stations = new List<(EquipmentAtsSaveCandidate Candidate, PhoneNumberLookup Station)>(affectedPhones.Length);
        var steps = new List<EquipmentAtsStep>();
        foreach (var candidate in affectedPhones)
        {
            var phoneId = candidate.TargetId;
            if (candidate.Deleting == currentPhoneIds.Contains(phoneId))
                throw new EquipmentAtsContextChangedException();
            if (candidate.Deleting)
            {
                var linksAfterDelete = await connection.QueryAsync<int>(Command(
                    SQLQueries.SELECT_ТелефонныйНомерАктуальныеПривязки, new { PhoneId = phoneId }, transaction, ct));
                if (linksAfterDelete.Any())
                    throw new EquipmentAtsContextChangedException();
            }

            var station = await connection.QuerySingleOrDefaultAsync<PhoneNumberLookup>(Command(
                SQLQueries.SELECT_ТелефонныйНомерПоИдентификатору, new { Id = phoneId }, transaction, ct));
            if (station is null || station.Id != phoneId)
                throw new EquipmentAtsPlanningUnavailableException();
            stations.Add((candidate, station));
            if (!string.IsNullOrWhiteSpace(station.XorcomName))
                steps.Add(new EquipmentAtsStep(steps.Count, EquipmentAtsStep.Xorcom, phoneId, candidate.Deleting,
                    EquipmentAtsStep.Planned));
        }

        if (steps.Count == 0 && stations.Any(item => item.Station.StationId == 0))
        {
            if (ccmTargets.Length == 0)
                throw new EquipmentAtsContextChangedException();
            foreach (var target in ccmTargets)
                steps.Add(new EquipmentAtsStep(steps.Count, EquipmentAtsStep.Ccm, target, false, EquipmentAtsStep.Planned));
        }

        var context = new
        {
            Links = linksFingerprint,
            Phones = stations.Select(item => new
            {
                PhoneId = item.Candidate.TargetId, item.Candidate.Deleting,
                item.Station.StationId, item.Station.XorcomName,
            }),
            CcmTargets = ccmTargets,
        };
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(context)));
        return new SavedPlanContext(fingerprint, steps.ToArray());
    }
}

internal sealed class EquipmentAtsPlanningUnavailableException()
    : InvalidOperationException("Не удалось определить телефонную станцию для синхронизации.");
