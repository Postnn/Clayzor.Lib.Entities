using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Розетка для формы порта локальной сети.</summary>
public sealed class NetSocketLookup : ILookupEntity
{
    /// <summary>Идентификатор розетки.</summary>
    [Column(NetSocketColumns.КодРозетки)]
    public int Id { get; set; }

    /// <summary>Название розетки.</summary>
    [Column(NetSocketColumns.Розетка)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Примечание розетки.</summary>
    [Column(NetSocketColumns.Примечание)]
    public string Notes { get; set; } = string.Empty;

    /// <summary>Идентификатор расположения.</summary>
    [Column(NetSocketColumns.КодРасположения)]
    public int LocationId { get; set; }

    /// <summary>Признак работающей розетки.</summary>
    [Column(NetSocketColumns.Работает)]
    public bool IsActive { get; set; }
}
