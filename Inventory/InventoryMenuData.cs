using Clayzor.Lib.DALC;
using Clayzor.Lib.Entities.DynamicGrid;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Read-only доступ к данным меню Inventory через существующий DbManager.</summary>
public static class InventoryMenuData
{
    /// <summary>Загружает пункты типов 1–3 и текущий запрос без чтения SQL-текста запросов.</summary>
    public static async Task<InventoryMenuLoadResult> LoadAsync(
        DbManager db, int? currentQueryId, CancellationToken cancellationToken = default)
    {
        var rows = await DynamicSql.QueryRowsAsync(
            db, SQLQueries.SELECT_ЗапросыДляМенюИнвентаризации, null, cancellationToken);
        var items = rows.Select(Map).ToList();
        var current = items.FirstOrDefault(item => item.Id == currentQueryId);

        if (current is null && currentQueryId is > 0)
        {
            var currentRows = await DynamicSql.QueryRowsAsync(
                db, SQLQueries.SELECT_ЗапросДляМенюИнвентаризации,
                new { Id = currentQueryId.Value }, cancellationToken);
            current = currentRows.Select(Map).FirstOrDefault();
        }

        return new InventoryMenuLoadResult { Items = items, CurrentQuery = current };
    }

    /// <summary>Преобразует строку результата в типизированную модель.</summary>
    internal static InventoryMenuQuery Map(IReadOnlyDictionary<string, object?> row) => new()
    {
        Id = Convert.ToInt32(row["КодЗапроса"]),
        Caption = Text(row, "Запрос"),
        QueryType = Convert.ToInt32(row["ТипЗапроса"]),
        ListForm = Text(row, "ФормаСписка"),
        Icon = Text(row, "Пиктограмма"),
        ReportMenuItem = Text(row, "ПунктМенюОтчета"),
        ReportForm = Text(row, "ФормаОтчета"),
    };

    private static string Text(IReadOnlyDictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var value) && value is not null and not DBNull
            ? value.ToString() ?? string.Empty
            : string.Empty;
}
