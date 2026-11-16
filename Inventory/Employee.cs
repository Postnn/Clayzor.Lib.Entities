using System.ComponentModel.DataAnnotations.Schema;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>
/// Сотрудник для справочных списков и элементов выбора.
/// </summary>
[Table("Сотрудники")]
public sealed class Employee : ILookupEntity
{
    /// <summary>Идентификатор сотрудника.</summary>
    [Column(EmployeeColumns.КодСотрудника)]
    public int Id { get; set; }

    /// <summary>Отображаемое имя сотрудника.</summary>
    [Column(EmployeeColumns.Сотрудник)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Имя сотрудника на английском языке.</summary>
    [Column(EmployeeColumns.Employee)]
    public string? NameEn { get; set; }
}
