namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Читает только ключи и полномочия, необходимые для присутствия в редакторах оборудования.</summary>
internal static class EquipmentChildPresenceQueries
{
    internal const string ComputerPresenceRow = """
        SELECT 1 FROM Компьютеры WHERE КодОборудования = @EquipmentId AND С = @Date
        """;
    internal const string LocationPresenceRow = """
        SELECT 1 FROM ОборудованиеРасположение WHERE КодОборудования = @EquipmentId AND От = @FromUtc
        """;
    internal const string ResponsiblePresenceRow = """
        SELECT 1 FROM ОборудованиеСотрудников
        WHERE КодОборудования = @EquipmentId AND От = @FromUtc AND (@RequireActive = 0 OR До IS NULL)
        """;
    internal const string SoftwarePresenceRow = """
        SELECT 1 FROM vwОборудованиеПО WHERE КодОборудования = @EquipmentId AND КодОборудованияПО = @Id
        """;
    internal const string AccountingPresenceOwner = """
        SELECT КодЛица FROM ОборудованиеБухУчета
        WHERE КодОборудования = @EquipmentId AND КодОборудованияБухУчета = @Id
        """;
    internal const string LicensePresenceRow = """
        SELECT 1 FROM vwSoft S
        INNER JOIN ТипыЛицензирования T ON T.КодТипаЛицензирования = S.КодТипаЛицензирования
        WHERE S.КодОборудования = @EquipmentId
        """;

    internal const string SimPresenceRow = """
        SELECT 1 FROM vwSIMКарты WHERE КодОборудования = @EquipmentId
        """;

    internal const string SimServicePresenceRow = """
        SELECT 1 FROM vwSIMКартыДопУслугиСотОператора U
        WHERE U.КодSIMКартыДопУслугиСотОператора = @Id AND EXISTS (
            SELECT 1 FROM vwSIMКарты S
            WHERE S.КодОборудования = @EquipmentId AND S.НомерТелефона = U.НомерТелефона
        )
        """;

    internal const string UpsLinkPresenceRow = """
        SELECT 1 FROM vwПодключенияUPS
        WHERE КодОборудованияUPS = @UpsEquipmentId AND КодОборудованияСервера = @ServerEquipmentId
        """;

    internal const string UpsTestPresenceRow = """
        SELECT 1 FROM vwПодключенияUPSтестирование
        WHERE КодОборудованияUPS = @UpsEquipmentId AND КодТестирования = @Id
        """;

    internal const string AccountPresenceLogin = """
        SELECT Логин FROM vwОборудованиеУчётныеЗаписи
        WHERE КодОборудования = @EquipmentId AND Логин = @Login
        """;
}
