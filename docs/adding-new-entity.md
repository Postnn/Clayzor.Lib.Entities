# Добавление новой сущности

## Порядок действий

### 1. Константы колонок

Добавить имена колонок в `ColumnNames.cs` (каждое имя ровно один раз):

```csharp
public static class MedA
{
    // существующие...
    public const string КодНовойЗаписи = "КодНовойЗаписи";
    public const string НазваниеНовойЗаписи = "НазваниеНовойЗаписи";
}
```

### 2. SQL-константы

Добавить в `Clayzor.Lib.Entities/SQLQueries.cs`:

```csharp
/// <summary>Выборка новых записей.</summary>
public const string SELECT_НоваяТаблица = @"
-- Основные поля
SELECT КодНовойЗаписи,    -- идентификатор
       НазваниеНовойЗаписи -- название
FROM НоваяТаблица";

/// <summary>Добавление новой записи.</summary>
public const string INSERT_НоваяТаблица = @"
INSERT INTO НоваяТаблица (НазваниеНовойЗаписи)
VALUES (@Name)";

/// <summary>Обновление новой записи.</summary>
public const string UPDATE_НоваяТаблица = @"
UPDATE НоваяТаблица SET НазваниеНовойЗаписи = @Name
WHERE КодНовойЗаписи = @Id";

/// <summary>Удаление новой записи.</summary>
public const string DELETE_НоваяТаблица = @"
DELETE FROM НоваяТаблица WHERE КодНовойЗаписи = @Id";
```

### 3. Класс сущности

Создать в `Clayzor.Lib.Entities/`:

```csharp
[Table("НоваяТаблица")]
public class NewEntity : Entity
{
    [Key]
    [Column(MedA.КодНовойЗаписи)]
    public override int Id { get; set; }

    [Column(MedA.НазваниеНовойЗаписи)]
    public string Name { get; set; } = string.Empty;

    protected override string SelectSql => SQLQueries.SELECT_НоваяТаблица;
    protected override string InsertSql => SQLQueries.INSERT_НоваяТаблица;
    protected override string UpdateSql => SQLQueries.UPDATE_НоваяТаблица;
    protected override string DeleteSql => SQLQueries.DELETE_НоваяТаблица;

    public static async Task<IEnumerable<NewEntity>> GetAllAsync(
        DbManager db, string? where, string? orderBy, object? param)
        => await Entity.GetAllAsync<NewEntity>(db, SQLQueries.SELECT_НоваяТаблица, where, orderBy, param);

    public static async Task<IEnumerable<NewEntity>> GetPagedAsync(
        DbManager db, string? where, string? orderBy, object? param,
        int pageNumber, int pageSize)
        => await Entity.GetPagedAsync<NewEntity>(db, SQLQueries.SELECT_НоваяТаблица, where, orderBy, param, pageNumber, pageSize);

    public static async Task<int> GetCountAsync(
        DbManager db, string? where = null, object? param = null)
        => await Entity.GetCountAsync<NewEntity>(db, SQLQueries.SELECT_НоваяТаблица, where, param);
}
```

### 4. Регистрация в DapperColumnMapper

В `Clayzor.Lib.Entities/MedicalTests/MedicalTest.cs` (файл с `DapperColumnMapper`):

```csharp
public static void Initialize()
{
    if (_initialized) return;
    RegisterColumnMap<MedicalTest>();
    RegisterColumnMap<MedicalTestType>();
    RegisterColumnMap<NewEntity>();  // ← добавить
    _initialized = true;
}
```

### 5. Диалог редактирования

```razor
@* NewEntityEditDialog.razor *@
<ClayEditForm TEntity="NewEntity" Model="Model" OnSave="SaveAsync" OnDelete="DeleteAsync">
    <MudTextField @bind-Value="Model.Name" Label="Название" Variant="Variant.Outlined" Required="true" />
</ClayEditForm>

@code {
    [Parameter] public NewEntity Model { get; set; } = null!;

    private async Task SaveAsync(NewEntity model)
    {
        if (model.Id == 0)
            await model.InsertAsync(Db);
        else
            await model.UpdateAsync(Db);
    }

    private async Task DeleteAsync(NewEntity model)
    {
        var confirmed = await DialogService.ShowExAsync<ConfirmDialog>(
            "Подтверждение",
            new DialogParameters<ConfirmDialog> { { x => x.Message, "Удалить запись?" } },
            new DialogOptionsEx { DragMode = MudDialogDragMode.Simple });
        var result = await confirmed.Result;
        if (result is not null && !result.Canceled)
            await model.DeleteAsync(Db);
    }
}
```

### 6. Страница

Создать `Components/Pages/NewEntityPage.razor`:

```razor
@page "/new-entities"
@using Clayzor.Lib.Web.Settings
@using Clayzor.Lib.Web.Controls
@inherits ClayGridPageBase<NewEntity>
@inject ClayAppSettings AppSettings

<PageTitle>Новые записи</PageTitle>

<ClayGrid TEntity="IClayGridRow"
           @ref="_dataGrid"
           DataLoader="this"
           Title="Новые записи"
           SelectSql="@SQLQueries.SELECT_НоваяТаблица"
           SearchColumns="@(new[]{"НазваниеНовойЗаписи"})"
           DefaultOrder="НазваниеНовойЗаписи"
           EditDialogType="@typeof(NewEntityEditDialog)"
           Items="_rows"
           Loading="_loading"
           PageSize="@AppSettings.DefaultPageSize"
           FilterColumnTypes="@FilterColumnTypes"
            TotalCount="@_query.TotalCount"
            PageNumber="@_query.PageNumber"
            ShowPagination="true"
            OnAdd="OpenAddDialog"
            OnGroupToggle="ToggleGroup">


    <ColumnDefs>
        <ClayColumnDef ColumnId="1" SqlName="КодНовойЗаписи"      DisplayName="Код"      Groupable="true" Filterable="true" />
        <ClayColumnDef ColumnId="2" SqlName="НазваниеНовойЗаписи" DisplayName="Название" Groupable="true" Filterable="true" />
    </ColumnDefs>

    <Columns>

        <ClayColumn TEntity="IClayGridRow" ColumnId="1">
            <CellTemplate>
                @if (context.Item is DetailRow<NewEntity> detail)
                {
                    <MudText Style="@($"padding-left:{(detail.Depth + 1) * 16}px")">@detail.Item.Id</MudText>
                }
            </CellTemplate>
        </ClayColumn>

        <ClayColumn TEntity="IClayGridRow" ColumnId="2">
            <CellTemplate>
                @if (context.Item is DetailRow<NewEntity> detail)
                {
                    <MudText>@detail.Item.Name</MudText>
                }
            </CellTemplate>
        </ClayColumn>

    </Columns>

</ClayGrid>

@code {
    private ClayGrid<IClayGridRow> _dataGrid = null!;
    protected override IClayGrid? Grid => _dataGrid;
}
```

**Важно:** страница передаёт всю SQL-конфигурацию через параметры `<ClayGrid>`:
- `SelectSql` — базовый SELECT
- `SearchColumns` — выходные имена колонок для поиска и фильтрации (те же, что видны в подзапросе `ROW_NUMBER()`)
- `DefaultOrder` — сортировка по умолчанию
- `EditDialogType` — тип диалога редактирования
- `DataLoader="this"` — подключает `IClayGridDataLoader`

Никаких abstract-свойств на странице не требуется.
