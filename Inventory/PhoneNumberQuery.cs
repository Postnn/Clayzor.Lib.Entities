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
        sql.AppendLine();
        sql.Append("   AND T0.КодТелефонногоНомера NOT IN (").Append(string.Join(", ", names)).Append(')');
    }
}
