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

/// <summary>Исход записи требует сверки и блокирует повтор команды.</summary>
public sealed class BeelineUnknownException(Exception inner) : InvalidOperationException("Исход записи требует сверки.", inner);

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

    private const string UpdateKind = "beeline.update";

    /// <summary>Обновляет Time Zone и email после проверки прав и атомарно фиксирует итог в серверном журнале.</summary>
    public static async Task<BeelineContract?> SaveAsync(DbManager db, string? principal, int expectedEmployee,
        BeelineContract original, BeelineDraft draft, IReadOnlyCollection<string> configuredZones, Guid attemptId, CancellationToken ct)
    {
        Employee actor;
        BeelineContract expected;
        try
        {
            if (!BeelineRules.ValidNumber(original.Number)) throw new BeelineRejectedException("Некорректный номер договора.");
            var errors = BeelineRules.Validate(draft, configuredZones);
            if (errors.Count > 0) throw new BeelineRejectedException(string.Join(" ", errors));

            (actor, expected) = await db.RunDbAsync(connection => CheckWriteAsync(connection, null, ct));
        }
        catch (Exception e) when (e is not BeelineRejectedException)
        {
            throw new BeelineRejectedException("Проверка данных завершилась ошибкой. Запись не начата.", e);
        }

        var attempt = InventoryOperationJournal.ForEmployee(actor, attemptId, ConflictKey(expected.Number), UpdateKind);
        BeelineContract? saved = null;
        try
        {
            var previous = await InventoryOperationJournal.ReadAttemptAsync(db, attempt, ct);
            if (previous?.State == InventoryOperationState.Confirmed)
                return await ReadConfirmedAsync();
            if (previous?.State == InventoryOperationState.Rejected)
                throw new BeelineRejectedException("Эта попытка уже отклонена. Сверьте актуальные параметры.");
            if (previous is not null)
                throw new InventoryOperationPendingException();

            EnsureBaseline(expected);
            var receipt = await InventoryOperationJournal.RunSqlAsync<BeelineRejectedException>(db, attempt,
                async (connection, transaction, token) =>
                {
                    // Проверки и запись используют тот же SQL principal и ту же транзакцию.
                    BeelineContract current;
                    try
                    {
                        (_, current) = await CheckWriteAsync(connection, transaction, token);
                        EnsureBaseline(current);
                        token.ThrowIfCancellationRequested();
                    }
                    catch (Exception e) when (e is not BeelineRejectedException)
                    {
                        throw new BeelineRejectedException("Проверка данных завершилась ошибкой. Запись не начата.", e);
                    }
                    if (!string.Equals(current.Number, expected.Number, StringComparison.Ordinal))
                        throw new BeelineRejectedException("Номер договора изменился. Сверьте исходные данные.");

                    try
                    {
                        var update = Command(SQLQueries.UPDATE_БилайнПараметры,
                            new { id = Key(current.Number), timeZone = draft.TimeZone, email = draft.EmailManager }, transaction, token);
                        var affected = await connection.QuerySingleAsync<int>(update);
                        if (affected != 1) throw new BeelineRejectedException("База не подтвердила обновление одного договора.");

                        saved = await ReadContractAsync(connection, transaction, current.Number, token);
                        if (saved is null || saved.TimeZone != draft.TimeZone || saved.EmailManager != draft.EmailManager)
                            throw new BeelineRejectedException("База не сохранила введённые параметры. Для мобильного бюджета проверьте настройки основного договора.");
                    }
                    catch (Exception e) when (e is not (BeelineRejectedException or TimeoutException or OperationCanceledException) &&
                        (e is not Microsoft.Data.SqlClient.SqlException sql || sql.Number != -2 && !DbManager.IsConnectivityError(sql)) &&
                        transaction.Connection is not null)
                    {
                        throw new BeelineRejectedException("База отклонила изменение параметров договора.", e);
                    }

                    return null;
                }, ct);
            if (receipt.State == InventoryOperationState.Rejected)
                throw new BeelineRejectedException("Эта попытка уже отклонена. Сверьте актуальные параметры.");
            return saved ?? await ReadConfirmedAsync();
        }
        catch (Exception e) when (e is not (BeelineRejectedException or InventoryOperationPendingException or InventoryOperationReservationException))
        {
            throw new BeelineUnknownException(e);
        }

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

        async Task<BeelineContract?> ReadConfirmedAsync()
        {
            try
            {
                var (_, current) = await db.RunDbAsync(connection => CheckWriteAsync(connection, null, ct));
                return current;
            }
            catch (Exception e) when (e is InvalidOperationException or TimeoutException or OperationCanceledException
                or Microsoft.Data.SqlClient.SqlException)
            {
                // Повторное чтение не меняет уже доказанный серверный исход.
                return null;
            }
        }

        void EnsureBaseline(BeelineContract current)
        {
            if (current.TimeZone != original.TimeZone || current.EmailManager != original.EmailManager ||
                current.ChangedAt != original.ChangedAt || current.ChangedBy != original.ChangedBy)
                throw new BeelineRejectedException("Параметры договора изменились. Сверьте исходные данные.");
        }
    }

    /// <summary>Читает серверный исход по каноническому номеру после свежей проверки сотрудника и доступа к договору.</summary>
    public static async Task<(InventoryOperationReceipt? Receipt, bool HasPending)> ReadReceiptAsync(DbManager db, string? principal, int expectedEmployee,
        string number, CancellationToken ct)
    {
        var scope = await ReadAuthorizedScopeAsync(db, principal, expectedEmployee, number, ct);
        if (scope is not { } access)
            return (null, false);

        var receipt = await InventoryOperationJournal.ReadLatestAsync(db, access.Actor, access.ConflictKey, ct);
        return (receipt, await InventoryOperationJournal.IsScopePendingAsync(db, access.ConflictKey, ct));
    }

    /// <summary>Сверяет только указанную попытку изменения текущего договора после свежей проверки сотрудника и прав.</summary>
    public static async Task<InventoryOperationReceipt?> ReadVerifiedReceiptAsync(DbManager db, string? principal,
        int expectedEmployee, string number, Guid attemptId, CancellationToken ct)
    {
        if (attemptId == Guid.Empty)
            return null;

        var scope = await ReadAuthorizedScopeAsync(db, principal, expectedEmployee, number, ct);
        if (scope is not { } access)
            return null;

        var attempt = InventoryOperationJournal.ForEmployee(access.Actor, attemptId, access.ConflictKey, UpdateKind);
        return await InventoryOperationJournal.ReadAttemptAsync(db, attempt, ct);
    }

    /// <summary>Переносит только идентификаторы прежних попыток канонического договора для подтверждённого сотрудника.</summary>
    public static async Task ImportLegacyAsync(DbManager db, string? principal, int expectedEmployee, string number,
        IReadOnlyCollection<Guid> attemptIds, CancellationToken ct)
    {
        if (attemptIds.Count == 0)
            return;

        var scope = await ReadAuthorizedScopeAsync(db, principal, expectedEmployee, number, ct);
        if (scope is not { } access || access.Number != number)
            throw new BeelineRejectedException("Не удалось подтвердить договор для сверки прежней операции.");

        var attempts = attemptIds.Distinct().Select(attemptId =>
            InventoryOperationJournal.ForEmployee(access.Actor, attemptId, access.ConflictKey, UpdateKind)).ToArray();
        await InventoryOperationJournal.ImportLegacyAsync(db, attempts, ct);
    }

    private static async Task<(Employee Actor, string ConflictKey, string Number)?> ReadAuthorizedScopeAsync(DbManager db, string? principal,
        int expectedEmployee, string number, CancellationToken ct)
    {
        if (!BeelineRules.ValidNumber(number))
            return null;

        return await db.RunDbAsync<(Employee Actor, string ConflictKey, string Number)?>(async connection =>
        {
            var employee = await ReadActorAsync(connection, null, principal, expectedEmployee, ct);
            if (!(await ReadAccessAsync(connection, null, employee, ct)).CanRead)
                throw new UnauthorizedAccessException("Нет доступа к параметрам договора Билайн.");

            var contract = await ReadContractAsync(connection, null, number, ct);
            return contract is null ? null : (employee, ConflictKey(contract.Number), contract.Number);
        });
    }

    private static string ConflictKey(string canonicalNumber) => "beeline:contract:" + canonicalNumber;

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

