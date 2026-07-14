> Глобальные правила и обзор решения — в корневом /AGENTS.md. Здесь — только специфика проекта Clayzor.Lib.Entities.

## Entity CRUD & Lookup pattern
→ [docs/entity-crud.md](docs/entity-crud.md)

## Adding a new entity
→ [docs/adding-new-entity.md](docs/adding-new-entity.md)

## DynamicGrid — динамические SQL-помощники

Пакет `Clayzor.Lib.Entities.DynamicGrid` — доступ к БД для динамического режима ClayGrid.
Весь произвольный SQL выполняется **только здесь**; слой Controls вызывает эти методы, передавая инжектированный `DbManager Db`.

| Класс | Назначение |
|---|---|
| `DynamicSql` | Статические методы выполнения динамического SQL: `QueryRowsAsync` (SELECT → словари), `QueryPagedRowsAsync` (ROW_NUMBER, ≡ `Entity.GetPagedAsync`), `QueryCountAsync` (COUNT), `QueryPairsAsync` (пары для Тип 5), `QueryTriplesAsync` (тройки для Тип 9), `ExecuteAsync` (DELETE/INSERT/UPDATE, `CommandType.Text`) |
| `ClayGridSchemaMap` | Имена колонок трёх таблиц (Settings, Columns, UserParams). Русские дефолты, переопределяются в appsettings |
| `ClayGridDefinition` | Record: определение грида (GridId, Title, Sql, IdColumn, EditForm, NewForm, SqlDelete, …) |
| `ClayColumnDefinition` | Record: определение колонки (ColumnId, Column, Header, UrlKey, Order, Format, Type) |
| `ClayGridDefinitionData` | Класс данных: `LoadGridAsync`/`LoadColumnsAsync` (SQL через `DynamicSql`) + чистые функции `BuildGridSql`/`BuildColumnsSql`/`MapDefinition`/`MapColumn` (тестируются без БД) |
| `ClayGridUserParamsData` | Класс данных: `BuildParamName` (префикс+gridId), `BuildLoadSql`/`BuildInsertSql` (INSERT-only, upsert через триггер БД), `LoadAsync`/`SaveAsync` |

**Правила:**
- Никакого создания `DbManager` внутри — он передаётся параметром
- Пагинация — `ROW_NUMBER() OVER (ORDER BY ...)`, параметры `@__start`/`@__end` (SQL Server 2008 R2)
- Все значения — только через Dapper-параметры (`@param`), без конкатенации
