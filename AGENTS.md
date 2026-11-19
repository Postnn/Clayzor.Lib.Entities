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
| `ClayGridSchemaMap` | Имена колонок трёх таблиц (Settings, Columns, UserParams). Columns.QuickSearch = `"УчаствуетВБыстромПоиске"` (опциональная колонка). Русские дефолты, переопределяются в appsettings |
| `ClayGridDefinition` | Record: определение грида (GridId, Title, Sql, IdColumn, EditForm, NewForm, SqlDelete, SupportsQuickSearch) |
| `ClayColumnDefinition` | Record: определение колонки (ColumnId, Column, Header, UrlKey, Order, Format, Type, QuickSearch). QuickSearch: 1→true, 0/NULL/нет колонки→false |
| `ClayGridDefinitionData` | Класс данных: `LoadGridWithQuickSearchAsync`, `LoadColumnsAsync` (SQL через `DynamicSql`), `CheckQuickSearchSupportAsync` (COL_LENGTH), `BuildColumnsSqlWithQuickSearch`, `MapColumn` (с флагом QuickSearch) + чистые функции (тестируются без БД) |
| `ClayGridUserParamsData` | Класс данных: `BuildParamName` (префикс+gridId, валидация ≤20 символов), `BuildLoadSql`/`BuildInsertSql` (INSERT-only, upsert через триггер БД, колонка `SharedId`), `LoadAsync`/`SaveAsync` (с параметром `sharedId`, по умолчанию 0 — личные настройки) |
| `ClayGridSharedParamsData` | Класс данных общих настроек (серия SH): `CreateAsync` (SCOPE_IDENTITY), `SaveParamsAsync` (цикл `SaveAsync`), `ListAsync` (DISTINCT JOIN, исключает служебный 0), `AnyAsync` (проверка наличия), `RenameAsync`, `DeleteAsync` (два шага: параметры→запись, без каскада), `LoadSharedParamsAsync` (через табличную функцию `UserParamsShared`), `CreateWithParamsAsync` (создание + параметры с компенсацией при ошибке) |

**Правила:**
- Никакого создания `DbManager` внутри — он передаётся параметром
- Пагинация — `ROW_NUMBER() OVER (ORDER BY ...)`, параметры `@__start`/`@__end` (SQL Server 2008 R2)
- Все значения — только через Dapper-параметры (`@param`), без конкатенации

## Enum фиксированных записей БД

- Фиксированный бизнес-ID строки справочника БД объявлять enum в `Clayzor.Lib.Entities`. **Имя enum строго равно имени таблицы**, имя элемента — реальному наименованию записи, значение — фактическому коду. XML-doc enum указывает таблицу и колонку-код, XML-doc элементов — исходное наименование/назначение.
- Не создавать enum по форме и не выдумывать соответствие. Сверять схему/данные БД или legacy; неподтверждённое заносить в долговой список. Переменные PK, арифметика, размеры и `0/1` булевых признаков — не enum.
- В сигнатурах использовать enum, приведение к `int` только на границе Dapper/SQL. SQL-литералы по возможности заменять параметрами, не конкатенировать пользовательский ввод.
- Проверять карту `таблица → ключевое поле → код → наименование → enum → использования`, включая 42/43/44/66/68/102/103, типы 27/30, расположение 55 и документ 2365 (в базе документов). Роль 42 называть только по `Роли`/`vwРоли`, не по догадке.

## Employee — единый авторизационный домен

- `Employee` — единственный владелец получения текущего сотрудника, effective roles, `HasRole`/`RequireRoleAsync`, global/local области лица, замещений, языка и fallback культуры. Не создавать конкурирующую сущность Employee.
- Идентичность получать только на сервере и сверять Windows/Negotiate principal с SQL principal. Service-account не считать текущим пользователем. При неизвестной/ошибочной идентичности — fail closed.
- Поддерживать `КодЛица=0` и `zeroContains` по legacy; не подменять глобальную роль ролью по одному лицу. Замещения проверять через подтверждённую SQL-логику с датами действия.
- Для UX использовать снимок прав, перед каждой записью/внешним действием — свежую проверку через `Employee`. При отзыве роли, ошибке SQL или неизвестных правах запись запрещена. Не принимать EmployeeId от клиента как доказательство.
- CRUD и read helpers остаются в Entities; Services не дублируют `HasRoleAsync`/`HasRoleForPersonAsync` и не содержат SQL. При записи связанных таблиц контекст и владелец загружаются из актуальной БД.
- Дополнительные нормы и порядок миграции — в корневом `/AGENTS.md` (`Границы Services`, `Enum для фиксированных кодов БД`, `Employee CurrentUser и авторизация`).
- Entity CRUD и добавление сущностей выполнять по [docs/entity-crud.md](docs/entity-crud.md) и [docs/adding-new-entity.md](docs/adding-new-entity.md).
