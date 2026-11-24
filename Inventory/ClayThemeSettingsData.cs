using Clayzor.Lib.DALC;
using Clayzor.Lib.Entities.DynamicGrid;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Общий параметр оформления в личном пространстве настроек Инвентаризации.</summary>
public static class ClayThemeSettingsData
{
    /// <summary>Имя параметра темы приложения Inventory.</summary>
    public const string InventoryParameter = "InvTheme";

    /// <summary>Имя параметра темы приложения Users.</summary>
    public const string UsersParameter = "UsersTheme";

    private const string View = "vwНастройки";
    private const int ApplicationIndependentClient = 0;
    private const int PersonalSetting = 0;
    private static readonly ClayGridSchemaMap Schema = new();

    /// <summary>Читает личную тему текущего SQL-пользователя через штатное представление настроек.</summary>
    /// <param name="db">Подключение к базе Инвентаризации.</param>
    /// <param name="parameter">Зарегистрированное имя параметра приложения.</param>
    /// <param name="cancellationToken">Токен отмены чтения.</param>
    public static async Task<string?> LoadAsync(DbManager db, string parameter, CancellationToken cancellationToken)
    {
        var sql = ClayGridUserParamsData.BuildLoadSql(View, Schema, 1);
        var rows = await db.QueryRequiredAsync<(string Name, string Value)>(sql,
            new { clid = ApplicationIndependentClient, shid = PersonalSetting, n0 = parameter }, cancellationToken);
        return rows.SingleOrDefault().Value;
    }

    /// <summary>Сохраняет тему штатным INSERT-upsert и подтверждает результат контрольным чтением.</summary>
    /// <param name="db">Подключение к базе Инвентаризации.</param>
    /// <param name="parameter">Зарегистрированное имя параметра приложения.</param>
    /// <param name="value">Сериализованное сочетание темы и режима.</param>
    /// <param name="cancellationToken">Токен отмены записи и контрольного чтения.</param>
    public static async Task SaveAsync(DbManager db, string parameter, string value, CancellationToken cancellationToken)
    {
        await ClayGridUserParamsData.SaveAsync(db, ApplicationIndependentClient, parameter, value,
            View, Schema, PersonalSetting, cancellationToken);

        // В том числе обнаруживает молчаливый отказ триггера для незарегистрированного параметра.
        if (!string.Equals(await LoadAsync(db, parameter, cancellationToken), value, StringComparison.Ordinal))
            throw new InvalidOperationException($"Триггер не подтвердил сохранение параметра {parameter}.");
    }
}
