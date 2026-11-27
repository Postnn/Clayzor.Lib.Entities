using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Договор Билайн и его неизменяемые реквизиты с версией редактируемых полей.</summary>
public sealed record BeelineContract(string Number, string BaseNumber, string BudgetNumber, string BudgetPhone,
    int? EquipmentId, string? Customer, string UserName, DateTime Actual, string TimeZone, string EmailManager,
    bool HasAccount, int ChangedBy, DateTime ChangedAt, string? ChangedByName)
{
    /// <summary>Признак мобильного бюджета определяется номером телефона.</summary>
    public bool MobileBudget => !string.IsNullOrEmpty(BudgetPhone);
}

/// <summary>Два изменяемых параметра договора.</summary>
public sealed record BeelineDraft(string TimeZone, string EmailManager);

/// <summary>Проверка ключа и редактируемых полей по подтверждённой схеме и правилам формы.</summary>
public static class BeelineRules
{
    private static readonly Regex Email = new(
        @"^[a-z0-9_]+([-+.'][a-z0-9_]+)*@[a-z0-9_]+([-.][a-z0-9_]+)*\.\w+([-.][a-z0-9_]+)*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Encoding SqlEncoding = CodePagesEncodingProvider.Instance.GetEncoding(
        1251, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)!;

    /// <summary>Ключ nvarchar(50) сохраняется как строка, включая ведущие нули.</summary>
    public static bool ValidNumber(string? number) =>
        !string.IsNullOrWhiteSpace(number) && number.Length <= 50 && !number.Any(char.IsControl);

    /// <summary>Проверяет обязательность, допустимый Windows key и точное представление varchar.</summary>
    public static IReadOnlyList<string> Validate(BeelineDraft draft, IReadOnlyCollection<string> zones)
    {
        var errors = new List<string>();
        if (string.IsNullOrEmpty(draft.TimeZone))
            errors.Add("Необходимо указать Time Zone!");
        else if (!FitsSql(draft.TimeZone, 32) || !zones.Contains(draft.TimeZone, StringComparer.Ordinal))
            errors.Add("Выберите Time Zone из настроенного списка.");

        if (string.IsNullOrEmpty(draft.EmailManager))
            errors.Add("Необходимо указать Email менеджера!");
        else if (!FitsSql(draft.EmailManager, 50) || draft.EmailManager.Any(char.IsControl) || !Email.IsMatch(draft.EmailManager))
            errors.Add("Email менеджера указан некорректно!");

        return errors;
    }

    private static bool FitsSql(string text, int max)
    {
        if (text.Length > max) return false;
        try { return SqlEncoding.GetByteCount(text) <= max; }
        catch (EncoderFallbackException) { return false; }
    }
}

/// <summary>Подтверждённый отказ или конфликт параметров договора.</summary>
public sealed class BeelineRejectedException(string message, Exception? inner = null) : InvalidOperationException(message, inner);

/// <summary>Читает договор и обновляет параметры с повторной проверкой identity и исходной версии.</summary>
public static class BeelineData
{
    /// <summary>Читает договор и доступность изменения параметров для текущего сотрудника.</summary>
    public static Task<(BeelineContract? Contract, bool CanUpdate)> ReadAsync(DbManager db, Employee actor, string number,
        CancellationToken ct) => db.RunDbAsync(async connection =>
    {
        if (!BeelineRules.ValidNumber(number)) throw new BeelineRejectedException("Некорректный номер договора.");
        var access = await ReadAccessAsync(connection, null, actor, ct);
        if (!access.CanRead) throw new UnauthorizedAccessException("Нет доступа к параметрам договора Билайн.");
        return (await ReadContractAsync(connection, null, number, ct), access.CanUpdate);
    });

    /// <summary>Обновляет Time Zone и email после проверки прав и исходной версии.</summary>
    public static async Task<BeelineContract?> SaveAsync(DbManager db, string? principal, int expectedEmployee,
        BeelineContract original, BeelineDraft draft, IReadOnlyCollection<string> configuredZones, CancellationToken ct)
    {
        BeelineContract expected;
        try
        {
            if (!BeelineRules.ValidNumber(original.Number)) throw new BeelineRejectedException("Некорректный номер договора.");
            var errors = BeelineRules.Validate(draft, configuredZones);
            if (errors.Count > 0) throw new BeelineRejectedException(string.Join(" ", errors));

            (_, expected) = await db.RunDbAsync(connection => CheckWriteAsync(connection, null, ct));
            EnsureBaseline(expected);
        }
        catch (Exception e) when (e is not BeelineRejectedException)
        {
            throw new BeelineRejectedException("Проверка данных завершилась ошибкой. Запись не начата.", e);
        }

        return await db.RunDbAsync(async connection =>
        {
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            BeelineContract current;
            BeelineContract? saved;
            try
            {
                (_, current) = await CheckWriteAsync(connection, transaction, ct);
                EnsureBaseline(current);
                ct.ThrowIfCancellationRequested();
                if (!string.Equals(current.Number, expected.Number, StringComparison.Ordinal))
                    throw new BeelineRejectedException("Номер договора изменился. Сверьте исходные данные.");

                var update = Command(SQLQueries.UPDATE_БилайнПараметры,
                    new { id = Key(current.Number), timeZone = draft.TimeZone, email = draft.EmailManager }, transaction, ct);
                var affected = await connection.QuerySingleAsync<int>(update);
                if (affected != 1) throw new BeelineRejectedException("База не подтвердила обновление одного договора.");

                saved = await ReadContractAsync(connection, transaction, current.Number, ct);
                if (saved is null || saved.TimeZone != draft.TimeZone || saved.EmailManager != draft.EmailManager)
                    throw new BeelineRejectedException("База не сохранила введённые параметры. Для мобильного бюджета проверьте настройки основного договора.");
            }
            catch (Exception e) when (e is not (BeelineRejectedException or TimeoutException or OperationCanceledException) &&
                (e is not Microsoft.Data.SqlClient.SqlException sql || sql.Number != -2 && !DbManager.IsConnectivityError(sql)) &&
                transaction.Connection is not null)
            {
                throw new BeelineRejectedException("База отклонила изменение параметров договора.", e);
            }

            await transaction.CommitAsync(CancellationToken.None);
            return saved;
        });

        async Task<(Employee Actor, BeelineContract Contract)> CheckWriteAsync(
            DbConnection connection, DbTransaction? transaction, CancellationToken token)
        {
            var currentActor = await ReadActorAsync(connection, transaction, principal, expectedEmployee, token);
            var access = await ReadAccessAsync(connection, transaction, currentActor, token);
            if (!access.CanUpdate)
                throw new BeelineRejectedException("Нет разрешения на изменение параметров договора Билайн.");

            var current = await ReadContractAsync(connection, transaction, original.Number, token);
            if (current is null)
                throw new BeelineRejectedException("Договор удалён или недоступен. Ввод сохранён.");
            return (currentActor, current);
        }

        void EnsureBaseline(BeelineContract current)
        {
            if (current.TimeZone != original.TimeZone || current.EmailManager != original.EmailManager ||
                current.ChangedAt != original.ChangedAt || current.ChangedBy != original.ChangedBy)
                throw new BeelineRejectedException("Параметры договора изменились. Сверьте исходные данные.");
        }
    }

    private static async Task<Employee> ReadActorAsync(DbConnection connection, DbTransaction? transaction,
        string? principal, int expectedEmployee, CancellationToken ct)
    {
        var actor = await Employee.GetCurrentWithRolesAsync(connection, transaction, principal, ct);
        return actor is not null && actor.Id == expectedEmployee
            ? actor : throw new BeelineRejectedException("Не удалось подтвердить текущего сотрудника.");
    }

    private static async Task<(bool CanRead, bool CanUpdate)> ReadAccessAsync(DbConnection connection, DbTransaction? transaction,
        Employee actor, CancellationToken ct)
    {
        var administrator = actor.HasRole(Роли.Администратор_тарифов_связи, EmployeeRoleScope.Global);
        if (!administrator && !actor.HasRole(Роли.Наблюдатель_услуг_связи, EmployeeRoleScope.Global))
            return (false, false);
        var grants = await connection.QuerySingleAsync<Grants>(Command(SQLQueries.SELECT_БилайнПрава, null, transaction, ct));
        return (grants.CanRead, grants.CanRead && grants.CanUpdate && (administrator || grants.NamedWriter));
    }

    private static Task<BeelineContract?> ReadContractAsync(DbConnection connection, DbTransaction? transaction,
        string number, CancellationToken ct) =>
        connection.QuerySingleOrDefaultAsync<BeelineContract>(Command(string.Format(CultureInfo.InvariantCulture,
            SQLQueries.SELECT_БилайнКарточка, transaction is null ? "" : "WITH (UPDLOCK,HOLDLOCK)"),
            new { id = Key(number), beeline = (int)Лица.Билайн }, transaction, ct));

    private static DbString Key(string number) => new() { Value = number, IsAnsi = false, Length = 50 };

    private sealed record Grants(bool CanRead, bool CanUpdate, bool NamedWriter);

    private static CommandDefinition Command(string sql, object? parameters, DbTransaction? transaction, CancellationToken ct) =>
        new(sql, parameters, transaction, commandType: CommandType.Text, cancellationToken: ct);
}

