# Clayzor.Lib.Entities

Библиотека объектов данных (доменный слой) решения **Clayzor**. Содержит базовый класс сущности `Entity` со стандартными CRUD-операциями и серверной пагинацией, справочный контракт `ILookupEntity`, именованные SQL-запросы (`SQLQueries`) и вспомогательные классы доступа к данным для динамического грида (`Clayzor.Lib.Entities.DynamicGrid`).

> Промежуточный слой решения: зависит от `Clayzor.Lib.DALC` и используется вышестоящими проектами (например `Clayzor.Lib.Web.Controls`). Отдельного NuGet-пакета и релизов нет — подключается как `ProjectReference`.

## Содержание

- [Что это](#что-это)
- [Место в решении](#место-в-решении)
- [Технологии и зависимости](#технологии-и-зависимости)
- [Структура](#структура)
- [Базовый класс Entity](#базовый-класс-entity)
- [Паттерн сущности (CRUD)](#паттерн-сущности-crud)
- [SQL-запросы и имена колонок](#sql-запросы-и-имена-колонок)
- [Справочные сущности (ILookupEntity)](#справочные-сущности-ilookupentity)
- [Пагинация для SQL Server 2008 R2](#пагинация-для-sql-server-2008-r2)
- [DynamicGrid — доступ к данным динамического грида](#dynamicgrid--доступ-к-данным-динамического-грида)
- [Документация](#документация)
- [Разработка](#разработка)
- [Лицензия](#лицензия)

## Что это

Проект описывает доменные объекты приложения и то, как они читаются и пишутся в БД. Здесь нет ORM и репозиториев: каждая сущность наследует абстрактный `Entity`, переопределяет свои SQL-константы (`SELECT`/`INSERT`/`UPDATE`/`DELETE`) и получает готовые методы CRUD, а также статические хелперы выборки с фильтром, сортировкой и постраничным чтением. Фактическое выполнение запросов делегируется `DbManager` из `Clayzor.Lib.DALC`.

## Место в решении

```
Clayzor.Lib.DALC          — доступ к данным (DbManager, ISqlErrorHandler)
        ▲
        │ ProjectReference
Clayzor.Lib.Entities      — этот проект: сущности, SQL-константы, Entity, DynamicGrid
        ▲
        │ ProjectReference
Clayzor.Lib.Web.Controls  — UI-компоненты (ClayGrid и др.)
```

## Технологии и зависимости

- **.NET 10** (`net10.0`), `Microsoft.NET.Sdk`, включены `ImplicitUsings` и `Nullable`.
- **Dapper** `2.*` — маппинг и параметры запросов.
- **ProjectReference:** `Clayzor.Lib.DALC` (менеджер подключения и выполнение SQL).

## Структура

```
Clayzor.Lib.Entities/
├─ Entity.cs             базовый класс сущности (CRUD + статические хелперы выборки)
├─ ILookupEntity.cs      контракт справочной сущности (Id + Name)
├─ SQLQueries.cs         именованные SQL-запросы (константы)
├─ MedicalTests/         доменные сущности предметной области (медицинские анализы)
├─ DynamicGrid/          доступ к данным для динамического режима ClayGrid
├─ docs/                 документация по паттернам сущностей
├─ AGENTS.md             правила проекта для разработчиков/агентов
└─ Clayzor.Lib.Entities.csproj
```

## Базовый класс Entity

`Entity` — абстрактный базовый класс. Производный класс задаёт первичный ключ и четыре SQL-константы, а взамен получает CRUD и статические методы выборки.

**Абстрактные члены, которые переопределяет сущность:**

| Член | Назначение |
| --- | --- |
| `int Id { get; set; }` | Первичный ключ. |
| `SelectSql` (protected) | SQL `SELECT` для выборки. |
| `InsertSql` (protected) | SQL `INSERT`. |
| `UpdateSql` (protected) | SQL `UPDATE`. |
| `DeleteSql` (protected) | SQL `DELETE` по `Id`. |

**Методы экземпляра (CRUD):**

| Метод | Действие |
| --- | --- |
| `InsertAsync(DbManager db)` | Выполняет `InsertSql` с `this` как параметрами (`CommandType.Text`). |
| `UpdateAsync(DbManager db)` | Выполняет `UpdateSql` с `this` (`CommandType.Text`). |
| `DeleteAsync(DbManager db)` | Выполняет `DeleteSql` с `new { Id }` (`CommandType.Text`). |

**Статические хелперы выборки** (`where T : Entity`):

| Метод | Назначение |
| --- | --- |
| `GetAllAsync<T>(db, selectSql, whereClause?, orderByClause?, param?)` | `SELECT` с динамическими `WHERE` и `ORDER BY`. |
| `GetAllSimpleAsync<T>(db, selectSql)` | Простой `SELECT` без динамических условий. |
| `GetPagedAsync<T>(db, selectSql, whereClause?, orderByClause?, param?, pageNumber, pageSize)` | Постраничная выборка через `ROW_NUMBER()`. |
| `GetCountAsync<T>(db, selectSql, whereClause?, param?)` | Общее число записей под фильтром. |

`GetPagedAsync` и `GetCountAsync` оборачивают `selectSql` в подзапрос (`_q`), поэтому `WHERE` оперирует **выходными именами колонок** запроса — алиасы таблиц не нужны, а плоский и группированный режимы грида работают единообразно.

## Паттерн сущности (CRUD)

Типичная сущность наследует `Entity`, отдаёт свои SQL-константы из `SQLQueries` и оборачивает статические хелперы в удобные статические методы:

```csharp
public class MyEntity : Entity
{
    public override int Id { get; set; }

    [Column("НазваниеАнализа")]
    public string Name { get; set; } = "";

    protected override string SelectSql => SQLQueries.SELECT_МоиЗаписи;
    protected override string InsertSql => SQLQueries.INSERT_МояТаблица;
    protected override string UpdateSql => SQLQueries.UPDATE_МояТаблица;
    protected override string DeleteSql => SQLQueries.DELETE_МояТаблица;

    public static Task<IEnumerable<MyEntity>> GetAllAsync(DbManager db) =>
        GetAllSimpleAsync<MyEntity>(db, SQLQueries.SELECT_МоиЗаписи);
}
```

Пошаговое руководство по добавлению новой сущности — в [`docs/adding-new-entity.md`](docs/adding-new-entity.md); общий паттерн CRUD и справочников — в [`docs/entity-crud.md`](docs/entity-crud.md).

## SQL-запросы и имена колонок

- Весь SQL хранится в `SQLQueries.cs` как именованные константы. Соглашение об именовании: `SELECT_{DataName}`, `INSERT_/UPDATE_/DELETE_{TableName}`, `SP_{Name}` (процедуры), `FN_{Name}` (функции).
- Имена колонок — **русские** (`КодМедицинскогоАнализа`, `НазваниеАнализа` и т. п.). Свойства сущностей маппятся на колонки атрибутом `[Column(...)]` со ссылкой на константы имён колонок (каждое имя определено ровно один раз).
- Каждый класс сущности регистрируется в `DapperColumnMapper.Initialize()` — так Dapper корректно сопоставляет русские колонки со свойствами.

## Справочные сущности (ILookupEntity)

`ILookupEntity` — минимальный контракт справочника: целочисленный `Id` и строковое `Name`. Такие сущности используются, например, в выпадающих списках `ClayComboBox` из слоя UI.

## Пагинация для SQL Server 2008 R2

Целевая СУБД — SQL Server 2008 R2, поэтому `OFFSET/FETCH` (2012+) не применяется. `GetPagedAsync` строит запрос на `ROW_NUMBER()`:

```sql
SELECT * FROM (
    SELECT _src.*, ROW_NUMBER() OVER (ORDER BY {orderBy}) AS _rn
    FROM ({selectSql}) _src
) _p WHERE _rn BETWEEN @__start AND @__end
```

Границы страницы передаются **параметрами** `@__start = (pageNumber - 1) * pageSize + 1` и `@__end = pageNumber * pageSize` — без подстановки значений в текст SQL. Если `ORDER BY` не задан, используется `(SELECT 0)`.

## DynamicGrid — доступ к данным динамического грида

Пакет `Clayzor.Lib.Entities.DynamicGrid` — единственное место, где выполняется произвольный (динамический) SQL для динамического режима `ClayGrid`. Слой UI (Controls) вызывает эти методы, передавая инжектированный `DbManager` параметром — свой экземпляр `DbManager` пакет не создаёт.

| Класс | Назначение |
| --- | --- |
| `DynamicSql` | Статические методы выполнения динамического SQL: `QueryRowsAsync` (SELECT → словари), `QueryPagedRowsAsync` (ROW_NUMBER, аналог `Entity.GetPagedAsync`), `QueryCountAsync`, `QueryPairsAsync` (пары значений), `QueryTriplesAsync` (тройки), `ExecuteAsync` (DELETE/INSERT/UPDATE, `CommandType.Text`). |
| `ClayGridSchemaMap` | Имена колонок трёх таблиц (Settings, Columns, UserParams). Русские значения по умолчанию, переопределяются в appsettings. |
| `ClayGridDefinition` | Record с определением грида (`GridId`, `Title`, `Sql`, `IdColumn`, `EditForm`, `NewForm`, `SqlDelete`, …). |
| `ClayColumnDefinition` | Record с определением колонки (`ColumnId`, `Column`, `Header`, `UrlKey`, `Order`, `Format`, `Type`). |
| `ClayGridDefinitionData` | Загрузка определения и колонок из БД (`LoadGridAsync`/`LoadColumnsAsync`) + чистые функции сборки SQL и маппинга (`BuildGridSql`/`BuildColumnsSql`/`MapDefinition`/`MapColumn`), тестируемые без БД. |
| `ClayGridUserParamsData` | Пользовательские параметры грида: `BuildParamName`, `BuildLoadSql`/`BuildInsertSql` (INSERT-only, upsert через триггер БД), `LoadAsync`/`SaveAsync`. |

**Правила пакета:**

- `DbManager` не создаётся внутри — только передаётся параметром.
- Пагинация — `ROW_NUMBER() OVER (ORDER BY ...)` с параметрами `@__start`/`@__end`.
- Все значения передаются только через Dapper-параметры (`@param`), без конкатенации в SQL.

## Документация

Каталог [`docs/`](docs):

- [`docs/entity-crud.md`](docs/entity-crud.md) — паттерн CRUD и справочников для сущностей.
- [`docs/adding-new-entity.md`](docs/adding-new-entity.md) — как добавить новую сущность.

## Разработка

`AGENTS.md` — ориентир для разработчиков и AI-агентов: он фиксирует паттерн сущностей, порядок добавления новых объектов данных и правила пакета `DynamicGrid`. Глобальные правила решения — в корневом `AGENTS.md` вышестоящего репозитория.

## Лицензия

В репозитории не найден файл лицензии. Если библиотека предназначена для внешнего использования, стоит добавить `LICENSE`; до этого права по умолчанию сохраняются за автором.
