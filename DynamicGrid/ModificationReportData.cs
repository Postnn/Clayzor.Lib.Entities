using System.Data;
using System.Globalization;
using Clayzor.Lib.DALC;
using Dapper;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Clayzor.Lib.Entities.DynamicGrid;

/// <summary>Серверное определение раздела отчёта.</summary>
public sealed record ModificationReportDefinition(int Id, string Name, string? Sql, string? FormReference);

/// <summary>Страница строк со схемой исходного SELECT.</summary>
public sealed record ModificationReportPage(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows, long Total)
{
    /// <summary>Разрешённые имена сотрудников по их кодам.</summary>
    public IReadOnlyDictionary<int, string> Authors { get; init; } = new Dictionary<int, string>();
}

/// <summary>Чтение разделов отчёта из авторизованного SQL-каталога Inventory.</summary>
public static class ModificationReportData
{
    /// <summary>Доступные определения, отсортированные по названию.</summary>
    public static Task<IEnumerable<ModificationReportDefinition>> ListAsync(DbManager db, CancellationToken ct = default) =>
        db.QueryRequiredAsync<ModificationReportDefinition>(SQLQueries.SELECT_ModificationReports, cancellationToken: ct);

    /// <summary>Выполняет один разрешённый раздел, сохраняя схему пустого результата.</summary>
    public static async Task<ModificationReportPage> ReadAsync(DbManager db, ModificationReportDefinition definition,
        DateTime from, DateTime to, int? employeeId, int page, int size, CancellationToken ct)
    {
        if (from.Date > to.Date || page < 1 || size is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(page));
        var sql = ValidateSelect(definition.Sql);
        var schemaSql = string.Format(CultureInfo.InvariantCulture, SQLQueries.SELECT_ModificationReportSchema, sql);
        var filtered = string.Format(CultureInfo.InvariantCulture, SQLQueries.SELECT_ModificationReportFiltered, sql);
        return await db.RunDbAsync(async connection =>
        {
            List<string> fields = [];
            await using (var reader = await connection.ExecuteReaderAsync(new CommandDefinition(schemaSql, cancellationToken: ct)))
                for (var i = 0; i < reader.FieldCount; i++) fields.Add(reader.GetName(i));
            if (!fields.Contains("Изменено", StringComparer.OrdinalIgnoreCase) || !fields.Contains("Изменил", StringComparer.OrdinalIgnoreCase) ||
                fields.Contains("__clay_report_row", StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException("В SQL раздела отсутствуют колонки Изменено или Изменил либо занято служебное имя.");

            var key = "[" + fields[0].Replace("]", "]]", StringComparison.Ordinal) + "]";
            var pageSql = string.Format(CultureInfo.InvariantCulture, SQLQueries.SELECT_ModificationReportPage, filtered, key);
            var parameters = new DynamicParameters();
            parameters.Add("DateFrom", from.Date, DbType.Date);
            parameters.Add("DateTo", to.Date, DbType.Date);
            parameters.Add("EmployeeId", employeeId, DbType.Int32);
            parameters.Add("FirstRow", (page - 1L) * size + 1);
            parameters.Add("LastRow", (long)page * size);

            using var result = await connection.QueryMultipleAsync(new CommandDefinition(pageSql, parameters, cancellationToken: ct));
            var total = await result.ReadSingleAsync<long>();
            var rows = (await result.ReadAsync()).Select(row => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(
                (IDictionary<string, object?>)row, StringComparer.OrdinalIgnoreCase)).ToList();
            var output = new ModificationReportPage(fields, rows, total);
            if (!fields.Skip(1).Any(IsAuthorColumn)) return output;
            var ids = rows.Select(AuthorId).OfType<int>().Distinct().ToArray();
            if (ids.Length == 0) return output;
            var authors = await connection.QueryAsync<AuthorName>(new CommandDefinition(
                SQLQueries.SELECT_ModificationAuthors, new { Ids = ids }, cancellationToken: ct));
            return output with { Authors = authors.Where(a => !string.IsNullOrWhiteSpace(a.Name)).ToDictionary(a => a.Id, a => a.Name) };
        });
    }

    /// <summary>Возвращает ФИО из доверенного справочника либо исходную подпись или fallback.</summary>
    public static string? CellText(ModificationReportPage result, IReadOnlyDictionary<string, object?> row, string name)
    {
        var text = Convert.ToString(row[name], CultureInfo.CurrentCulture);
        if (result.Columns[0] == name || !ModificationReportData.IsAuthorColumn(name)) return text;
        var id = ModificationReportData.AuthorId(row);
        if (id.HasValue && result.Authors.TryGetValue(id.Value, out var fio)) return fio;
        return !string.IsNullOrWhiteSpace(text) ? text : id.HasValue ? $"#{id}" : "Автор не указан";
    }

    /// <summary>Определяет исходные названия колонок автора.</summary>
    public static bool IsAuthorColumn(string name) => name is "ФИО" or "ИзменилФИО" or "Изменил ФИО";

    /// <summary>Читает положительный код сотрудника только из скрытой исходной колонки.</summary>
    public static int? AuthorId(IReadOnlyDictionary<string, object?> row) =>
        row.TryGetValue("Изменил", out var raw) && int.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture),
            NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;

    private sealed record AuthorName(int Id, string Name);

    internal static string ValidateSelect(string? source)
    {
        var sql = source?.Trim().TrimEnd(';').TrimEnd();
        if (string.IsNullOrWhiteSpace(sql)) throw new InvalidDataException("Не указан SQL раздела.");
        var wrapped = string.Format(CultureInfo.InvariantCulture, SQLQueries.SELECT_ModificationReportSchema, sql);
        using var text = new StringReader(wrapped);
        var parsed = new TSql100Parser(initialQuotedIdentifiers: true).Parse(text, out var errors);
        if (errors.Count > 0 || parsed is not TSqlScript { Batches: [ { Statements: [SelectStatement] } ] })
            throw new InvalidDataException("SQL раздела должен быть одним SELECT, допустимым в подзапросе SQL Server 2008 R2.");
        var guard = new ReportSelectGuard();
        parsed.Accept(guard);
        if (guard.Rejected) throw new InvalidDataException("SQL раздела содержит недопустимую конструкцию.");
        return sql;
    }

    private sealed class ReportSelectGuard : TSqlFragmentVisitor
    {
        public bool Rejected { get; private set; }
        public override void Visit(SelectStatement node) => Rejected |= node.Into is not null;
        public override void Visit(SelectSetVariable node) => Rejected = true;
        public override void Visit(OpenRowsetTableReference node) => Rejected = true;
        public override void Visit(BulkOpenRowset node) => Rejected = true;
        public override void Visit(OpenQueryTableReference node) => Rejected = true;
        public override void Visit(AdHocTableReference node) => Rejected = true;
        public override void Visit(SchemaObjectName node) => Rejected |= node.ServerIdentifier is not null;
    }
}
