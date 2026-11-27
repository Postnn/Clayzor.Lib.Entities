using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
using Clayzor.Lib.DALC;

namespace Clayzor.Lib.Entities.Inventory;

internal sealed class EquipmentSimWriteRejectedException(string message) : InvalidOperationException(message);

/// <summary>Сведения SIM-карты без секретных кодов.</summary>
public sealed class EquipmentSimCard
{
    private static readonly byte[] VersionKey = RandomNumberGenerator.GetBytes(32);
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

    /// <summary>Признак доступной текущему пользователю детализации.</summary>
    [Column(SimCardColumns.Подчинённый)]
    public byte IsSubordinate { get; set; }

    /// <summary>Признак начисления НДС на общую сумму договора.</summary>
    [Column(SimCardColumns.НДСНаСумму)]
    public byte? VatOnTotal { get; set; }

    /// <summary>Признак включения НДС в тарификацию.</summary>
    [Column(SimCardColumns.НДСВТарификации)]
    public byte? VatInRating { get; set; }

    /// <summary>Ставка НДС по условиям договора.</summary>
    [Column(SimCardColumns.СтавкаНДС)]
    public decimal VatRate { get; set; }

    /// <summary>Число знаков округления суммы.</summary>
    [Column(SimCardColumns.ОкруглениеСуммы)]
    public int AmountScale { get; set; }

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

    /// <summary>Строит адрес внешней детализации с параметрами и категорией НДС legacy.</summary>
    public string BuildAccountingPhoneUri(string baseUri, string responsibleName)
    {
        if (string.IsNullOrWhiteSpace(baseUri))
            throw new ArgumentException("Не задан адрес формы детализации.", nameof(baseUri));
        var vatCalculation = VatCalculation();
        var (color, title) = vatCalculation switch
        {
            0 => ("background:darkorange;", "Не определены условия договора сотовой связи"),
            2 => ("background:lightgray;", "Сумма указана без НДС"),
            _ => ("background:white;", "Сумма указана с НДС"),
        };
        var parameters = new (string Key, string? Value)[]
        {
            ("UserT", responsibleName),
            ("Dogovor", ContractId?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("DogovorT", ContractName),
            ("Phone", PhoneNumber),
            ("NDSAllSumm", vatCalculation.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("NDSIn", VatInRating?.ToString() ?? "-1"),
            ("NDSStavka", VatRate.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("Scale", AmountScale.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("OpenMonth", "1"), ("Year", "0"), ("Month", "0"), ("bln", "0"),
            ("User", string.Empty), ("Color", color), ("Title", title), ("standalone", "1"),
        };
        var query = string.Join("&", parameters.Select(parameter =>
            $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value ?? string.Empty)}"));
        var builder = new UriBuilder(baseUri);
        var existingQuery = builder.Query.TrimStart('?');
        builder.Query = string.IsNullOrEmpty(existingQuery) ? query : $"{existingQuery}&{query}";
        return builder.Uri.AbsoluteUri;
    }

    /// <summary>Секретные коды, не входящие в общее чтение списка.</summary>
    [NotMapped, JsonIgnore]
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

    /// <summary>Непрозрачная версия полного исходного снимка после отдельного защищённого чтения.</summary>
    [NotMapped, JsonIgnore]
    public string? EditorVersion { get; internal set; }

    /// <summary>Поля уже видимой строки, с которыми сверяется защищённый снимок перед открытием.</summary>
    public EquipmentSimOriginal CaptureOriginal() =>
        new(EquipmentId, PhoneNumber, ActivatedAtUtc, ContractId, PersonalDataPersonId, ChangedById, ChangedAt);

    /// <summary>Читает один защищённый снимок SIM после серверной проверки права вызывающим сервисом.</summary>
    public static async Task<EquipmentSimCard?> ReadEditorSnapshotAsync(DbManager db, int equipmentId)
    {
        var row = await RunProtectedAsync(db, connection => connection.QuerySingleOrDefaultAsync<EditorRow>(
            SQLQueries.SELECT_SIMКарта_СнимокРедактора, new { EquipmentId = equipmentId }), write: false);
        var snapshot = MaterializeSnapshot(row);
        if (snapshot is not null)
            snapshot.EditorVersion = snapshot.CaptureVersion();
        return snapshot;
    }

    private static EquipmentSimCard? MaterializeSnapshot(EditorRow? row)
    {
        if (row is null)
            return null;
        return new EquipmentSimCard
        {
            EquipmentId = row.EquipmentId, PhoneNumber = row.PhoneNumber, ActivatedAtUtc = row.ActivatedAtUtc,
            ContractId = row.ContractId, PersonalDataPersonId = row.PersonalDataPersonId,
            ChangedById = row.ChangedById, ChangedAt = row.ChangedAt,
            Secrets = new EquipmentSimSecrets { Pin = row.Pin ?? string.Empty, Pin2 = row.Pin2 ?? string.Empty,
                Puk = row.Puk ?? string.Empty, Puk2 = row.Puk2 ?? string.Empty },
        };
    }

    /// <summary>Добавляет отсутствующую либо обновляет неизменённую SIM, атомарно списывая подтверждённый дубликат.</summary>
    public async Task SaveAsync(DbManager db, EquipmentSimWriteMode mode, string? expectedVersion, int? duplicateEquipmentId = null)
    {
        EquipmentSimCard? original = mode switch
        {
            EquipmentSimWriteMode.Add when expectedVersion is null => null,
            EquipmentSimWriteMode.Edit => await ReadExpectedAsync(db, EquipmentId, expectedVersion),
            _ => throw Conflict(),
        };
        await ExecuteWriteAsync(db, EquipmentSimSaveBuilder.BuildSave(this, mode, original, duplicateEquipmentId));
    }

    /// <summary>Удаляет только подтверждённую исходную строку SIM.</summary>
    public static async Task DeleteAsync(DbManager db, int equipmentId, string? expectedVersion)
    {
        var original = await ReadExpectedAsync(db, equipmentId, expectedVersion);
        await ExecuteWriteAsync(db, EquipmentSimSaveBuilder.BuildDelete(original));
    }

    internal async Task<int> SaveEnlistedAsync(DbConnection connection, DbTransaction transaction, EquipmentSimWriteMode mode,
        string? expectedVersion, int? duplicateEquipmentId, CancellationToken ct)
    {
        EquipmentSimCard? original = mode switch
        {
            EquipmentSimWriteMode.Add when expectedVersion is null => null,
            EquipmentSimWriteMode.Edit => await RequireEnlistedSnapshotAsync(connection, transaction, EquipmentId, expectedVersion, ct),
            _ => throw SimWriteConflict(),
        };
        var personalDataPerson = PersonalDataPersonId;
        if (duplicateEquipmentId is int duplicateId)
        {
            var duplicate = await ReadEnlistedSnapshotAsync(connection, transaction, duplicateId, ct);
            if (duplicate is null || duplicate.EquipmentId != duplicateId)
                throw SimWriteConflict();
            personalDataPerson ??= duplicate.PersonalDataPersonId;
        }

        await RunEnlistedWriteAsync(connection, transaction,
            EquipmentSimSaveBuilder.BuildSave(this, mode, original, duplicateEquipmentId, enlisted: true), ct);
        var saved = await ReadEnlistedSnapshotAsync(connection, transaction, EquipmentId, ct);
        var activated = new SqlDateTime(ActivatedAtUtc).Value;
        if (saved is null || saved.EquipmentId != EquipmentId || saved.PhoneNumber != PhoneNumber ||
            saved.ActivatedAtUtc != activated || saved.ContractId != ContractId || saved.PersonalDataPersonId != personalDataPerson ||
            saved.Secrets.Pin != (Secrets.Pin ?? string.Empty) || saved.Secrets.Pin2 != (Secrets.Pin2 ?? string.Empty) ||
            saved.Secrets.Puk != (Secrets.Puk ?? string.Empty) || saved.Secrets.Puk2 != (Secrets.Puk2 ?? string.Empty))
            throw SimWriteConflict();

        if (duplicateEquipmentId is int retiredId)
        {
            var retired = await connection.QuerySingleOrDefaultAsync<Equipment>(new CommandDefinition(
                SQLQueries.SELECT_ID_Оборудование, new { Id = retiredId }, transaction, cancellationToken: ct));
            if (retired?.Id != retiredId || retired.WriteOffAtUtc != activated)
                throw SimWriteConflict();
        }
        return EquipmentId;
    }

    internal static async Task<int> DeleteEnlistedAsync(DbConnection connection, DbTransaction transaction,
        int equipmentId, string? expectedVersion, CancellationToken ct)
    {
        var original = await RequireEnlistedSnapshotAsync(connection, transaction, equipmentId, expectedVersion, ct);
        await RunEnlistedWriteAsync(connection, transaction, EquipmentSimSaveBuilder.BuildDelete(original, enlisted: true), ct);
        if (await ReadEnlistedSnapshotAsync(connection, transaction, equipmentId, ct) is not null)
            throw SimWriteConflict();
        return equipmentId;
    }

    private static async Task<EquipmentSimCard> RequireEnlistedSnapshotAsync(DbConnection connection, DbTransaction transaction,
        int equipmentId, string? expectedVersion, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(expectedVersion)) throw SimWriteConflict();
        var actual = await ReadEnlistedSnapshotAsync(connection, transaction, equipmentId, ct);
        if (actual?.EquipmentId != equipmentId || !string.Equals(expectedVersion, actual.CaptureVersion(), StringComparison.Ordinal))
            throw SimWriteConflict();
        return actual;
    }

    internal static async Task<EquipmentSimCard?> ReadEditorSnapshotAsync(
        DbConnection connection, int equipmentId, CancellationToken ct)
    {
        var snapshot = await ReadEnlistedSnapshotAsync(connection, null, equipmentId, ct);
        if (snapshot is not null)
            snapshot.EditorVersion = snapshot.CaptureVersion();
        return snapshot;
    }

    private static async Task<EquipmentSimCard?> ReadEnlistedSnapshotAsync(DbConnection connection, DbTransaction? transaction,
        int equipmentId, CancellationToken ct) => MaterializeSnapshot(await connection.QuerySingleOrDefaultAsync<EditorRow>(
        new CommandDefinition(SQLQueries.SELECT_SIMКарта_СнимокРедактора, new { EquipmentId = equipmentId }, transaction,
            commandType: CommandType.Text, cancellationToken: ct)));

    private static async Task RunEnlistedWriteAsync(DbConnection connection, DbTransaction transaction,
        EquipmentPhoneSaveCommand command, CancellationToken ct)
    {
        var status = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(command.Sql, command.Parameters, transaction,
            commandType: CommandType.Text, cancellationToken: ct));
        if (status == -1)
            throw new EquipmentSimWriteRejectedException("Дубликат SIM изменился или уже списан. Сверьте актуальные данные.");
        if (status == 0) throw SimWriteConflict();
        if (status != 1) throw new InvalidOperationException("База не подтвердила результат операции SIM.");
    }

    private static EquipmentSimWriteRejectedException SimWriteConflict() =>
        new("Исходное состояние или результат SIM не подтверждены. Сверьте актуальные данные.");

    private static async Task<EquipmentSimCard> ReadExpectedAsync(DbManager db, int equipmentId, string? expectedVersion)
    {
        if (string.IsNullOrWhiteSpace(expectedVersion))
            throw Conflict();
        var original = await ReadEditorSnapshotAsync(db, equipmentId);
        if (original is null || !string.Equals(expectedVersion, original.EditorVersion, StringComparison.Ordinal))
            throw Conflict();
        return original;
    }

    private static async Task ExecuteWriteAsync(DbManager db, EquipmentPhoneSaveCommand command)
    {
        var result = await RunProtectedAsync(db, connection => connection.ExecuteScalarAsync<int>(
            command.Sql, command.Parameters, commandType: CommandType.Text), write: true);
        if (result == -1)
            throw new InvalidOperationException("Дубликат SIM изменился или уже списан. Сверьте актуальные данные.");
        if (result != 1)
            throw Conflict();
    }

    private static async Task<T> RunProtectedAsync<T>(DbManager db, Func<DbConnection, Task<T>> action, bool write)
    {
        try
        {
            return await db.RunDbAsync(async connection =>
            {
                try { return await action(connection); }
                catch (DbException)
                {
                    throw new EquipmentSimStorageException(write);
                }
            });
        }
        catch (DbException)
        {
            throw new EquipmentSimStorageException(false);
        }
    }

    internal string CaptureVersion()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.Unicode, leaveOpen: true);
        writer.Write(EquipmentId);
        writer.Write(PhoneNumber);
        writer.Write(ActivatedAtUtc.Ticks);
        writer.Write(ContractId.HasValue);
        if (ContractId is int contractId) writer.Write(contractId);
        writer.Write(PersonalDataPersonId.HasValue);
        if (PersonalDataPersonId is int personId) writer.Write(personId);
        writer.Write(ChangedById);
        writer.Write(ChangedAt.Ticks);
        writer.Write(Secrets.Pin);
        writer.Write(Secrets.Pin2);
        writer.Write(Secrets.Puk);
        writer.Write(Secrets.Puk2);
        var bytes = stream.GetBuffer().AsSpan(0, checked((int)stream.Length));
        try { return Convert.ToHexString(HMACSHA256.HashData(VersionKey, bytes)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static InvalidOperationException Conflict() =>
        new("Исходное состояние SIM не подтверждено. Ввод сохранён. Сверьте актуальные данные перед записью.");

    private sealed class EditorRow
    {
        public int EquipmentId { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime ActivatedAtUtc { get; set; }
        public int? ContractId { get; set; }
        public int? PersonalDataPersonId { get; set; }
        public int ChangedById { get; set; }
        public DateTime ChangedAt { get; set; }
        public string Pin { get; set; } = string.Empty;
        public string Pin2 { get; set; } = string.Empty;
        public string Puk { get; set; } = string.Empty;
        public string Puk2 { get; set; } = string.Empty;
    }

    private int VatCalculation()
    {
        if (VatOnTotal is null || VatInRating is null)
            return 0;
        if (VatInRating == 1)
            return 3;
        if (VatOnTotal == 1)
            return 2;
        return VatOnTotal == 0 && VatInRating == 0 ? 1 : 0;
    }

    private static bool ValidCode(string? value, int min, int max) =>
        string.IsNullOrEmpty(value) || value.Length >= min && value.Length <= max && Regex.IsMatch(value, "^[0-9]+$");
}

/// <summary>Явный режим записи SIM без UPSERT.</summary>
public enum EquipmentSimWriteMode
{
    /// <summary>Создание ожидаемо отсутствующей строки.</summary>
    Add = 1,
    /// <summary>Изменение существующей строки с ожидаемой версией.</summary>
    Edit = 2,
}

/// <summary>Проверяемые исходные сведения SIM без PIN/PUK.</summary>
/// <param name="EquipmentId">Ключ SIM.</param>
/// <param name="PhoneNumber">Исходный номер телефона.</param>
/// <param name="ActivatedAtUtc">Исходная активация UTC.</param>
/// <param name="ContractId">Исходный договор.</param>
/// <param name="PersonalDataPersonId">Исходное лицо ПДн.</param>
/// <param name="ChangedById">Исходный изменивший сотрудник.</param>
/// <param name="ChangedAt">Исходный точный timestamp.</param>
public sealed record EquipmentSimOriginal(int EquipmentId, string PhoneNumber, DateTime ActivatedAtUtc,
    int? ContractId, int? PersonalDataPersonId, int ChangedById, DateTime ChangedAt);

/// <summary>Ошибка чтения или записи SIM без исходного SQL исключения и PIN/PUK.</summary>
/// <param name="wasDispatched">Провайдеру передана команда записи.</param>
public sealed class EquipmentSimStorageException(bool wasDispatched) : InvalidOperationException(wasDispatched
    ? "Запись SIM отклонена."
    : "Не удалось прочитать исходные сведения SIM. Команда не отправлена.")
{
    /// <summary>Провайдеру передана команда записи.</summary>
    public bool WasDispatched { get; } = wasDispatched;
}
