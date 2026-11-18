using System.Globalization;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Разбор и сериализация компонентов конфигурации компьютера.</summary>
public static class EquipmentComputerDetail
{
    /// <summary>Разбирает значения, разделённые плюсом, и проверяет неотрицательность.</summary>
    public static IReadOnlyList<decimal> Parse(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
            return [];
        return detail.Split('+').Select(value =>
        {
            var number = decimal.Parse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture);
            if (number < 0)
                throw new FormatException("Компоненты конфигурации не могут быть отрицательными.");
            return number;
        }).ToList();
    }

    /// <summary>Удаляет нулевые хвосты и объединяет компоненты плюсом.</summary>
    public static string Serialize(IEnumerable<decimal> values, bool allowDecimals)
    {
        var components = values.ToList();
        if (components.Any(value => value < 0 || !allowDecimals && value != decimal.Truncate(value)))
            throw new FormatException("RAM и SSD задаются неотрицательными целыми числами.");
        while (components.Count > 0 && components[^1] == 0)
            components.RemoveAt(components.Count - 1);
        return string.Join('+', components.Select(value => value.ToString(
            allowDecimals ? "0.###" : "0", CultureInfo.InvariantCulture)));
    }

    /// <summary>Возвращает сумму компонентов.</summary>
    public static decimal Sum(IEnumerable<decimal> values) => values.Sum();
}
