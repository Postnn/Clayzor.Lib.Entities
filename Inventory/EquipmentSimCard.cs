using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Text.RegularExpressions;
using Clayzor.Lib.DALC;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Сведения SIM-карты без секретных кодов.</summary>
public sealed class EquipmentSimCard
{
    /// <summary>Идентификатор оборудования.</summary>
    [Column(EquipmentColumns.КодОборудования)]
    public int EquipmentId { get; set; }

    /// <summary>Номер телефона.</summary>
    [Column(SimCardColumns.НомерТелефона)]
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Дата активации в UTC.</summary>
    [Column(EquipmentPeriodColumns.От)]
    public DateTime ActivatedAtUtc { get; set; }

    /// <summary>Договор связи.</summary>
    [Column(SimCardColumns.КодДоговора)]
    public int? ContractId { get; set; }

    /// <summary>Лицо персональных данных.</summary>
    [Column(SimCardColumns.КодЛицаПерсональныхДанных)]
    public int? PersonalDataPersonId { get; set; }

    /// <summary>Лицо исполнителя.</summary>
    [Column(SimCardColumns.КодЛицаИсполнителя)]
    public int? ExecutorPersonId { get; set; }

    /// <summary>Название договора.</summary>
    [Column(SimCardColumns.Договор)]
    public string ContractName { get; set; } = string.Empty;

    /// <summary>Название лица персональных данных.</summary>
    [Column(SimCardColumns.ЛицоПерсональныхДанных)]
    public string PersonalDataPersonName { get; set; } = string.Empty;

    /// <summary>Идентификатор изменившего сотрудника.</summary>
    [Column(EquipmentColumns.Изменил)]
    public int ChangedById { get; set; }

    /// <summary>ФИО изменившего сотрудника.</summary>
    [Column(EquipmentPeriodColumns.ИзменилФИО)]
    public string ChangedByName { get; set; } = string.Empty;

    /// <summary>Время изменения.</summary>
    [Column(EquipmentColumns.Изменено)]
    public DateTime ChangedAt { get; set; }

    /// <summary>Секретные коды, не входящие в общее чтение списка.</summary>
    [NotMapped]
    public EquipmentSimSecrets Secrets { get; set; } = new();

    /// <summary>Проверяет обязательные поля и форматы номера и кодов.</summary>
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(PhoneNumber) || !Regex.IsMatch(PhoneNumber, "^[0-9]+$"))
            return "Номер телефона должен содержать только цифры.";
        if (PhoneNumber.Length > 50)
            return "Номер телефона не может быть длиннее 50 цифр.";
        if (ContractId is null)
            return "Выберите договор связи.";
        if (ActivatedAtUtc == default)
            return "Укажите дату и время активации.";
        if (!ValidCode(Secrets.Pin, 4, 4))
            return "PIN должен содержать ровно 4 цифры.";
        if (!ValidCode(Secrets.Pin2, 4, 8))
            return "PIN2 должен содержать от 4 до 8 цифр.";
        if (!ValidCode(Secrets.Puk, 8, 8))
            return "PUK должен содержать ровно 8 цифр.";
        if (!ValidCode(Secrets.Puk2, 8, 8))
            return "PUK2 должен содержать ровно 8 цифр.";
        return null;
    }

    /// <summary>Атомарно списывает дубликат и сохраняет текущую SIM.</summary>
    public Task SaveAsync(DbManager db, int? duplicateEquipmentId = null)
    {
        var command = EquipmentSimSaveBuilder.BuildSave(this, duplicateEquipmentId);
        return db.ExecuteAsync(command.Sql, command.Parameters, commandType: CommandType.Text);
    }

    private static bool ValidCode(string? value, int min, int max) =>
        string.IsNullOrEmpty(value) || value.Length >= min && value.Length <= max && Regex.IsMatch(value, "^[0-9]+$");
}
