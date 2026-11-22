using System.Text.RegularExpressions;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Извлекает кандидатов оборудования из текста порта по алгоритму legacy NetForm.RenderLink.</summary>
public static class NetEquipmentLinkExtractor
{
    private static readonly Regex NameRegex = new(
        @"((?:(?!\d+\.|-)[*a-zA-Z0-9_\-]{1,50}(?<!-)\.?)+(?:[a-zA-Z0-9])?)",
        RegexOptions.Multiline,
        TimeSpan.FromMilliseconds(250));

    private static readonly Regex MacRegex = new(
        @"(\b([0-9A-Fa-f]{2}[:.-]?){5}[0-9A-Fa-f]{2}\b)",
        RegexOptions.Multiline,
        TimeSpan.FromMilliseconds(250));

    /// <summary>Возвращает токены в legacy-порядке: сначала имена, затем новые MAC, с учётом регистра при дедупликации.</summary>
    public static IReadOnlyList<string> Extract(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var tokens = new List<string>();
        foreach (Match match in NameRegex.Matches(text))
        {
            if (!match.Success)
                continue;
            var value = match.Groups[1].Value;
            if (value.StartsWith("ilo-", StringComparison.InvariantCulture))
                value = value.Replace("ilo-", "");
            if (!tokens.Contains(value))
                tokens.Add(value);
        }

        foreach (Match match in MacRegex.Matches(text))
        {
            if (!match.Success)
                continue;
            var value = match.Groups[1].Value;
            if (!tokens.Contains(value))
                tokens.Add(value);
        }
        return tokens;
    }
}
