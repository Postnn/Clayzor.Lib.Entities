using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using Clayzor.Lib.DALC;
using Dapper;

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

    /// <summary>Идентификатор изменившего сотрудника.</summary>
    [Column(EquipmentColumns.Изменил)]
    public int ChangedById { get; set; }

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
            await db.ExecuteAsync(SQLQueries.INSERT_ОборудованиеБухУчета, WriteParameters(), commandType: CommandType.Text);
            return;
        }

        var status = await db.ExecuteWriteScalarAsync<int?>(SQLQueries.UPDATE_ID_ОборудованиеБухУчета, WriteParameters());
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
        var status = await db.ExecuteWriteScalarAsync<int?>(SQLQueries.DELETE_КодОборудования_ОборудованиеБухУчета,
            DeleteParameters(equipmentId, accountingId));
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

    internal async Task<EquipmentAccountingWriteProof> SaveEnlistedAsync(DbConnection connection,
        DbTransaction transaction, CancellationToken ct)
    {
        ApplyMaterialRules();
        var sql = Id == 0 ? SQLQueries.INSERT_ОборудованиеБухУчетаEnlisted : SQLQueries.UPDATE_ID_ОборудованиеБухУчетаEnlisted;
        var proof = await connection.QuerySingleOrDefaultAsync<EquipmentAccountingWriteProof>(new CommandDefinition(
            sql, WriteParameters(), transaction, commandType: CommandType.Text, cancellationToken: ct));
        var rejection = proof?.Status switch
        {
            1 => "Запись бухгалтерского учёта изменилась или перенесена в другое оборудование. Обновите данные.",
            2 or 3 => "Недостаточно прав для изменения бухгалтерского учёта по фактическому или новому владельцу.",
            4 => "Документ бухгалтерского учёта недоступен текущему пользователю.",
            5 => "База не подтвердила сохранение бухгалтерского учёта. Обновите данные.",
            _ => null,
        };
        if (rejection is not null)
            throw new EquipmentAccountingWriteRejectedException(rejection);
        if (proof is not { Status: 0, ConfirmedAccountingId: > 0, EffectiveOwnerId: > 0 } ||
            proof.EffectiveOwnerId != OwnerId || Id == 0 && proof.OriginalOwnerId is not null ||
            Id > 0 && (proof.ConfirmedAccountingId != Id || proof.OriginalOwnerId is not > 0))
            throw new InvalidOperationException("База не подтвердила точную запись и владельца бухгалтерского учёта.");
        return proof;
    }

    internal static async Task<EquipmentAccountingWriteProof> DeleteLatestEnlistedAsync(DbConnection connection,
        DbTransaction transaction, int equipmentId, int accountingId, CancellationToken ct)
    {
        var proof = await connection.QuerySingleOrDefaultAsync<EquipmentAccountingWriteProof>(new CommandDefinition(
            SQLQueries.DELETE_КодОборудования_ОборудованиеБухУчетаEnlisted, DeleteParameters(equipmentId, accountingId),
            transaction, commandType: CommandType.Text, cancellationToken: ct));
        var rejection = proof?.Status switch
        {
            1 => "Запись бухгалтерского учёта уже удалена. Обновите данные.",
            2 => "История бухгалтерского учёта изменилась. Обновите данные перед удалением.",
            3 => "Недостаточно прав для удаления бухгалтерского учёта.",
            5 => "База не подтвердила удаление бухгалтерского учёта. Обновите данные.",
            _ => null,
        };
        if (rejection is not null)
            throw new EquipmentAccountingWriteRejectedException(rejection);
        if (proof is not { Status: 0, OriginalOwnerId: > 0, EffectiveOwnerId: null } || proof.ConfirmedAccountingId != accountingId)
            throw new InvalidOperationException("База не подтвердила точную удалённую запись бухгалтерского учёта.");
        return proof;
    }

    private object WriteParameters() => new
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
    };

    private static object DeleteParameters(int equipmentId, int accountingId) => new
    {
        КодОборудования = equipmentId,
        КодОборудованияБухУчета = accountingId,
        RoleIdPrimary = (int)Роли.Бухгалтер,
        RoleIdSecondary = (int)Роли.Главный_бухгалтер_1С,
    };
}

internal sealed class EquipmentAccountingWriteProof
{
    /// <summary>Именованный статус SQL, полученный в транзакции записи.</summary>
    public int? Status { get; set; }
    /// <summary>Подтверждённый идентификатор изменённой записи.</summary>
    public int? ConfirmedAccountingId { get; set; }
    /// <summary>Владелец записи до изменения, когда запись существовала.</summary>
    public int? OriginalOwnerId { get; set; }
    /// <summary>Владелец записи после создания или изменения.</summary>
    public int? EffectiveOwnerId { get; set; }
}

internal sealed class EquipmentAccountingWriteRejectedException(string message) : InvalidOperationException(message);
