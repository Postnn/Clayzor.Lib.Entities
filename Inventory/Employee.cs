using System.ComponentModel.DataAnnotations.Schema;
using Clayzor.Lib.DALC;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>
/// Сотрудник для справочных списков, элементов выбора и проверки текущей идентичности.
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

    /// <summary>Организация сотрудника.</summary>
    [Column(EmployeeColumns.КодЛицаЗаказчика)]
    public int? OrganizationId { get; set; }

    /// <summary>Получает текущего сотрудника только при совпадении серверного Windows principal и SQL principal.</summary>
    public static async Task<Employee?> GetCurrentAsync(
        DbManager db,
        string? windowsPrincipal,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(windowsPrincipal))
            return null;
        var rows = await db.QueryRequiredAsync<Employee>(SQLQueries.SELECT_ТекущийСотрудникПоPrincipal,
            new { WindowsPrincipal = windowsPrincipal }, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return rows.SingleOrDefault();
    }
}
