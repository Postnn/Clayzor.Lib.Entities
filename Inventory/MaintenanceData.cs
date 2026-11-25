using System.Data;
using System.Globalization;
using Clayzor.Lib.DALC;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Строка представления регламентных работ.</summary>
public sealed record MaintenanceWork
{
    /// <summary>Ключ работы.</summary>
    public int Id { get; init; }
    /// <summary>Срок в UTC после преобразования из локального времени SQL Server.</summary>
    public DateTime? Deadline { get; init; }
    /// <summary>Название регламентной работы.</summary>
    public string Name { get; init; } = "";
    /// <summary>Назначенный ответственный.</summary>
    public string Responsible { get; init; } = "";
    /// <summary>Адрес инструкции.</summary>
    public string Instruction { get; init; } = "";
    /// <summary>Последнее выполнение в UTC после преобразования из локального времени SQL Server.</summary>
    public DateTime? LastCompleted { get; init; }
    /// <summary>Имя сотрудника, выполнившего работу.</summary>
    public string? EmployeeName { get; init; }
    /// <summary>Ключ сотрудника, выполнившего работу.</summary>
    public int? EmployeeId { get; init; }

    /// <summary>Видимый признак утверждения работы из контракта триггера.</summary>
    public bool IsApproved => !Name.StartsWith('*');
}

/// <summary>Список работ и время SQL Server, полученные в одном вызове.</summary>
public sealed record MaintenanceList(DateTime ServerNow, IReadOnlyList<MaintenanceWork> Rows);

/// <summary>Подтверждённый отказ до регистрации выполнения.</summary>
public sealed class MaintenanceRejectedException(string message, Exception? inner = null) : InvalidOperationException(message, inner);

/// <summary>Читает работы и регистрирует выполнение под текущим сотрудником.</summary>
public static class MaintenanceData
{
    /// <summary>Имена шести колонок, разрешённых для построения параметризованного фильтра.</summary>
    public static IReadOnlyList<string> FilterColumns { get; } = Array.AsReadOnly(new[]
    {
        MaintenanceColumns.Deadline, MaintenanceColumns.Name, MaintenanceColumns.Responsible,
        MaintenanceColumns.Instruction, MaintenanceColumns.LastCompleted, MaintenanceColumns.EmployeeName,
    });

    /// <summary>Читает работы с фильтром и порядком из доверенного SQL-билдера грида.</summary>
    public static Task<MaintenanceList> ReadAsync(DbManager db, CancellationToken ct = default,
        string? gridPredicate = null, DynamicParameters? parameters = null, string? gridOrderBy = null) =>
        db.RunDbAsync(async connection =>
        {
            using var result = await connection.QueryMultipleAsync(
                new CommandDefinition(string.Format(CultureInfo.InvariantCulture, SQLQueries.SELECT_РегламентныеРаботы,
                    gridPredicate ?? "1=1", gridOrderBy ?? "ВыполнитьДо ASC, КодРегламентныхРабот ASC"), parameters, commandType: CommandType.Text, cancellationToken: ct));
            var now = await result.ReadSingleAsync<DateTime>();
            return new MaintenanceList(now, (await result.ReadAsync<MaintenanceWork>()).AsList());
        });

    /// <summary>Читает значения разрешённой колонки, включая пустые значения, с запасом для признака лимита.</summary>
    public static async Task<IReadOnlyList<object?>> ReadFilterValuesAsync(DbManager db, string column, int limit, CancellationToken ct)
    {
        if (!FilterColumns.Contains(column, StringComparer.Ordinal))
            throw new ArgumentException("Фильтрация по этой колонке недоступна.", nameof(column));
        var sql = string.Format(CultureInfo.InvariantCulture, SQLQueries.SELECT_РегламентныеРаботыЗначенияФильтра, $"[{column}]");
        return await db.RunDbAsync<IReadOnlyList<object?>>(async connection =>
        {
            using var reader = await connection.ExecuteReaderAsync(new CommandDefinition(sql,
                new { sampleLimit = Math.Clamp(limit, 1, 100) + 2 }, commandType: CommandType.Text, cancellationToken: ct));
            var values = new List<object?>();
            while (await reader.ReadAsync(ct))
                values.Add(reader.IsDBNull(0) ? null : reader.GetValue(0));
            return values;
        });
    }

    /// <summary>Сопоставляет роли триггера с разрешением INSERT текущего SQL-контекста.</summary>
    public static Task<bool> CanCompleteAsync(DbManager db, Employee actor, CancellationToken ct = default) =>
        actor.HasRole(Роли.Дежурный_инженер) ||
        actor.HasRole(Роли.Исполнитель_заявок_службы_поддержки_пользователей) ||
        actor.HasRole(Роли.Администратор_тарифов_связи)
            ? ReadFlagAsync(db, SQLQueries.SELECT_РегламентныеРаботыРазрешение, null, ct)
            : Task.FromResult(false);

    /// <summary>Повторно проверяет сотрудника, разрешение и работу, затем передаёт БД только ключ.</summary>
    public static async Task CompleteAsync(DbManager db, string? principal, int? expectedEmployeeId, int id, CancellationToken ct = default)
    {
        try
        {
            if (id <= 0) throw new MaintenanceRejectedException("Некорректный код регламентной работы.");
            var actor = await Employee.GetCurrentWithRolesAsync(db, principal, ct);
            if (actor is null || actor.Id != expectedEmployeeId || !await CanCompleteAsync(db, actor, ct))
                throw new MaintenanceRejectedException("У вас нет права подтверждать выполнение.");
            if (!await ReadFlagAsync(db, SQLQueries.SELECT_РегламентнаяРаботаДоступна, new { id }, ct))
                throw new MaintenanceRejectedException("Работа удалена, недоступна или ещё не утверждена.");
            ct.ThrowIfCancellationRequested();
        }
        catch (Exception e) when (e is not MaintenanceRejectedException)
        {
            throw new MaintenanceRejectedException("Проверка прав или работы завершилась ошибкой. Команда не отправлена.", e);
        }

        try
        {
            await db.RunDbAsync(connection => connection.ExecuteAsync(
                new CommandDefinition(SQLQueries.INSERT_РегламентныеРаботыВыполнение, new { id },
                    commandType: CommandType.Text, cancellationToken: ct)));
        }
        catch (SqlException e) when (e.Number is 229 or 547 || e.Number == 50000 && e.Class == 12)
        {
            // Эти отказы прав, FK и проверок штатного триггера прерывают INSERT.
            throw new MaintenanceRejectedException("База данных отклонила подтверждение выполнения.", e);
        }
    }

    private static Task<bool> ReadFlagAsync(DbManager db, string query, object? parameters, CancellationToken ct) =>
        db.RunDbAsync(connection => connection.QuerySingleAsync<bool>(
            new CommandDefinition(query, parameters, commandType: CommandType.Text, cancellationToken: ct)));
}

