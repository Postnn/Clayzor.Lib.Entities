using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Ссылка оборудования на порт локальной сети, найденная по сетевому имени в поле «Подключено».</summary>
public sealed class EquipmentNetworkPortLink
{
    /// <summary>Идентификатор порта локальной сети.</summary>
    [Column(LocalNetworkPortColumns.КодЛокальнойСети)]
    public int Id { get; set; }

    /// <summary>Сетевое имя оборудования порта.</summary>
    [Column(EquipmentColumns.СетевоеИмя)]
    public string NetworkName { get; set; } = string.Empty;

    /// <summary>Номер порта.</summary>
    [Column(LocalNetworkPortColumns.Порт)]
    public byte Port { get; set; }

    /// <summary>Отображаемая подпись «СетевоеИмя_Порт».</summary>
    public string DisplayName => $"{NetworkName}_{Port}";
}

/// <summary>Диагностические правила обратного поиска сетевых портов legacy DisplayNet.</summary>
public static class EquipmentNetworkDiagnostics
{
    /// <summary>Сравнивает число портов с Model.Net и проверяет наличие двух MAC для двух портов.</summary>
    public static bool HasWarning(int portCount, int modelNetworkInterfaces,
        string? macAddress, string? macAddress2, int? equipmentTypeId)
    {
        var warning = portCount > modelNetworkInterfaces ||
                      portCount == 2 && (string.IsNullOrEmpty(macAddress) || string.IsNullOrEmpty(macAddress2));
        if (equipmentTypeId == (int)Clayzor.Lib.Entities.ТипыОборудования.Коммутатор ||
            equipmentTypeId == (int)Clayzor.Lib.Entities.ТипыОборудования.Маршрутизатор)
            return portCount >= 3 && warning;
        return warning;
    }

    /// <summary>Скрывает секцию switch/router при трёх и более найденных портах.</summary>
    public static bool HideSection(int portCount, int? equipmentTypeId) =>
        portCount >= 3 &&
        (equipmentTypeId == (int)Clayzor.Lib.Entities.ТипыОборудования.Коммутатор ||
         equipmentTypeId == (int)Clayzor.Lib.Entities.ТипыОборудования.Маршрутизатор);
}
