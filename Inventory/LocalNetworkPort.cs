using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Запись порта локальной сети из vwЛокальнаяСеть.</summary>
public sealed class LocalNetworkPort
{
    /// <summary>Идентификатор порта.</summary>
    [Column(LocalNetworkPortColumns.КодЛокальнойСети)]
    public int Id { get; set; }

    /// <summary>Идентификатор оборудования.</summary>
    [Column(LocalNetworkPortColumns.КодОборудования)]
    public int EquipmentId { get; set; }

    /// <summary>Номер порта.</summary>
    [Column(LocalNetworkPortColumns.Порт)]
    public byte Port { get; set; }

    /// <summary>Признак поддержки PoE.</summary>
    [Column(LocalNetworkPortColumns.PoE)]
    public byte Poe { get; set; }

    /// <summary>Идентификатор заказчика.</summary>
    [Column(LocalNetworkPortColumns.КодЛицаЗаказчика)]
    public int PersonCustomerId { get; set; }

    /// <summary>Идентификатор сотрудника.</summary>
    [Column(LocalNetworkPortColumns.КодСотрудника)]
    public int? EmployeeId { get; set; }

    /// <summary>Идентификатор розетки.</summary>
    [Column(LocalNetworkPortColumns.КодРозетки)]
    public int? SocketId { get; set; }

    /// <summary>Исходный текст поля «Подключено».</summary>
    [Column(LocalNetworkPortColumns.Подключено)]
    public string Connected { get; set; } = string.Empty;

    /// <summary>Исходный текст поля «Примечание».</summary>
    [Column(LocalNetworkPortColumns.Примечания)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Календарный срок возврата без UTC-преобразования.</summary>
    [Column(LocalNetworkPortColumns.СрокВозврата)]
    public DateTime? ReturnDate { get; set; }

    /// <summary>Идентификатор изменившего сотрудника.</summary>
    [Column(LocalNetworkPortColumns.Изменил)]
    public int ChangedById { get; set; }

    /// <summary>Отображаемое имя изменившего сотрудника.</summary>
    [Column(EmployeeColumns.Сотрудник)]
    public string ChangedByName { get; set; } = string.Empty;

    /// <summary>Время изменения в UTC.</summary>
    [Column(LocalNetworkPortColumns.Изменено)]
    public DateTime ChangedAtUtc { get; set; }

    /// <summary>Загружает существующий доступный порт.</summary>
    public static async Task<LocalNetworkPort?> GetAsync(
        DbManager db,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return null;
        var rows = await db.QueryRequiredAsync<LocalNetworkPort>(SQLQueries.SELECT_ID_ЛокальнаяСеть, new { Id = id }, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return rows.SingleOrDefault();
    }

    /// <summary>Возвращает сетевое имя оборудования порта.</summary>
    public static async Task<string?> GetEquipmentNetworkNameAsync(
        DbManager db,
        int equipmentId,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.QueryRequiredAsync<string>(SQLQueries.SELECT_СетевоеИмяОборудованияПоИдентификатору,
            new { EquipmentId = equipmentId }, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return rows.FirstOrDefault();
    }

    /// <summary>Возвращает соседние порты; freeOnly ограничивает список незанятыми.</summary>
    public static async Task<IReadOnlyList<LocalNetworkNeighborPort>> GetNeighborsAsync(
        DbManager db,
        int id,
        bool freeOnly,
        CancellationToken cancellationToken = default)
    {
        var sql = freeOnly ? SQLQueries.SELECT_СоседниеПортыНеЗанятые : SQLQueries.SELECT_СоседниеПортыВсе;
        var rows = await db.QueryRequiredAsync<LocalNetworkNeighborPort>(sql, new { КодЛокальнойСети = id }, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return rows.ToList();
    }

    /// <summary>Находит ближайший офис последней записи расположения оборудования.</summary>
    public static async Task<int?> GetOfficeLocationIdAsync(
        DbManager db,
        int equipmentId,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.QueryRequiredAsync<int>(SQLQueries.SELECT_ОфисПоКодуОборудования,
            new { КодОборудования = equipmentId }, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return rows.FirstOrDefault() is > 0 and var id ? id : null;
    }

    /// <summary>Проверяет обязательного заказчика и связь розетки с сотрудником или подключённым текстом.</summary>
    public string? Validate()
    {
        if (PersonCustomerId <= 0)
            return "Не выбран заказчик.";
        if (SocketId is > 0 && EmployeeId is null && string.IsNullOrEmpty(Connected))
            return "Вы указали розетку. В этом случае необходимо указать сотрудника или что подключено.";
        return null;
    }

    /// <summary>Атомарно сохраняет обычное редактирование, переключение или обмен с проверкой актуальных записей.</summary>
    public static async Task SaveAsync(
        DbManager db,
        LocalNetworkPort draft,
        DateTime expectedChangedAtUtc,
        int? switchToId = null,
        int? exchangeWithId = null)
    {
        var validation = draft.Validate();
        if (validation is not null)
            throw new InvalidOperationException(validation);
        if (switchToId is not null && exchangeWithId is not null)
            throw new InvalidOperationException("Одновременно можно выбрать только переключение или обмен.");
        if (switchToId <= 0 || exchangeWithId <= 0 || switchToId == draft.Id || exchangeWithId == draft.Id)
            throw new InvalidOperationException("Выбранный порт недопустим. Обновите список.");

        var sql = switchToId is not null
            ? SQLQueries.SWITCH_ЛокальнаяСеть
            : exchangeWithId is not null
                ? SQLQueries.EXCHANGE_ЛокальнаяСеть
                : SQLQueries.UPDATE_ЛокальнаяСеть;
        var status = await db.ExecuteWriteScalarAsync<int?>(sql, new
        {
            draft.Id,
            ExpectedChangedAt = expectedChangedAtUtc,
            TargetId = switchToId ?? exchangeWithId ?? 0,
            PersonCustomerId = draft.PersonCustomerId,
            EmployeeId = draft.EmployeeId,
            SocketId = draft.SocketId,
            Connected = draft.Connected?.Trim() ?? string.Empty,
            Description = draft.Description?.Trim() ?? string.Empty,
            ReturnDate = draft.ReturnDate?.Date,
        });
        if (status == 0)
            return;
        if (status == 1)
            throw new InvalidOperationException("Запись локальной сети изменилась или недоступна. Обновите данные.");
        if (status == 2)
            throw new InvalidOperationException("Не выбран заказчик.");
        if (status == 3)
            throw new InvalidOperationException("Выбранный порт недопустим или уже занят. Обновите список.");
        throw new InvalidOperationException("Не получен корректный результат сохранения локальной сети. Обновите данные и проверьте состояние записи.");
    }

    /// <summary>Перезапускает PoE после актуальной серверной проверки записи; timeout соответствует legacy 120 секундам.</summary>
    public static Task ResetPoeAsync(DbManager db, int id) => db.ExecuteAsync(
        SQLQueries.EXEC_ResetPoEport,
        new { КодЛокальнойСети = id },
        commandType: CommandType.Text,
        commandTimeout: 120);
}

/// <summary>Соседний порт с готовой legacy-подписью.</summary>
public sealed class LocalNetworkNeighborPort : ILookupEntity
{
    /// <summary>Идентификатор порта.</summary>
    [Column(LocalNetworkPortColumns.КодЛокальнойСети)]
    public int Id { get; set; }

    /// <summary>Подпись «СетевоеИмя, порт:N».</summary>
    [Column(LocalNetworkPortColumns.Строка)]
    public string Name { get; set; } = string.Empty;
}
