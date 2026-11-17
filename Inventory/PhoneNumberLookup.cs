using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Справочное значение телефонного номера и сведения его АТС.</summary>
public sealed class PhoneNumberLookup
{
    /// <summary>Идентификатор телефонного номера.</summary>
    [Column(PhoneColumns.КодТелефонногоНомера)]
    public int Id { get; set; }

    /// <summary>Отображаемое название номера.</summary>
    [Column(PhoneColumns.Телефон)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Идентификатор телефонной станции.</summary>
    [Column(PhoneColumns.КодТелефоннойСтанции)]
    public int StationId { get; set; }

    /// <summary>Имя станции Xorcom либо пустая строка для других АТС.</summary>
    [Column(PhoneColumns.XorcomName)]
    public string XorcomName { get; set; } = string.Empty;
}
