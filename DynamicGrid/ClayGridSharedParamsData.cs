using System.Data;
using Dapper;
using Clayzor.Lib.DALC;

namespace Clayzor.Lib.Entities.DynamicGrid;

/// <summary>
/// Класс данных для работы с общими настройками динамического грида
/// (серия SH — «Поделиться»). Все операции выполняются в слое Entities;
/// Controls не создаёт DbManager и не выполняет SQL напрямую.
/// </summary>
public static class ClayGridSharedParamsData
{
    // ── 1. Создание общей настройки ──────────────────────────────────────────

    /// <summary>
    /// Строит SQL для создания общей настройки. Чистая функция, без БД.
    /// </summary>
    /// <param name="sharedTable">Имя таблицы общих настроек (из конфигурации).</param>
    public static string BuildCreateSql(string sharedTable) =>
        $"-- Создание общей настройки. SCOPE_IDENTITY() возвращает идентификатор,\n" +
        $"-- выданный в текущей области видимости, — безопаснее @@IDENTITY при наличии триггеров.\n" +
        $"INSERT INTO [{sharedTable}] ([Название]) VALUES (@title);\n" +
        $"SELECT CAST(SCOPE_IDENTITY() AS int);";

    /// <summary>
    /// Создаёт общую настройку и возвращает новый <c>КодНастройкиОбщей</c>.
    /// Название обрезается (<see cref="string.Trim"/>), проверяется на непустоту
    /// и длину ≤ 100 символов.
    /// </summary>
    /// <param name="db">Менеджер подключения к БД.</param>
    /// <param name="title">Название ссылки (показывается пользователю).</param>
    /// <param name="sharedTable">Имя таблицы общих настроек (из конфигурации).</param>
    /// <returns>Новый идентификатор общей настройки (≥ 1).</returns>
    public static async Task<int> CreateAsync(
        DbManager db, string title, string sharedTable)
    {
        var trimmed = title?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException("Название общей настройки не может быть пустым.");
        if (trimmed.Length > 100)
            throw new ArgumentException($"Название общей настройки длиннее 100 символов: {trimmed.Length}.");

        var sql = BuildCreateSql(sharedTable);
        var result = await DynamicSql.QueryRowsAsync(db, sql, new { title = trimmed });
        if (result.Count == 0)
            throw new InvalidOperationException("Не удалось создать общую настройку: SCOPE_IDENTITY() не вернул результат.");
        return Convert.ToInt32(result[0].Values.First());
    }

    // ── 2. Сохранение набора параметров ──────────────────────────────────────

    /// <summary>
    /// Сохраняет набор параметров под общей настройкой.
    /// Каждый параметр пишется отдельным INSERT; upsert делает триггер БД.
    /// <see cref="ClayGridUserParamsData.SaveAsync"/> переиспользуется —
    /// новый механизм записи не изобретается.
    /// </summary>
    /// <param name="db">Менеджер подключения к БД.</param>
    /// <param name="sharedId">Идентификатор общей настройки (≥ 1).</param>
    /// <param name="params">Словарь Параметр → Значение.</param>
    /// <param name="userParamsTable">Имя таблицы пользовательских параметров.</param>
    /// <param name="s">Маппинг имён колонок.</param>
    public static async Task SaveParamsAsync(
        DbManager db, int sharedId, IReadOnlyDictionary<string, string> @params,
        string userParamsTable, ClayGridSchemaMap s)
    {
        foreach (var (name, value) in @params)
        {
            // clientId = 0 для общих настроек: владелец выводится из первой
            // строки параметров (КодНастройкиКлиента), а не дублируется в каждой.
            await ClayGridUserParamsData.SaveAsync(
                db, clientId: 0, name, value, userParamsTable, s, sharedId: sharedId);
        }
    }

    // ── 3. Список общих настроек текущего грида ─────────────────────────────

    /// <summary>
    /// Строит SQL для получения списка общих настроек. Чистая функция.
    /// DISTINCT обязателен: одна настройка → много строк параметров.
    /// КодНастройкиОбщей &lt;&gt; 0: служебная запись исключена из списка.
    /// </summary>
    public static string BuildListSql(
        string sharedTable, string userParamsTable, ClayGridSchemaMap s, int nameCount)
    {
        var sc = s.UserParams;
        var inParams = string.Join(", ", Enumerable.Range(0, nameCount).Select(i => $"@n{i}"));
        return $"-- Список общих настроек текущего грида. DISTINCT — одна настройка → много параметров.\n" +
               $"-- КодНастройкиОбщей &lt;&gt; 0 — служебная запись исключена.\n" +
               $"SELECT DISTINCT sh.[КодНастройкиОбщей], sh.[Название]\n" +
               $"FROM [{sharedTable}] sh\n" +
               $"INNER JOIN [{userParamsTable}] p\n" +
               $"    ON p.[{sc.SharedId}] = sh.[КодНастройкиОбщей]\n" +
               $"WHERE p.[{sc.ClientId}] = @clid\n" +
               $"  AND p.[{sc.SharedId}] &lt;&gt; 0\n" +
               $"  AND p.[{sc.Name}] IN ({inParams})\n" +
               $"ORDER BY sh.[Название]";
    }

    /// <summary>
    /// Возвращает список общих настроек текущего грида:
    /// пары (КодНастройкиОбщей, Название), отсортированные по названию.
    /// Служебная запись 0 в результат не попадает.
    /// </summary>
    public static async Task<IReadOnlyList<(int SharedId, string Title)>> ListAsync(
        DbManager db, int clientId, IReadOnlyList<string> paramNames,
        string userParamsTable, string sharedTable, ClayGridSchemaMap s)
    {
        if (paramNames.Count == 0)
            return [];

        var sql = BuildListSql(sharedTable, userParamsTable, s, paramNames.Count);
        var dp  = new DynamicParameters();
        dp.Add("clid", clientId);
        for (int i = 0; i < paramNames.Count; i++)
            dp.Add($"n{i}", paramNames[i]);

        var rows = await DynamicSql.QueryRowsAsync(db, sql, dp);
        return rows
            .Select(r => (
                SharedId: Convert.ToInt32(r["КодНастройкиОбщей"]),
                Title:    r["Название"]?.ToString() ?? ""))
            .ToList();
    }

    // ── 4. Проверка наличия общих настроек ──────────────────────────────────

    /// <summary>
    /// Строит SQL для проверки наличия общих настроек. Чистая функция.
    /// </summary>
    public static string BuildAnySql(
        string userParamsTable, ClayGridSchemaMap s, int nameCount)
    {
        var sc = s.UserParams;
        var inParams = string.Join(", ", Enumerable.Range(0, nameCount).Select(i => $"@n{i}"));
        return $"-- Проверка наличия общих настроек у текущего грида.\n" +
               $"SELECT TOP 1 1\n" +
               $"FROM [{userParamsTable}]\n" +
               $"WHERE [{sc.ClientId}] = @clid\n" +
               $"  AND [{sc.SharedId}] &lt;&gt; 0\n" +
               $"  AND [{sc.Name}] IN ({inParams})";
    }

    /// <summary>
    /// Проверяет, есть ли у текущего грида хотя бы одна общая настройка.
    /// Используется для определения видимости кнопки списка.
    /// </summary>
    public static async Task<bool> AnyAsync(
        DbManager db, int clientId, IReadOnlyList<string> paramNames,
        string userParamsTable, ClayGridSchemaMap s)
    {
        if (paramNames.Count == 0)
            return false;

        var sql = BuildAnySql(userParamsTable, s, paramNames.Count);
        var dp  = new DynamicParameters();
        dp.Add("clid", clientId);
        for (int i = 0; i < paramNames.Count; i++)
            dp.Add($"n{i}", paramNames[i]);

        var rows = await DynamicSql.QueryRowsAsync(db, sql, dp);
        return rows.Count > 0;
    }

    // ── 5. Переименование общей настройки ───────────────────────────────────

    /// <summary>
    /// Строит SQL для переименования общей настройки. Чистая функция.
    /// </summary>
    public static string BuildRenameSql(string sharedTable) =>
        $"-- Переименование общей настройки.\n" +
        $"UPDATE [{sharedTable}]\n" +
        $"   SET [Название] = @title\n" +
        $" WHERE [КодНастройкиОбщей] = @shid\n" +
        $"   AND @shid &lt;&gt; 0";

    /// <summary>
    /// Переименовывает общую настройку.
    /// Защита: <paramref name="sharedId"/> ≤ 0 → <see cref="InvalidOperationException"/>.
    /// </summary>
    public static async Task RenameAsync(
        DbManager db, int sharedId, string newTitle, string sharedTable)
    {
        if (sharedId <= 0)
            throw new InvalidOperationException(
                $"Попытка переименовать служебную запись: КодНастройкиОбщей = {sharedId}.");

        var trimmed = newTitle?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException("Название общей настройки не может быть пустым.");
        if (trimmed.Length > 100)
            throw new ArgumentException($"Название общей настройки длиннее 100 символов: {trimmed.Length}.");

        var sql = BuildRenameSql(sharedTable);
        await DynamicSql.ExecuteAsync(db, sql, new { shid = sharedId, title = trimmed });
    }

    // ── 6. Удаление общей настройки ─────────────────────────────────────────

    /// <summary>
    /// Строит SQL для удаления параметров общей настройки (шаг 1 из 2). Чистая функция.
    /// </summary>
    public static string BuildDeleteParamsSql(string userParamsTable, ClayGridSchemaMap s) =>
        $"-- Удаление параметров общей настройки (шаг 1 из 2).\n" +
        $"DELETE FROM [{userParamsTable}]\n" +
        $" WHERE [{s.UserParams.SharedId}] = @shid\n" +
        $"   AND @shid &lt;&gt; 0";

    /// <summary>
    /// Строит SQL для удаления записи общей настройки (шаг 2 из 2). Чистая функция.
    /// </summary>
    public static string BuildDeleteSharedSql(string sharedTable) =>
        $"-- Удаление записи общей настройки (шаг 2 из 2).\n" +
        $"DELETE FROM [{sharedTable}]\n" +
        $" WHERE [КодНастройкиОбщей] = @shid\n" +
        $"   AND @shid &lt;&gt; 0";

    /// <summary>
    /// Удаляет общую настройку: сначала дочерние параметры, затем родительскую запись.
    /// Порядок важен: внешний ключ не даст удалить родителя при наличии дочерних строк.
    /// Каскада в схеме нет намеренно (<c>SH2</c>, п. 3).
    /// Защита: <paramref name="sharedId"/> ≤ 0 → <see cref="InvalidOperationException"/>.
    /// </summary>
    public static async Task DeleteAsync(
        DbManager db, int sharedId,
        string userParamsTable, string sharedTable, ClayGridSchemaMap s)
    {
        if (sharedId <= 0)
            throw new InvalidOperationException(
                $"Попытка удалить служебную запись: КодНастройкиОбщей = {sharedId}.");

        // Шаг 1: удаляем параметры
        var sqlParams = BuildDeleteParamsSql(userParamsTable, s);
        await DynamicSql.ExecuteAsync(db, sqlParams, new { shid = sharedId });

        // Шаг 2: удаляем саму запись
        var sqlShared = BuildDeleteSharedSql(sharedTable);
        await DynamicSql.ExecuteAsync(db, sqlShared, new { shid = sharedId });
    }

    /// <summary>
    /// Удаляет ТОЛЬКО запись общей настройки (без параметров).
    /// Используется как компенсация при ошибке сохранения параметров.
    /// </summary>
    private static async Task DeleteSharedOnlyAsync(
        DbManager db, int sharedId, string sharedTable)
    {
        if (sharedId <= 0) return;
        var sql = BuildDeleteSharedSql(sharedTable);
        await DynamicSql.ExecuteAsync(db, sql, new { shid = sharedId });
    }

    // ── 7. Чтение параметров по sharedId ────────────────────────────────────

    /// <summary>
    /// Строит SQL для чтения параметров общей настройки через табличную функцию.
    /// Контракт функции задан заказчиком:
    /// входной параметр @КодНастройкиОбщей int,
    /// возвращаемые поля Параметр varchar(50), Значение nvarchar(MAX).
    /// <para>
    /// ВАЖНО: форма вызова зависит от фактического типа объекта в целевой БД.
    /// Приведённый вариант — для табличной функции (SELECT ... FROM func(@p)).
    /// Если в целевой БД это хранимая процедура — вызов будет через EXEC.
    /// </para>
    /// </summary>
    public static string BuildLoadSharedSql(string functionName) =>
        $"-- Чтение параметров общей настройки через табличную функцию.\n" +
        $"-- Фильтр по КодНастройкиКлиента отсутствует намеренно:\n" +
        $"-- ссылку открывает другой пользователь.\n" +
        $"SELECT [Параметр], [Значение]\n" +
        $"  FROM [{functionName}](@shid)";

    /// <summary>
    /// Читает параметры общей настройки через объект БД, имя которого
    /// задано в конфигурации (<c>UserParamsShared</c>).
    /// Возвращает словарь Параметр → Значение (только имена из <paramref name="paramNames"/>).
    /// Пустой результат — штатная ситуация (настройка не найдена), не ошибка.
    /// Защита: <paramref name="sharedId"/> ≤ 0 → <see cref="InvalidOperationException"/>.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string>> LoadSharedParamsAsync(
        DbManager db, int sharedId, string functionName,
        IReadOnlyList<string> paramNames)
    {
        if (sharedId <= 0)
            throw new InvalidOperationException(
                $"Попытка загрузить общие параметры с недопустимым идентификатором: {sharedId}.");

        var sql = BuildLoadSharedSql(functionName);
        var rows = await DynamicSql.QueryRowsAsync(db, sql, new { shid = sharedId });

        var result = new Dictionary<string, string>();
        var knownNames = new HashSet<string>(paramNames, StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var name  = row["Параметр"]?.ToString() ?? "";
            var value = row["Значение"]?.ToString() ?? "";
            // Только параметры текущего грида; чужие имена игнорируются
            if (knownNames.Contains(name))
                result[name] = value;
        }
        return result;
    }

    // ── Композитный метод: создать + сохранить параметры ────────────────────

    /// <summary>
    /// Создаёт общую настройку и сохраняет набор параметров одним вызовом.
    /// При ошибке сохранения параметров — компенсация: удаляет только что
    /// созданную родительскую запись, чтобы не оставить «сироту» без параметров.
    /// <para>
    /// Транзакции не используются: <see cref="DbManager"/> их не поддерживает
    /// (<c>SH1</c>, вопрос 6). Добавлять транзакционную инфраструктуру в DALC
    /// ради одной задачи нельзя (Simplicity First).
    /// </para>
    /// </summary>
    /// <returns>Новый идентификатор общей настройки.</returns>
    public static async Task<int> CreateWithParamsAsync(
        DbManager db, string title,
        IReadOnlyDictionary<string, string> @params,
        string sharedTable, string userParamsTable, ClayGridSchemaMap s)
    {
        var sharedId = await CreateAsync(db, title, sharedTable);
        try
        {
            await SaveParamsAsync(db, sharedId, @params, userParamsTable, s);
        }
        catch
        {
            // Компенсация: удалить «сиротскую» запись без параметров.
            // Ошибка补偿ции подавляется — исходная ошибка важнее.
            try { await DeleteSharedOnlyAsync(db, sharedId, sharedTable); }
            catch { /* подавлена — исходная ошибка важнее */ }
            throw;
        }
        return sharedId;
    }
}
