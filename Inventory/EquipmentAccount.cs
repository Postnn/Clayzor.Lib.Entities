using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using Clayzor.Lib.DALC;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Учётная запись оборудования без пароля в общем списке.</summary>
public sealed class EquipmentAccount
{
    /// <summary>Идентификатор оборудования.</summary>
    [Column(EquipmentColumns.КодОборудования)]
    public int EquipmentId { get; set; }

    /// <summary>Логин, часть составного ключа.</summary>
    [Column(EquipmentAccountColumns.Логин)]
    public string Login { get; set; } = string.Empty;

    /// <summary>Пароль, загружаемый только отдельным авторизованным запросом.</summary>
    [Column(EquipmentAccountColumns.Пароль)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Признак ограниченных прав (0 или 1).</summary>
    [Column(EquipmentAccountColumns.ОграниченныеПрава)]
    public byte LimitedRights { get; set; }

    /// <summary>Примечание к учётной записи.</summary>
    [Column(PhoneColumns.Примечание)]
    public string Notes { get; set; } = string.Empty;

    /// <summary>Добавляет учётную запись.</summary>
    public Task InsertAsync(DbManager db) => db.ExecuteAsync(SQLQueries.INSERT_ОборудованиеУчётныеЗаписи, new
    {
        КодОборудования = EquipmentId,
        Логин = Login,
        Пароль = Password,
        ОграниченныеПрава = LimitedRights,
        Примечание = Notes ?? string.Empty,
    }, commandType: CommandType.Text);

    /// <summary>Обновляет пароль, права и примечание по составному ключу.</summary>
    public Task UpdateAsync(DbManager db) => db.ExecuteAsync(SQLQueries.UPDATE_ОборудованиеУчётныеЗаписи, new
    {
        КодОборудования = EquipmentId,
        Логин = Login,
        Пароль = Password,
        ОграниченныеПрава = LimitedRights,
        Примечание = Notes ?? string.Empty,
    }, commandType: CommandType.Text);

    /// <summary>Удаляет учётную запись по составному ключу.</summary>
    public Task DeleteAsync(DbManager db) => db.ExecuteAsync(SQLQueries.DELETE_ОборудованиеУчётныеЗаписи, new
    {
        КодОборудования = EquipmentId,
        Логин = Login,
    }, commandType: CommandType.Text);
}
