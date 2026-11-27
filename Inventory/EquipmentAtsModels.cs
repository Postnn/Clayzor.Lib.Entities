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
