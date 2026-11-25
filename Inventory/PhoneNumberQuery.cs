using System.Text;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Безопасное построение дополнительных условий выбора телефонных номеров.</summary>
public static class PhoneNumberQuery
{
    /// <summary>Добавляет параметризованное исключение уже выбранных телефонных номеров.</summary>
    public static void AddExcludedIds(
        StringBuilder sql,
        DynamicParameters parameters,
        IEnumerable<int> excludedIds)
    {
        var ids = excludedIds.Distinct().ToList();
        if (ids.Count == 0)
            return;
        var names = new List<string>(ids.Count);
        for (var index = 0; index < ids.Count; index++)
        {
            var name = $"@ExcludedPhoneId{index}";
            parameters.Add(name, ids[index]);
            names.Add(name);
        }
        AddPredicate(sql, $"AND T0.КодТелефонногоНомера NOT IN ({string.Join(", ", names)})");
    }

    internal static void AddSelectedId(StringBuilder sql) => AddPredicate(sql, "AND T0.КодТелефонногоНомера = @SelectedPhoneId");

    internal static void AddPredicate(StringBuilder sql, string predicate)
    {
        var orderByIndex = sql.ToString().LastIndexOf("ORDER BY", StringComparison.OrdinalIgnoreCase);
        if (orderByIndex >= 0)
        {
            sql.Insert(orderByIndex, predicate + Environment.NewLine);
            return;
        }
        sql.AppendLine();
        sql.Append(predicate);
    }
}
