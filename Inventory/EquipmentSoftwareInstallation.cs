using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Запись установки программного обеспечения.</summary>
public sealed class EquipmentSoftwareInstallation
{
    /// <summary>Идентификатор записи установки.</summary>
    [Column(SoftwareInstallationColumns.КодОборудованияПО)]
    public int Id { get; set; }

    /// <summary>Идентификатор оборудования.</summary>
    [Column(EquipmentColumns.КодОборудования)]
    public int EquipmentId { get; set; }

    /// <summary>Идентификатор программного оборудования.</summary>
    [Column(SoftwareInstallationColumns.КодПО)]
    public int? SoftwareId { get; set; }

    /// <summary>Название программного обеспечения.</summary>
    [Column(SoftwareInstallationColumns.НазваниеПО)]
    public string SoftwareName { get; set; } = string.Empty;

    /// <summary>Дата установки.</summary>
    [Column(EquipmentPeriodColumns.От)]
    public DateTime? InstalledAt { get; set; }

    /// <summary>Включительная дата деинсталляции.</summary>
    [Column(EquipmentPeriodColumns.До)]
    public DateTime? UninstalledAt { get; set; }

    /// <summary>Установленная версия или модель.</summary>
    [Column(SoftwareInstallationColumns.Установлено)]
    public string Installed { get; set; } = string.Empty;

    /// <summary>Дистрибутив.</summary>
    [Column(SoftwareInstallationColumns.Дистрибутив)]
    public string Distribution { get; set; } = string.Empty;

    /// <summary>Примечания.</summary>
    [Column(EquipmentColumns.Примечания)]
    public string Notes { get; set; } = string.Empty;

    /// <summary>Признак деинсталляции.</summary>
    [Column(SoftwareInstallationColumns.Деинсталировано)]
    public byte IsUninstalled { get; set; }

    /// <summary>Идентификатор изменившего сотрудника.</summary>
    [Column(EquipmentColumns.Изменил)]
    public int ChangedById { get; set; }

    /// <summary>ФИО изменившего сотрудника.</summary>
    [Column(EquipmentPeriodColumns.ИзменилФИО)]
    public string ChangedByName { get; set; } = string.Empty;

    /// <summary>Время изменения.</summary>
    [Column(EquipmentColumns.Изменено)]
    public DateTime ChangedAt { get; set; }

    /// <summary>Проверяет обязательные поля и порядок дат.</summary>
    public string? Validate()
    {
        if (InstalledAt is null)
            return "Укажите дату установки.";
        if (SoftwareId is null)
            return "Выберите программное обеспечение.";
        if (string.IsNullOrWhiteSpace(Installed))
            return "Укажите установленную версию.";
        if (string.IsNullOrWhiteSpace(Distribution))
            return "Укажите дистрибутив.";
        if (UninstalledAt is not null && InstalledAt.Value.Date > UninstalledAt.Value.Date)
            return "Дата установки не может быть позже деинсталляции.";
        return null;
    }

    /// <summary>Добавляет или обновляет запись с эксклюзивной датой деинсталляции.</summary>
    public async Task SaveAsync(DbManager db)
    {
        var sql = Id == 0 ? SQLQueries.INSERT_ОборудованиеПО : SQLQueries.UPDATE_ID_ОборудованиеПО;
        var affectedRows = await db.ExecuteAsync(sql, WriteParameters(), commandType: CommandType.Text);
        if (affectedRows != 1)
            throw new InvalidOperationException(
                "Установка ПО была изменена или удалена другим пользователем. Обновите данные и повторите операцию.");
    }

    internal async Task<int> SaveEnlistedAsync(DbConnection connection, DbTransaction transaction, CancellationToken ct)
    {
        var sql = Id == 0 ? SQLQueries.INSERT_ОборудованиеПОEnlisted : SQLQueries.UPDATE_ID_ОборудованиеПОEnlisted;
        var proof = await connection.QuerySingleOrDefaultAsync<SoftwareWriteProof>(new CommandDefinition(sql, WriteParameters(), transaction,
            commandType: CommandType.Text, cancellationToken: ct));
        var result = proof?.ConfirmedInstallationId;
        if (result == 0)
            throw new EquipmentSoftwareWriteRejectedException(
                "Состояние установки ПО изменилось или база не подтвердила запись. Обновите данные.");
        if (result is not > 0 || Id > 0 && result != Id)
            throw new InvalidOperationException("База не подтвердила точный идентификатор установки ПО.");
        return result.Value;
    }

    private sealed class SoftwareWriteProof
    {
        /// <summary>Ключ проверенной сохранённой установки или 0 при доказанном отказе.</summary>
        public int? ConfirmedInstallationId { get; set; }
    }

    private object WriteParameters() => new
    {
        КодОборудованияПО = Id,
        КодОборудования = EquipmentId,
        ИсходныйКодОборудования = EquipmentId,
        ИсходноеИзменено = ChangedAt,
        КодПО = SoftwareId,
        От = InstalledAt?.Date,
        До = UninstalledAt?.Date,
        Установлено = Installed,
        Дистрибутив = Distribution,
        Примечания = Notes ?? string.Empty,
    };
}

internal sealed class EquipmentSoftwareWriteRejectedException(string message) : InvalidOperationException(message);
