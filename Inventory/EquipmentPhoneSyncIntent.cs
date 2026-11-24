namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Происхождение подтверждённого сервером контекста синхронизации АТС.</summary>
public enum EquipmentPhoneSyncOrigin
{
    /// <summary>Атомарное сохранение основной карточки и телефонного diff.</summary>
    EquipmentSave,

    /// <summary>Явная настройка сохранённого IP-телефона без основного UPDATE.</summary>
    ExplicitConfigure,
}

/// <summary>Неподписанный сериализуемый контекст; service подтверждает его по AttemptId перед каждым dispatch.</summary>
/// <param name="AttemptId">Стабильный идентификатор серверной попытки.</param>
/// <param name="EquipmentId">Идентификатор оборудования.</param>
/// <param name="Origin">Происхождение и требуемая авторизационная область.</param>
/// <param name="ExpectedChangedAtUtc">Исходный exact timestamp оборудования.</param>
/// <param name="OwnerId">Фактический владелец для explicit Configure.</param>
/// <param name="EquipmentTypeId">Фактический тип для explicit Configure.</param>
/// <param name="Original">Подтверждённые before-связи.</param>
/// <param name="Current">Подтверждённые after-связи.</param>
public sealed record EquipmentPhoneSyncIntent(
    string AttemptId,
    int EquipmentId,
    EquipmentPhoneSyncOrigin Origin,
    DateTime ExpectedChangedAtUtc,
    int? OwnerId,
    int? EquipmentTypeId,
    IReadOnlyCollection<EquipmentPhoneLink> Original,
    IReadOnlyCollection<EquipmentPhoneLink> Current);
