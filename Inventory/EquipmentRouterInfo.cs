using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Основные сведения маршрутизатора, доступные только для чтения.</summary>
public sealed class EquipmentRouterInfo
{
    /// <summary>Идентификатор оборудования.</summary>
    [Column(EquipmentColumns.КодОборудования)]
    public int EquipmentId { get; set; }

    /// <summary>Сетевое имя.</summary>
    [Column(RouterColumns.СетевоеИмя)]
    public string NetworkName { get; set; } = string.Empty;

    /// <summary>Голосовой шлюз.</summary>
    [Column(RouterColumns.ГолосовойШлюз)]
    public string VoiceGateway { get; set; } = string.Empty;

    /// <summary>Версия ОС.</summary>
    [Column(RouterColumns.OSVersion)]
    public string OsVersion { get; set; } = string.Empty;

    /// <summary>Объём RAM.</summary>
    [Column(RouterColumns.ProcRAM)]
    public long RamBytes { get; set; }

    /// <summary>Свободный объём RAM.</summary>
    [Column(RouterColumns.ProcRAMfree)]
    public long FreeRamBytes { get; set; }

    /// <summary>Объём Flash.</summary>
    [Column(RouterColumns.Flash)]
    public long FlashBytes { get; set; }

    /// <summary>Свободный объём Flash.</summary>
    [Column(RouterColumns.FlashFree)]
    public long FreeFlashBytes { get; set; }

    /// <summary>Номер BGP.</summary>
    [Column(RouterColumns.BGP)]
    public int Bgp { get; set; }

    /// <summary>Идентификатор маршрутизатора.</summary>
    [Column(RouterColumns.RouterID)]
    public string RouterId { get; set; } = string.Empty;

    /// <summary>Описание конфигурации.</summary>
    [Column(RouterColumns.Description)]
    public string Description { get; set; } = string.Empty;
}
