using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Data.SqlClient;
using Clayzor.Lib.DALC;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Учётная запись оборудования без пароля в общем списке.</summary>
public sealed class EquipmentAccount
{
    private static readonly byte[] VersionKey = RandomNumberGenerator.GetBytes(32);

    /// <summary>Идентификатор оборудования.</summary>
    [Column(EquipmentColumns.КодОборудования)]
    public int EquipmentId { get; set; }

    /// <summary>Логин, часть составного ключа.</summary>
    [Column(EquipmentAccountColumns.Логин)]
    public string Login { get; set; } = string.Empty;

    /// <summary>Пароль, загружаемый только отдельным авторизованным запросом.</summary>
    [JsonIgnore]
    [Column(EquipmentAccountColumns.Пароль)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Признак ограниченных прав (0 или 1).</summary>
    [Column(EquipmentAccountColumns.ОграниченныеПрава)]
    public byte LimitedRights { get; set; }

    /// <summary>Примечание к учётной записи.</summary>
    [Column(PhoneColumns.Примечание)]
    public string Notes { get; set; } = string.Empty;

    /// <summary>Непрозрачная подпись исходной строки для редактирования и удаления.</summary>
    [NotMapped, JsonIgnore]
    public string? ExpectedVersion { get; set; }

    /// <summary>Читает один полный снимок учётной записи для отдельной авторизованной операции.</summary>
    public static async Task<EquipmentAccount?> ReadEditorSnapshotAsync(DbManager db, int equipmentId, string login)
    {
        try
        {
            return await db.RunDbAsync(async connection =>
            {
                try
                {
                    var snapshot = await ReadSnapshotAsync(connection, equipmentId, login);
                    if (snapshot is not null)
                        snapshot.ExpectedVersion = snapshot.CaptureVersion();
                    return snapshot;
                }
                catch (DbException)
                {
                    throw new EquipmentAccountStorageException(false);
                }
            });
        }
        catch (DbException)
        {
            throw new EquipmentAccountStorageException(false);
        }
    }

    /// <summary>Добавляет учётную запись через защищённый от вывода параметров путь.</summary>
    public Task InsertAsync(DbManager db) => WriteAsync(db, SQLQueries.INSERT_ОборудованиеУчётныеЗаписи, compare: false, delete: false);

    /// <summary>Обновляет пароль, права и примечание с атомарной проверкой исходного состояния.</summary>
    public Task UpdateAsync(DbManager db) => WriteAsync(db, SQLQueries.UPDATE_ОборудованиеУчётныеЗаписи, compare: true, delete: false);

    /// <summary>Удаляет ровно одну строку с подтверждённым исходным состоянием.</summary>
    public Task DeleteAsync(DbManager db) => WriteAsync(db, SQLQueries.DELETE_ОборудованиеУчётныеЗаписи, compare: true, delete: true);

    private async Task WriteAsync(DbManager db, string sql, bool compare, bool delete)
    {
        var expected = ExpectedVersion;
        if (compare && string.IsNullOrWhiteSpace(expected))
            throw Conflict();

        var key = new { КодОборудования = EquipmentId, Логин = Login };
        var values = new
        {
            key.КодОборудования,
            key.Логин,
            Пароль = Password,
            ОграниченныеПрава = LimitedRights,
            Примечание = Notes ?? string.Empty,
        };
        object parameters = delete ? key : values;
        try
        {
            await db.RunDbAsync(async connection =>
            {
                var dispatched = false;
                var committing = false;
                try
                {
                    if (!compare)
                    {
                        dispatched = true;
                        return await connection.ExecuteAsync(sql, parameters, commandType: CommandType.Text);
                    }

                    await using var transaction = await connection.BeginTransactionAsync();
                    try
                    {
                        var current = await ReadSnapshotAsync(connection, key.КодОборудования, key.Логин, transaction);
                        if (current is null || !string.Equals(expected, current.CaptureVersion(), StringComparison.Ordinal))
                            throw Conflict();

                        dispatched = true;
                        var affected = await connection.ExecuteScalarAsync<int>(
                            sql, parameters, transaction, commandType: CommandType.Text);
                        if (affected != 1)
                            throw Conflict();

                        committing = true;
                        await transaction.CommitAsync();
                        return affected;
                    }
                    catch
                    {
                        try { await transaction.RollbackAsync(); }
                        catch (Exception error) when (error is DbException or InvalidOperationException)
                        {
                            throw new EquipmentAccountStorageException(dispatched, dispatched);
                        }
                        throw;
                    }
                }
                catch (DbException error)
                {
                    var unknown = dispatched && (committing || error is not SqlException sqlError || DbManager.IsConnectivityError(sqlError));
                    throw new EquipmentAccountStorageException(dispatched, unknown);
                }
            });
        }
        catch (DbException)
        {
            // Ошибка открывания соединения до передачи каких-либо параметров команды.
            throw new EquipmentAccountStorageException(false);
        }
    }

    private static Task<EquipmentAccount?> ReadSnapshotAsync(
        DbConnection connection, int equipmentId, string login, DbTransaction? transaction = null) =>
        connection.QuerySingleOrDefaultAsync<EquipmentAccount>(
            string.Format(CultureInfo.InvariantCulture, SQLQueries.SELECT_ОборудованиеУчётнаяЗаписьСнимок,
                transaction is null ? "" : "WITH (UPDLOCK, HOLDLOCK)"),
            new { EquipmentId = equipmentId, Login = login }, transaction, commandType: CommandType.Text);

    internal string CaptureVersion()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.Unicode, leaveOpen: true);
        writer.Write(EquipmentId);
        writer.Write(Login);
        writer.Write(Password);
        writer.Write(LimitedRights);
        writer.Write(Notes ?? string.Empty);
        var bytes = stream.GetBuffer().AsSpan(0, checked((int)stream.Length));
        try { return Convert.ToHexString(HMACSHA256.HashData(VersionKey, bytes)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static InvalidOperationException Conflict() =>
        new("Исходная версия учётной записи не подтверждена. Введённые данные сохранены в редакторе. Сверьте актуальные данные.");
}

/// <summary>Сбой операции с учётной записью без исходного SQL-исключения или секретных параметров.</summary>
/// <param name="wasDispatched">Команда записи передана провайдеру.</param>
/// <param name="mayHaveWritten">После передачи команды её исход остался неизвестен.</param>
public sealed class EquipmentAccountStorageException(bool wasDispatched, bool mayHaveWritten = false) : InvalidOperationException(mayHaveWritten
    ? "Результат записи учётной записи неизвестен. Перед повторной записью требуется сверка."
    : wasDispatched ? "База данных отклонила запись учётной записи."
        : "Не удалось прочитать данные учётной записи. Команда записи не отправлена.")
{
    /// <summary>Команда записи передана провайдеру.</summary>
    public bool WasDispatched { get; } = wasDispatched;

    /// <summary>Команда могла достичь базы данных и её исход не подтверждён.</summary>
    public bool MayHaveWritten { get; } = mayHaveWritten;
}
