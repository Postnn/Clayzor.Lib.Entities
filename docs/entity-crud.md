# Entity CRUD

Все CRUD-операции — методы класса сущности, не инлайн-SQL в страницах.

## Базовый класс `Entity`

`Clayzor.Lib.Entities/Entity.cs`:
- `abstract int Id` — первичный ключ
- `abstract string SelectSql / InsertSql / UpdateSql / DeleteSql` — SQL-константы
- `InsertAsync(DbManager db)` / `UpdateAsync(DbManager db)` / `DeleteAsync(DbManager db)` — наследуемые CRUD-методы
- `GetAllAsync<T>(db, sql, where, orderBy, param)` — SELECT с динамическими WHERE/ORDER BY
- `GetPagedAsync<T>(db, sql, where, orderBy, param, pageNumber, pageSize)` — SELECT с `ROW_NUMBER()` (SQL Server 2008 R2 совместимый)
- `GetCountAsync<T>(db, sql, where, param)` — `SELECT COUNT(*) FROM (SELECT ...)`
- `GetAllSimpleAsync<T>(db, sql)` — простой SELECT без динамических частей

### SQL Server 2008 R2 пагинация

`GetPagedAsync` использует `ROW_NUMBER()` вместо `OFFSET/FETCH`:

```sql
SELECT * FROM (
    SELECT _src.*, ROW_NUMBER() OVER (ORDER BY {orderBy}) AS _rn
    FROM ({selectSql}) _src
) _p WHERE _rn BETWEEN @__start AND @__end
```

Параметры: `@__start = (pageNumber - 1) * pageSize + 1`, `@__end = pageNumber * pageSize`.

## CRUD-сущность

```csharp
[Table("МояТаблица")]
public class MyEntity : Entity
{
    [Key]
    [Column(MedA.КодЗаписи)]
    public override int Id { get; set; }

    [Column(MedA.Название)]
    public string Name { get; set; } = string.Empty;

    protected override string SelectSql => SQLQueries.SELECT_МояТаблица;
    protected override string InsertSql => SQLQueries.INSERT_МояТаблица;
    protected override string UpdateSql => SQLQueries.UPDATE_МояТаблица;
    protected override string DeleteSql => SQLQueries.DELETE_МояТаблица;

    public static async Task<IEnumerable<MyEntity>> GetAllAsync(
        DbManager db, string? where, string? orderBy, object? param)
        => await Entity.GetAllAsync<MyEntity>(db, SQLQueries.SELECT_МояТаблица, where, orderBy, param);

    public static async Task<IEnumerable<MyEntity>> GetPagedAsync(
        DbManager db, string? where, string? orderBy, object? param,
        int pageNumber, int pageSize)
        => await Entity.GetPagedAsync<MyEntity>(db, SQLQueries.SELECT_МояТаблица, where, orderBy, param, pageNumber, pageSize);

    public static async Task<int> GetCountAsync(
        DbManager db, string? where = null, object? param = null)
        => await Entity.GetCountAsync<MyEntity>(db, SQLQueries.SELECT_МояТаблица, where, param);
}
```

## Справочная сущность (Lookup)

Реализует `ILookupEntity`, не наследует `Entity` (нет CRUD).

```csharp
public class MyLookup : ILookupEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public static async Task<List<MyLookup>> GetAllAsync(DbManager db)
    {
        var result = await db.QueryAsync<MyLookup>(SQLQueries.SELECT_MyLookup);
        return result.ToList();
    }
}
```

## Серверная группировка — модель данных

`Clayzor.Lib.Web.Controls/Components/Grid/ClayGridRow.cs` содержит типы для плоской модели с группами:

```csharp
public interface IClayGridRow { }

public class GroupHeaderRow : IClayGridRow
{
    public string DisplayValue { get; set; }
    public string FullKey { get; set; }      // ключи через \u001F
    public int ItemCount { get; set; }
    public int Depth { get; set; }           // 0 = внешний уровень
    public bool IsExpanded { get; set; }
    public List<string> GroupKeys { get; set; }
}

public class DetailRow<T> : IClayGridRow where T : Entity
{
    public T Item { get; set; }
    public string GroupKey { get; set; }
    public int Depth { get; set; }           // 0 = самый глубокий листовой уровень
}

public class GroupedPage<T> where T : Entity
{
    public List<IClayGridRow> Rows { get; set; }
    public int TotalEffectiveRows { get; set; }
}
```

## DapperColumnMapper

Регистрирует маппинг свойств .NET на русские колонки БД.
Находится в `Clayzor.Lib.Entities/MedicalTests/MedicalTest.cs`.

```csharp
public static class DapperColumnMapper
{
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        RegisterColumnMap<MedicalTest>();
        RegisterColumnMap<MedicalTestType>();
        _initialized = true;
    }

    private static void RegisterColumnMap<T>()
    {
        SqlMapper.SetTypeMap(typeof(T), new CustomPropertyTypeMap(typeof(T),
            (type, columnName) =>
            {
                return type.GetProperties().FirstOrDefault(p =>
                {
                    var attr = p.GetCustomAttributes(false)
                        .OfType<ColumnAttribute>().FirstOrDefault();
                    if (attr is not null && string.Equals(attr.Name, columnName, StringComparison.OrdinalIgnoreCase))
                        return true;
                    return string.Equals(p.Name, columnName, StringComparison.OrdinalIgnoreCase);
                })!;
            }
        ));
    }
}
```

### Правила маппинга
1. Сначала ищет совпадение по `[Column]`-атрибуту
2. Если не найдено — fallback на имя свойства

Это позволяет SQL-алиасы (`SELECT КодТипа AS Id`) работать даже при `[Column("КодТипа")]` на свойстве `Id`.
