using System.Data;
using System.Data.Common;
using System.Globalization;
using Clayzor.Lib.DALC;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Чтение, календарь и транзакция изменения расписания дежурств.</summary>
public static class DutyData
{
    private const string WriteLock = "WITH (UPDLOCK, HOLDLOCK)";

    /// <summary>Получает серверный день и доступные операции.</summary>
    public static Task<DutyContext> ReadContextAsync(DbManager db, CancellationToken ct) =>
        db.RunDbAsync(connection => ReadContextAsync(connection, null, ct));

    /// <summary>Читает существующую карточку по дате.</summary>
    public static Task<DutyRow?> ReadAsync(DbManager db, DateOnly date, CancellationToken ct) =>
        db.RunDbAsync(connection => ReadRowAsync(connection, null, date, ct));

    /// <summary>Возвращает первый пробел в расписании, начиная с серверного дня.</summary>
    public static async Task<DateOnly> NextDayAsync(DbManager db, DateOnly today, CancellationToken ct)
    {
        var value = await db.RunDbAsync(connection => connection.QuerySingleAsync<DateTime>(
            Command(SQLQueries.SELECT_ДежурствоСвободныйДень, new { today = AsDate(today) }, null, ct)));
        return DateOnly.FromDateTime(value);
    }

    /// <summary>Загружает рабочий день для выбранного инженера по производственному календарю.</summary>
    public static Task<bool> IsWorkDayAsync(DbManager db, DateOnly date, int employeeId, CancellationToken ct) =>
        db.RunDbAsync(connection => IsWorkDayAsync(connection, null, date, employeeId, ct));

    /// <summary>Ищет доступных инженеров по имени или точному коду в календарном месяце.</summary>
    public static Task<IReadOnlyList<DutyEngineer>> FindEngineersAsync(DbManager db, DateOnly date, string? text,
        int? id, int limit, CancellationToken ct) =>
        db.RunDbAsync(async connection => (IReadOnlyList<DutyEngineer>)
            (await FindEngineersAsync(connection, null, date, text, id, limit, ct)).AsList());

    /// <summary>Пишет план либо факт под свежей идентичностью и актуальной версией. Отгул и расписание используют одну транзакцию.</summary>
    public static async Task<DutyRow> SaveAsync(DbManager db, string? principal, int expectedActor, DutyRow? original,
        DutyDraft draft, CancellationToken ct)
    {
        try
        {
            var actor = await Employee.GetCurrentAsync(db, principal, ct);
            if (actor?.Id != expectedActor)
                throw new DutyRejectedException("Не удалось подтвердить текущего сотрудника.");
            var validation = draft.Validate(false, null);
            if (validation is not null) throw new DutyRejectedException(validation);
            if (draft.Date == DateOnly.MinValue || draft.Date == DateOnly.MaxValue)
                throw new DutyRejectedException("Дата дежурства вне допустимого диапазона.");
        }
        catch (Exception e) when (e is not DutyRejectedException)
        {
            throw new DutyRejectedException("Проверка перед сохранением завершилась ошибкой. Запись не начата.", e);
        }

        return await db.RunDbAsync(async connection =>
        {
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var committing = false;
            try
            {
                var context = await ReadContextAsync(connection, transaction, ct);
                if (!(original is null ? context.CanInsert : context.CanUpdate))
                    throw new DutyRejectedException("Сохранение дежурства недоступно в текущем SQL-контексте.");
                var date = draft.Date!.Value;
                if (original is not null && DateOnly.FromDateTime(original.DutyDate) != date)
                    throw new DutyRejectedException("Дата существующего дежурства не изменяется.");

                var current = await ReadRowAsync(connection, transaction, date, ct);
                if (current != original)
                    throw new DutyRejectedException("Дежурство изменилось или дата уже занята. Перечитайте карточку.");
                if (current?.Closed != 0 && current is not null)
                    throw new DutyRejectedException("Закрытое дежурство нельзя изменить.");

                var fact = current is not null && date < context.BusinessDay;
                if (!fact && (draft.Confirmation is not null || draft.Close || draft.WantsTimeOff))
                    throw new DutyRejectedException("Подтверждение доступно только для прошедшего дежурства.");
                if (fact && current!.PlannedId != draft.PlannedId)
                    throw new DutyRejectedException("Плановый инженер прошедшего дежурства не изменяется.");
                if (!fact && (await FindEngineersAsync(connection, transaction, date, null, draft.PlannedId, 1, ct)).SingleOrDefault() is null)
                    throw new DutyRejectedException("Дежурный инженер больше недоступен.");

                DutyRow saved;
                if (fact)
                    saved = await SaveFactAsync(connection, transaction, context, current!, draft, date, ct);
                else
                {
                    await connection.ExecuteAsync(Command(original is null ? SQLQueries.INSERT_Дежурство : SQLQueries.UPDATE_ДежурствоПлан,
                        new { date = AsDate(date), engineerId = draft.PlannedId }, transaction, ct));
                    saved = await RequireRowAsync(connection, transaction, date, ct);
                    if (saved.PlannedId != draft.PlannedId || saved.Closed != 0)
                        throw new DutyRejectedException("База не подтвердила сохранённый план.");
                }

                committing = true;
                await transaction.CommitAsync(CancellationToken.None);
                return saved;
            }
            catch (Exception e) when (!committing && e is not SqlException { Number: -2 } &&
                (e is not SqlException sql || !DbManager.IsConnectivityError(sql)))
            {
                // Если rollback невозможно подтвердить, исход остаётся неопределённым.
                if (transaction.Connection is not null)
                    await transaction.RollbackAsync(CancellationToken.None);
                else if (e is SqlException { Number: not (3609 or 50000) })
                    throw;
                throw e is DutyRejectedException rejected ? rejected :
                    new DutyRejectedException("Операция отклонена. Изменения расписания и отгула отменены.", e);
            }
        });
    }

    private static async Task<DutyRow> SaveFactAsync(DbConnection connection, DbTransaction transaction,
        DutyContext context, DutyRow original, DutyDraft draft, DateOnly date, CancellationToken ct)
    {
        var actual = draft.EffectiveEngineer(true);
        var validation = draft.Validate(true, null);
        if (validation is not null) throw new DutyRejectedException(validation);
        if (actual is { } engineer
            && (await FindEngineersAsync(connection, transaction, date, null, engineer, 1, ct)).SingleOrDefault() is null)
            throw new DutyRejectedException("Фактический дежурный инженер больше недоступен.");

        bool? workDay = actual is { } id ? await IsWorkDayAsync(connection, transaction, date, id, ct) : null;
        validation = draft.Validate(true, workDay);
        if (validation is not null) throw new DutyRejectedException(validation);
        var timeOff = draft.EffectiveTimeOff(workDay);
        if (timeOff == DateOnly.MaxValue)
            throw new DutyRejectedException("Для этой даты отгула невозможно вычислить следующий день.");
        if ((original.TimeOff.HasValue || draft.Close && timeOff.HasValue) && !context.CanChangeTimeOff)
            throw new DutyRejectedException("Контракт записи отгула в ЗаявкиIT недоступен.");

        if (original.TimeOff is { } previous)
        {
            // Прежний интервал принадлежит прежнему сотруднику, даже при смене A → B.
            var oldDay = DateOnly.FromDateTime(previous);
            var oldEmployee = original.ActualId ?? original.PlannedId;
            var intervals = await FindIntervalsAsync(connection, transaction, oldEmployee, oldDay, ct);
            if (intervals.Count > 0)
            {
                if (intervals.Count != 1 || await HasOtherDutyAsync(connection, transaction, date, oldEmployee, oldDay, ct))
                    throw new DutyRejectedException("Прежний отгул нельзя однозначно связать с этим дежурством.");
                await connection.ExecuteAsync(Command(SQLQueries.DELETE_ДежурствоОтгул,
                    new { intervalId = intervals[0] }, transaction, ct));
                if ((await FindIntervalsAsync(connection, transaction, oldEmployee, oldDay, ct)).Count != 0)
                    throw new DutyRejectedException("База не подтвердила удаление прежнего отгула.");
            }
        }

        await connection.ExecuteAsync(Command(SQLQueries.UPDATE_ДежурствоФакт,
            new { date = AsDate(date), actualId = actual, timeOff = timeOff.HasValue ? AsDate(timeOff.Value) : (DateTime?)null,
                close = (byte)(draft.Close ? 1 : 0) }, transaction, ct));
        var saved = await RequireRowAsync(connection, transaction, date, ct);
        if (saved.Closed != (draft.Close ? 1 : 0) || saved.ActualId != actual ||
            saved.TimeOff != (timeOff is { } requested ? AsDate(requested) : null))
            throw new DutyRejectedException("База не подтвердила факт дежурства или дату отгула.");

        if (saved.Closed == 1 && saved.TimeOff is { } next && saved.ActualId is { } employeeId)
        {
            var nextDay = DateOnly.FromDateTime(next);
            if ((await FindIntervalsAsync(connection, transaction, employeeId, nextDay, ct)).Count != 0 ||
                await HasOtherDutyAsync(connection, transaction, date, employeeId, nextDay, ct))
                throw new DutyRejectedException("На эту дату уже зарегистрирован отгул за дежурство.");
            await connection.ExecuteAsync(Command(SQLQueries.INSERT_ДежурствоОтгул,
                new { employeeId, timeOff = next }, transaction, ct));
            if ((await FindIntervalsAsync(connection, transaction, employeeId, nextDay, ct)).Count != 1)
                throw new DutyRejectedException("База ЗаявкиIT не подтвердила запись отгула.");
        }

        return saved;
    }

    private static Task<DutyContext> ReadContextAsync(DbConnection connection, DbTransaction? transaction, CancellationToken ct) =>
        connection.QuerySingleAsync<DutyContext>(Command(SQLQueries.SELECT_ДежурствоКонтекст, null, transaction, ct));

    private static Task<DutyRow?> ReadRowAsync(DbConnection connection, DbTransaction? transaction, DateOnly date, CancellationToken ct) =>
        connection.QuerySingleOrDefaultAsync<DutyRow>(Command(
            string.Format(CultureInfo.InvariantCulture, SQLQueries.SELECT_ДежурствоКарточка, transaction is null ? "" : WriteLock),
            new { date = AsDate(date) }, transaction, ct));

    private static async Task<DutyRow> RequireRowAsync(DbConnection connection, DbTransaction transaction, DateOnly date, CancellationToken ct) =>
        await ReadRowAsync(connection, transaction, date, ct) ?? throw new DutyRejectedException("Сохранённую строку дежурства не удалось прочитать.");

    private static Task<bool> IsWorkDayAsync(DbConnection connection, DbTransaction? transaction, DateOnly date,
        int employeeId, CancellationToken ct) =>
        connection.QuerySingleAsync<bool>(Command(SQLQueries.SELECT_ДежурствоРабочийДень,
            new { date = AsDate(date), employeeId, fallbackTerritory = (int)Территории.Российская_Федерация }, transaction, ct));

    private static Task<IEnumerable<DutyEngineer>> FindEngineersAsync(DbConnection connection, DbTransaction? transaction,
        DateOnly date, string? search, int? id, int limit, CancellationToken ct)
    {
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var escaped = search?.Replace("~", "~~").Replace("%", "~%").Replace("_", "~_").Replace("[", "~[");
        return connection.QueryAsync<DutyEngineer>(Command(SQLQueries.SELECT_ДежурствоИнженеры,
            new { month = new DateTime(date.Year, date.Month, 1), id, limit = Math.Clamp(limit, 1, 101),
                search = escaped is null ? null : $"%{escaped}%",
                code = int.TryParse(search, out var numeric) ? numeric : (int?)null,
                engineerRole = (int)Роли.Дежурный_инженер }, transaction, ct));
    }

    private static async Task<IReadOnlyList<int>> FindIntervalsAsync(DbConnection connection, DbTransaction transaction,
        int employeeId, DateOnly timeOff, CancellationToken ct) =>
        (await connection.QueryAsync<int>(Command(SQLQueries.SELECT_ДежурствоИнтервалыОтгула,
            new { employeeId, timeOff = AsDate(timeOff) }, transaction, ct))).AsList();

    private static Task<bool> HasOtherDutyAsync(DbConnection connection, DbTransaction transaction, DateOnly date,
        int employeeId, DateOnly timeOff, CancellationToken ct) =>
        connection.QuerySingleAsync<bool>(Command(SQLQueries.SELECT_ДежурствоДругойВладелецОтгула,
            new { employeeId, timeOff = AsDate(timeOff), date = AsDate(date) }, transaction, ct));

    private static DateTime AsDate(DateOnly value) => value.ToDateTime(TimeOnly.MinValue);

    private static CommandDefinition Command(string text, object? args, DbTransaction? transaction, CancellationToken ct) =>
        new(text, args, transaction, commandType: CommandType.Text, cancellationToken: ct);
}

