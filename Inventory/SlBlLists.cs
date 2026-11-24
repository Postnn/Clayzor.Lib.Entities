using System.Data;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Вид почтового списка. Значения соответствуют Block штатного представления.</summary>
public enum SlBlKind : byte
{
    /// <summary>Безопасные адреса.</summary>
    Safe = 0,
    /// <summary>Заблокированные адреса.</summary>
    Blocked = 1,
}

/// <summary>Почтовый ящик и необязательный вид списка из URL.</summary>
public sealed record SlBlKey(string Address, SlBlKind? Kind)
{
    /// <summary>Разбирает однократно декодированный query, включая старый разделитель U+001F.</summary>
    public static bool TryParse(string? value, out SlBlKey? key)
    {
        key = null;
        if (string.IsNullOrEmpty(value)) return true;
        var parts = value.Split(':', '\u001f');
        if (parts.Length > 2 || string.IsNullOrWhiteSpace(parts[0]) || parts[0].Length > 260 || parts[0].Any(char.IsControl))
            return false;
        var kind = parts.Length == 1 ? null : parts[1] switch
        {
            "0" => (SlBlKind?)SlBlKind.Safe,
            "1" => SlBlKind.Blocked,
            _ => null,
        };
        if (parts.Length == 2 && kind is null) return false;
        key = new(parts[0], kind);
        return true;
    }
}

/// <summary>Оба фактических списка одного ящика. Отсутствующий результат выборки представлен отдельно.</summary>
public sealed record SlBlLists
{
    /// <summary>Безопасные адреса в формате хранилища.</summary>
    public string? SafeList { get; init; }
    /// <summary>Заблокированные адреса в формате хранилища.</summary>
    public string? BlockedList { get; init; }

    /// <summary>Действующий legacy default при наличии строки.</summary>
    public SlBlKind? InitialKind => string.IsNullOrEmpty(SafeList) ? SlBlKind.Blocked
        : string.IsNullOrEmpty(BlockedList) ? SlBlKind.Safe : null;

    /// <summary>Текст одного списка без нормализации.</summary>
    public string GetText(SlBlKind kind) => (kind == SlBlKind.Safe ? SafeList : BlockedList) ?? "";
}

/// <summary>Две разности желаемого и фактического состояния.</summary>
public sealed record SlBlDifferences
{
    /// <summary>Фактические записи, которых нет в черновике.</summary>
    public string NotDeleted { get; init; } = "";
    /// <summary>Записи черновика, которых нет в фактическом списке.</summary>
    public string NotSaved { get; init; } = "";
}

/// <summary>Чтение и валидация почтовых списков.</summary>
public static class SlBlData
{
    /// <summary>Ограничение редактора legacy.</summary>
    public const int MaxLength = 7950;

    /// <summary>Проверяет ввод без усечения или замены символов. Пустой текст означает очистку.</summary>
    public static string? Validate(string text)
    {
        if (text.Length > MaxLength) return $"Максимальная длина списка — {MaxLength} символов.";
        if (text.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or '.' or '@' or ',' or ';' or ' ' or '\t' or '\r' or '\n' or '\u00a0')))
            return "Список содержит недопустимые символы. Используйте адреса, домены или имена серверов.";
        return null;
    }

    /// <summary>Читает списки ящика. Ошибка SQL не превращается в отсутствие записи.</summary>
    public static async Task<SlBlLists?> ReadAsync(DbManager db, string address, CancellationToken ct = default) =>
        (await db.QueryRequiredAsync<SlBlLists>(SQLQueries.SELECT_SlBlLists, new { Address = AddressParameter(address) }, ct)).SingleOrDefault();

    /// <summary>Читает точный адрес из справочника.</summary>
    public static async Task<string?> FindAddressAsync(DbManager db, string address, CancellationToken ct = default) =>
        (await db.QueryRequiredAsync<string>(SQLQueries.SELECT_SlBlAddressById, new { Address = AddressParameter(address) }, ct)).SingleOrDefault();

    /// <summary>Выполняет поиск по адресу, сохраняя исходный строковый ключ.</summary>
    public static Task<IEnumerable<string>> SearchAddressesAsync(DbManager db, string text, int limit, CancellationToken ct = default)
    {
        var escaped = text.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
        return db.QueryRequiredAsync<string>(SQLQueries.SELECT_SlBlAddresses,
            new { Limit = limit, Pattern = new DbString { Value = $"%{escaped}%", IsAnsi = true } }, ct);
    }

    /// <summary>Сравнивает один список с черновиком штатным parser. Ожидается ровно одна строка.</summary>
    public static async Task<SlBlDifferences> CompareAsync(DbManager db, SlBlKey key, string text, CancellationToken ct = default)
    {
        if (key.Kind is not { } kind) throw new ArgumentException("Выберите вид списка.", nameof(key));
        var parameters = new DynamicParameters();
        parameters.Add("Address", key.Address, DbType.AnsiString, size: 260);
        parameters.Add("Block", (byte)kind, DbType.Byte);
        parameters.Add("Text", text, DbType.String, size: -1);
        return (await db.QueryRequiredAsync<SlBlDifferences>(SQLQueries.SELECT_SlBlDifferences, parameters, ct)).Single();
    }

    private static DbString AddressParameter(string address) => new() { Value = address, IsAnsi = true, Length = 260 };
}
