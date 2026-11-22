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
