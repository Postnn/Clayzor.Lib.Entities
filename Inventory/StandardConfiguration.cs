using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Стандартная конфигурация компьютера.</summary>
public sealed class StandardConfiguration : ILookupEntity
{
    /// <summary>Идентификатор конфигурации.</summary>
    [Column(StandardConfigurationColumns.КодСтандартнойКонфигурации)]
    public int Id { get; set; }

    /// <summary>Название конфигурации.</summary>
    [Column(StandardConfigurationColumns.СтандартнаяКонфигурация)]
    public string Name { get; set; } = string.Empty;
}
