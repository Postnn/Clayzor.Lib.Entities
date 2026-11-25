using System.ComponentModel.DataAnnotations.Schema;
using Clayzor.Lib.DALC;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Область проверки роли сотрудника.</summary>
public enum EmployeeRoleScope
{
    /// <summary>Роль доступна в любой области.</summary>
    Any,
    /// <summary>Роль назначена глобально с КодЛица=0.</summary>
    Global,
    /// <summary>Роль назначена конкретному лицу или глобально; при неизвестном лице допускается только global.</summary>
    PersonOrGlobal,
    /// <summary>Роль назначена именно конкретному лицу без учёта global.</summary>
    PersonOnly,
}

/// <summary>Строка снимка effective roles текущего сотрудника.</summary>
internal sealed class EmployeeRoleGrant
{
    public int RoleId { get; set; }
    public int PersonId { get; set; }
}

/// <summary>
/// Сотрудник для справочных списков, элементов выбора и проверки текущей идентичности.
/// </summary>
[Table("Сотрудники")]
public sealed class Employee : ILookupEntity
{
    private IReadOnlyCollection<EmployeeRoleGrant>? _roleGrants;

    /// <summary>Идентификатор сотрудника.</summary>
    [Column(EmployeeColumns.КодСотрудника)]
    public int Id { get; set; }

    /// <summary>Отображаемое имя сотрудника.</summary>
    [Column(EmployeeColumns.Сотрудник)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Имя сотрудника на английском языке.</summary>
    [Column(EmployeeColumns.Employee)]
    public string? NameEn { get; set; }

    /// <summary>Язык, сохранённый для текущего сотрудника.</summary>
    [Column(EmployeeColumns.Язык)]
    public string? Language { get; set; }

    /// <summary>Русский язык используется при отсутствии сохранённого языка.</summary>
    public bool PrefersRussian => string.IsNullOrWhiteSpace(Language) ||
        string.Equals(Language.Trim(), "ru", StringComparison.OrdinalIgnoreCase);

    /// <summary>Организация сотрудника.</summary>
    [Column(EmployeeColumns.КодЛицаЗаказчика)]
    public int? OrganizationId { get; set; }

    /// <summary>Название организации сотрудника.</summary>
    [Column(EmployeeColumns.Организация)]
    public string OrganizationName { get; set; } = string.Empty;

    /// <summary>Проверяет роль в загруженном UX-снимке; не заменяет свежую проверку перед записью.</summary>
    public bool HasRole(Роли role, EmployeeRoleScope scope = EmployeeRoleScope.Any, int? personId = null)
    {
        if (_roleGrants is null)
            return false;
        var roles = _roleGrants.Where(grant => grant.RoleId == (int)role);
        return scope switch
        {
            EmployeeRoleScope.Any => roles.Any(),
            EmployeeRoleScope.Global => roles.Any(grant => grant.PersonId == 0),
            EmployeeRoleScope.PersonOrGlobal => roles.Any(grant => grant.PersonId == 0 ||
                personId is > 0 && grant.PersonId == personId.Value),
            EmployeeRoleScope.PersonOnly => personId is > 0 && roles.Any(grant => grant.PersonId == personId.Value),
            _ => false,
        };
    }

    internal void SetRoleGrants(IEnumerable<EmployeeRoleGrant> grants) => _roleGrants = grants.ToArray();

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

    /// <summary>Получает текущего сотрудника и единый снимок effective roles для доступности элементов формы.</summary>
    public static async Task<Employee?> GetCurrentWithRolesAsync(
        DbManager db,
        string? windowsPrincipal,
        CancellationToken cancellationToken = default)
    {
        var employee = await GetCurrentAsync(db, windowsPrincipal, cancellationToken);
        if (employee is null)
            return null;
        var roles = await db.QueryRequiredAsync<EmployeeRoleGrant>(SQLQueries.SELECT_ТекущиеРолиДляСотрудника,
            null, cancellationToken);
        employee.SetRoleGrants(roles);
        return employee;
    }

    /// <summary>Проверяет роль после свежего подтверждения Windows/SQL principal.</summary>
    public static Task<bool> HasRoleAsync(
        DbManager db,
        string? windowsPrincipal,
        Роли role,
        int? personId = null,
        CancellationToken cancellationToken = default) =>
        HasRoleAsync(db, windowsPrincipal, role,
            personId is null ? EmployeeRoleScope.Any : EmployeeRoleScope.PersonOrGlobal,
            personId, cancellationToken);

    /// <summary>Проверяет роль в явно заданной области после свежего подтверждения идентичности.</summary>
    public static async Task<bool> HasRoleAsync(
        DbManager db,
        string? windowsPrincipal,
        Роли role,
        EmployeeRoleScope scope,
        int? personId = null,
        CancellationToken cancellationToken = default)
    {
        if (await GetCurrentAsync(db, windowsPrincipal, cancellationToken) is null)
            return false;
        if (scope == EmployeeRoleScope.PersonOnly && personId is not > 0)
            return false;
        var sql = scope switch
        {
            EmployeeRoleScope.Any => SQLQueries.SELECT_ТекущийПользовательИмеетРоль,
            EmployeeRoleScope.Global => SQLQueries.SELECT_ТекущийПользовательИмеетГлобальнуюРоль,
            EmployeeRoleScope.PersonOnly => SQLQueries.SELECT_ТекущийПользовательИмеетРольТолькоПоЛицу,
            EmployeeRoleScope.PersonOrGlobal when personId is > 0 => SQLQueries.SELECT_ТекущийПользовательИмеетРольПоЛицу,
            EmployeeRoleScope.PersonOrGlobal => SQLQueries.SELECT_ТекущийПользовательИмеетГлобальнуюРоль,
            _ => throw new ArgumentOutOfRangeException(nameof(scope)),
        };
        var rows = await db.QueryRequiredAsync<int>(sql,
            new { RoleId = (int)role, PersonId = personId }, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return rows.FirstOrDefault() == 1;
    }
}
