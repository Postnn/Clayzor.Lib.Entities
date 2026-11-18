using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Секретные коды SIM, загружаемые только отдельным запросом.</summary>
public sealed class EquipmentSimSecrets
{
    /// <summary>PIN-код.</summary>
    [Column("PIN")]
    public string Pin { get; set; } = string.Empty;

    /// <summary>PIN2-код.</summary>
    [Column("PIN2")]
    public string Pin2 { get; set; } = string.Empty;

    /// <summary>PUK-код.</summary>
    [Column("PUK")]
    public string Puk { get; set; } = string.Empty;

    /// <summary>PUK2-код.</summary>
    [Column("PUK2")]
    public string Puk2 { get; set; } = string.Empty;
}
