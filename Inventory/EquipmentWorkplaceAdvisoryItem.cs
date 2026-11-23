namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Другое оборудование той же группы на выбранном рабочем месте.</summary>
public sealed class EquipmentWorkplaceAdvisoryItem
{
    /// <summary>Идентификатор другого оборудования.</summary>
    public int EquipmentId { get; set; }

    /// <summary>Название модели.</summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>Сетевое имя, если оно указано.</summary>
    public string? NetworkName { get; set; }

    /// <summary>Текст строки предупреждения для отображения или ссылки на оборудование.</summary>
    public string DisplayText => string.IsNullOrWhiteSpace(NetworkName)
        ? ModelName
        : $"{ModelName} ({NetworkName})";
}
