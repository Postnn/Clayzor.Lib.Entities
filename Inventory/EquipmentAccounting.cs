using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using Clayzor.Lib.DALC;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Запись бухгалтерского учёта оборудования.</summary>
public sealed class EquipmentAccounting
{
    /// <summary>Идентификатор записи.</summary>
    [Column(AccountingColumns.КодОборудованияБухУчета)]
    public int Id { get; set; }

    /// <summary>Идентификатор оборудования.</summary>
    [Column(EquipmentColumns.КодОборудования)]
    public int EquipmentId { get; set; }

    /// <summary>Лицо бухгалтерии-владельца.</summary>
    [Column(AccountingColumns.КодБухгалтерии)]
    public int? OwnerId { get; set; }

    /// <summary>Название бухгалтерии.</summary>
    [Column(AccountingColumns.Бухгалтерия)]
    public string OwnerName { get; set; } = string.Empty;

    /// <summary>Признак материала, иначе основное средство.</summary>
    [Column(AccountingColumns.Материал)]
    public bool IsMaterial { get; set; }

    /// <summary>Арендатор.</summary>
    [Column(EquipmentColumns.КодЛицаАрендатора)]
    public int? TenantId { get; set; }

    /// <summary>Название арендатора.</summary>
    [Column(AccountingColumns.Арендатор)]
    public string TenantName { get; set; } = string.Empty;

    /// <summary>Инвентарный номер или «Материал» в истории.</summary>
    [Column(AccountingColumns.ИнвентарныйНомер)]
    public string InventoryNumber { get; set; } = string.Empty;

    /// <summary>Дата поступления.</summary>
    [Column(AccountingColumns.ДатаПоступления)]
    public DateTime? DateFrom { get; set; }

    /// <summary>Документ поступления.</summary>
    [Column(EquipmentColumns.КодДокументаПоступления)]
    public int? ReceiptDocumentId { get; set; }

    /// <summary>Дата списания.</summary>
    [Column(AccountingColumns.ДатаСписания)]
    public DateTime? DateTo { get; set; }

    /// <summary>Документ списания.</summary>
    [Column(EquipmentColumns.КодДокументаСписания)]
    public int? WriteOffDocumentId { get; set; }

    /// <summary>Примечания.</summary>
    [Column(EquipmentColumns.Примечания)]
    public string Notes { get; set; } = string.Empty;

    /// <summary>ФИО изменившего сотрудника.</summary>
    [Column(EquipmentPeriodColumns.ИзменилФИО)]
    public string ChangedByName { get; set; } = string.Empty;

    /// <summary>Время изменения.</summary>
    [Column(EquipmentColumns.Изменено)]
    public DateTime ChangedAt { get; set; }

    /// <summary>Очищает арендатора и номер для материала.</summary>
    public void ApplyMaterialRules()
    {
        if (!IsMaterial)
            return;
        TenantId = null;
        TenantName = string.Empty;
        InventoryNumber = string.Empty;
    }

    /// <summary>Проверяет владельца, номер и даты.</summary>
    public string? Validate()
    {
        if (OwnerId is null)
            return "Выберите владельца бухгалтерского учёта.";
        if (DateFrom is null)
            return "Укажите дату поступления.";
        if (!IsMaterial && string.IsNullOrWhiteSpace(InventoryNumber))
            return "Укажите инвентарный номер основного средства.";
        if (!IsMaterial && !InventoryNumber.Any(char.IsDigit))
            return "Инвентарный номер должен содержать хотя бы одну цифру.";
        if (DateTo is not null && DateFrom.Value.Date > DateTo.Value.Date)
            return "Дата поступления не может быть позже списания.";
        return null;
    }

    /// <summary>Добавляет запись или атомарно обновляет существующую с проверкой версии, владельцев и документов.</summary>
    public async Task SaveAsync(DbManager db)
    {
        ApplyMaterialRules();
        if (Id == 0)
        {
            await db.ExecuteAsync(SQLQueries.INSERT_ОборудованиеБухУчета, new
            {
                КодОборудованияБухУчета = Id,
                КодОборудования = EquipmentId,
                КодЛица = OwnerId,
                Материал = IsMaterial,
                КодЛицаАрендатора = TenantId,
                ИнвентарныйНомер = InventoryNumber ?? string.Empty,
                От = DateFrom?.Date,
                КодДокументаПоступления = ReceiptDocumentId,
                До = DateTo?.Date,
                КодДокументаСписания = WriteOffDocumentId,
                Примечания = Notes ?? string.Empty,
            }, commandType: CommandType.Text);
            return;
        }

        var status = await db.ExecuteWriteScalarAsync<int?>(SQLQueries.UPDATE_ID_ОборудованиеБухУчета, new
        {
            КодОборудованияБухУчета = Id,
            КодОборудования = EquipmentId,
            КодЛица = OwnerId,
            Материал = IsMaterial,
            КодЛицаАрендатора = TenantId,
            ИнвентарныйНомер = InventoryNumber ?? string.Empty,
            От = DateFrom?.Date,
            КодДокументаПоступления = ReceiptDocumentId,
            До = DateTo?.Date,
            КодДокументаСписания = WriteOffDocumentId,
            Примечания = Notes ?? string.Empty,
            ExpectedChangedAt = ChangedAt,
            RoleIdPrimary = (int)Роли.Бухгалтер,
            RoleIdSecondary = (int)Роли.Главный_бухгалтер_1С,
        });
        if (status == 0)
            return;
        if (status == 1)
            throw new InvalidOperationException("Запись бухгалтерского учёта изменилась или перенесена в другое оборудование. Обновите данные.");
        if (status == 2 || status == 3)
            throw new UnauthorizedAccessException("Недостаточно прав для изменения бухгалтерского учёта по фактическому или новому владельцу.");
        if (status == 4)
            throw new InvalidOperationException("Документ бухгалтерского учёта недоступен текущему пользователю.");
        throw new InvalidOperationException("Не получен корректный результат обновления бухгалтерского учёта. Обновите данные и проверьте состояние записи.");
    }

    /// <summary>Атомарно удаляет подтверждённую запись, если она всё ещё последняя и доступна текущему пользователю.</summary>
    public static async Task DeleteLatestAsync(DbManager db, int equipmentId, int accountingId)
    {
        var status = await db.ExecuteWriteScalarAsync<int?>(SQLQueries.DELETE_КодОборудования_ОборудованиеБухУчета, new
        {
            КодОборудования = equipmentId,
            КодОборудованияБухУчета = accountingId,
            RoleIdPrimary = 66,
            RoleIdSecondary = 68,
        });
        if (status == 0)
            return;
        if (status == 1)
            throw new InvalidOperationException("Запись бухгалтерского учёта уже удалена. Обновите данные.");
        if (status == 2)
            throw new InvalidOperationException("История бухгалтерского учёта изменилась. Обновите данные перед удалением.");
        if (status == 3)
            throw new UnauthorizedAccessException("Недостаточно прав для удаления бухгалтерского учёта.");
        throw new InvalidOperationException("Не получен корректный результат удаления бухгалтерского учёта. Обновите данные и проверьте состояние записи.");
    }
}
