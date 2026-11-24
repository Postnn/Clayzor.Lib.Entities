using Clayzor.Lib.DALC;
using Clayzor.Lib.Entities.DynamicGrid;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Общий параметр оформления в личном пространстве настроек Инвентаризации.</summary>
public static class ClayThemeSettingsData
{
    /// <summary>Единый параметр темы в личных настройках сотрудника.</summary>
    public const string Parameter = "ClayTheme";

    private const string View = "vwНастройки";
    private const int ApplicationIndependentClient = 0;
    private const int PersonalSetting = 0;
    private static readonly ClayGridSchemaMap Schema = new();
    private static readonly string LoadSql = QualifyView(ClayGridUserParamsData.BuildLoadSql(View, Schema, 1));
    private static readonly string InsertSql = QualifyView(ClayGridUserParamsData.BuildInsertSql(View, Schema));

    /// <summary>Читает личную тему текущего SQL-пользователя через штатное представление настроек.</summary>
    /// <param name="db">Подключение к базе Инвентаризации.</param>
    /// <param name="cancellationToken">Токен отмены чтения.</param>
    public static async Task<string?> LoadAsync(DbManager db, CancellationToken cancellationToken)
    {
        var rows = await db.QueryRequiredAsync<(string Name, string Value)>(LoadSql,
            new { clid = ApplicationIndependentClient, shid = PersonalSetting, n0 = Parameter }, cancellationToken);
        return rows.SingleOrDefault().Value;
    }

    /// <summary>Сохраняет тему штатным INSERT-upsert и подтверждает результат контрольным чтением.</summary>
    /// <param name="db">Подключение к базе Инвентаризации.</param>
    /// <param name="value">Сериализованное сочетание темы и режима.</param>
    /// <param name="cancellationToken">Токен отмены записи и контрольного чтения.</param>
    public static async Task SaveAsync(DbManager db, string value, CancellationToken cancellationToken)
    {
        await DynamicSql.ExecuteAsync(db, InsertSql,
            new { clid = ApplicationIndependentClient, shid = PersonalSetting, name = Parameter, value }, cancellationToken);

        // В том числе обнаруживает молчаливый отказ триггера для незарегистрированного параметра.
        if (!string.Equals(await LoadAsync(db, cancellationToken), value, StringComparison.Ordinal))
            throw new InvalidOperationException($"Триггер не подтвердил сохранение параметра {Parameter}.");
    }

    private static string QualifyView(string sql) => sql.Replace($"[{View}]", $"[dbo].[{View}]", StringComparison.Ordinal);
}
