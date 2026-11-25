using System.Globalization;
using System.Text.Json;

namespace Clayzor.Lib.Entities.TerritorialEntity;

/// <summary>Один путь и один выбранный узел из штатной настройки InvTerTreeState.</summary>
public sealed record TerritoryViewState(int? Anchor, int? Selected, bool PhoneCodes)
{
    public static TerritoryViewState? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("core", out var core) ||
                core.ValueKind != JsonValueKind.Object) return null;
            return new(ReadId(core, "open", last: true), ReadId(core, "selected", last: false),
                root.TryGetProperty("phoneCodes", out var codes) && codes.ValueKind == JsonValueKind.True);
        }
        catch (JsonException) { return null; }
    }

    public string Serialize() => JsonSerializer.Serialize(new
    {
        core = new { open = Keys(Anchor), selected = Keys(Selected) },
        phoneCodes = PhoneCodes,
    });

    private static string[] Keys(int? id) => id is > 0 ? [id.Value.ToString(CultureInfo.InvariantCulture)] : [];

    private static int? ReadId(JsonElement core, string key, bool last)
    {
        if (!core.TryGetProperty(key, out var array) || array.ValueKind != JsonValueKind.Array) return null;
        int? found = null;
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind is not (JsonValueKind.String or JsonValueKind.Number) ||
                !int.TryParse(element.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0) continue;
            found = id;
            if (!last) break;
        }
        return found;
    }
}
