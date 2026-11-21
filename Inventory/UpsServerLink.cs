using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using Clayzor.Lib.DALC;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Связь сервера с UPS.</summary>
public sealed class UpsServerLink
{
    /// <summary>Идентификатор UPS.</summary>
    [Column(UpsColumns.КодОборудованияUPS)]
    public int UpsEquipmentId { get; set; }

    /// <summary>Идентификатор сервера.</summary>
    [Column(UpsColumns.КодОборудованияСервера)]
    public int ServerEquipmentId { get; set; }

    /// <summary>Признак настроенного выключения.</summary>
    [Column(UpsColumns.НастроеноВыключение)]
    public byte TurnOffConfigured { get; set; }

    /// <summary>Название сервера.</summary>
    [Column(UpsColumns.НазваниеСервера)]
    public string ServerName { get; set; } = string.Empty;

    /// <summary>Тип оборудования сервера; выбирать уведомления могут только компьютеры.</summary>
    [Column(EquipmentTypeColumns.КодТипаОборудования)]
    public int EquipmentTypeId { get; set; }

    /// <summary>Название связанного оборудования.</summary>
    [Column(UpsColumns.НазваниеОборудования)]
    public string EquipmentName { get; set; } = string.Empty;

    /// <summary>Проверяет актуальные права и сохраняет diff серверов и тестов одной транзакцией.</summary>
    public static async Task SaveDetailsAsync(
        DbManager db,
        int upsEquipmentId,
        int sourceServerEquipmentId,
        IReadOnlyCollection<UpsServerLink> originalServers,
        IReadOnlyCollection<UpsServerLink> currentServers,
        IReadOnlyCollection<UpsTest> originalTests,
        IReadOnlyCollection<UpsTest> currentTests)
    {
        var command = UpsSaveBuilder.Build(
            upsEquipmentId, sourceServerEquipmentId, originalServers, currentServers, originalTests, currentTests);
        var status = await db.ExecuteScalarAsync<int>(command.Sql, command.Parameters, commandType: CommandType.Text);
        if (status == 1 || status == 3)
            throw new UnauthorizedAccessException("Недостаточно прав для изменения подключений UPS.");
        if (status == 2)
            throw new InvalidOperationException("Подключение UPS изменилось. Обновите данные и повторите операцию.");
    }
}
