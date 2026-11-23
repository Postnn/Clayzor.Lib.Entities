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

    /// <summary>Название организации сотрудника.</summary>
    [Column(EmployeeColumns.Организация)]
    public string OrganizationName { get; set; } = string.Empty;

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

    /// <summary>Проверяет роль после свежего подтверждения Windows/SQL principal и глобальной или личной области.</summary>
    public static async Task<bool> HasRoleAsync(
        DbManager db,
        string? windowsPrincipal,
        Роли role,
        int? personId = null,
        CancellationToken cancellationToken = default)
    {
        if (await GetCurrentAsync(db, windowsPrincipal, cancellationToken) is null)
            return false;
        var sql = personId is null
            ? SQLQueries.SELECT_ТекущийПользовательИмеетРоль
            : SQLQueries.SELECT_ТекущийПользовательИмеетРольПоЛицу;
        var rows = await db.QueryRequiredAsync<int>(sql,
            new { RoleId = (int)role, PersonId = personId }, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return rows.FirstOrDefault() == 1;
    }
}
