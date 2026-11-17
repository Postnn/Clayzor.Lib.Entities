namespace Clayzor.Lib.Entities.Inventory;

internal static class EquipmentTypeColumns
{
    public const string КодТипаОборудования = "КодТипаОборудования";
    public const string ТипОборудования = "ТипОборудования";
    public const string ТипОборудованияЛат = "ТипОборудованияЛат";
    public const string SNобязателен = "SNобязателен";
    public const string БезТочногоРасположения = "БезТочногоРасположения";
    public const string ЕстьХарактеристикиКомпьютера = "ЕстьХарактеристикиКомпьютера";
    public const string ЕстьХарактеристикиМонитора = "ЕстьХарактеристикиМонитора";
    public const string ЕстьХарактеристикиSIM = "ЕстьХарактеристикиSIM";
    public const string ЕстьХарактеристикиSoft = "ЕстьХарактеристикиSoft";
    public const string ЕстьТелефонныйНомер = "ЕстьТелефонныйНомер";
    public const string ЕстьMACадрес = "ЕстьMACадрес";
    public const string ЕстьСетевоеИмя = "ЕстьСетевоеИмя";
}

internal static class EquipmentModelColumns
{
    public const string КодМоделиОборудования = "КодМоделиОборудования";
    public const string МодельОборудования = "МодельОборудования";
    public const string КодТипаОборудования = "КодТипаОборудования";
    public const string КодРесурса = "КодРесурса";
    public const string PN = "PN";
    public const string CPU = "CPU";
    public const string РазмерМонитора = "РазмерМонитора";
    public const string КодТипаПамяти = "КодТипаПамяти";
    public const string БанковПамяти = "БанковПамяти";
    public const string Мультимедиа = "Мультимедиа";
    public const string Net = "Net";
    public const string НазваниеNet = "НазваниеNet";
    public const string Драйвер = "Драйвер";
    public const string Ресурс = "Ресурс";
    public const string ШаблонТелефона = "ШаблонТелефона";
    public const string Примечания = "Примечания";
    public const string Изменил = "Изменил";
    public const string Изменено = "Изменено";
}

internal static class ResourceColumns
{
    public const string КодРесурса = "КодРесурса";
    public const string РесурсРус = "РесурсРус";
}

internal static class MemoryTypeColumns
{
    public const string КодТипаПамяти = "КодТипаПамяти";
    public const string ТипПамяти = "ТипПамяти";
}

internal static class EmployeeColumns
{
    public const string КодСотрудника = "КодСотрудника";
    public const string Сотрудник = "Сотрудник";
    public const string Employee = "Employee";
}

internal static class EquipmentColumns
{
    public const string КодОборудования = "КодОборудования";
    public const string SN = "SN";
    public const string MACадрес = "MACадрес";
    public const string MACадрес2 = "MACадрес2";
    public const string MACадресILO = "MACадресILO";
    public const string СетевоеИмя = "СетевоеИмя";
    public const string КодЛицаВладельца = "КодЛицаВладельца";
    public const string КодЛицаАрендатора = "КодЛицаАрендатора";
    public const string КодЛицаПоставщика = "КодЛицаПоставщика";
    public const string Примечания = "Примечания";
}

internal static class PersonLookupColumns
{
    public const string КодЛица = "КодЛица";
    public const string Кличка = "Кличка";
}

internal static class DocumentLookupColumns
{
    public const string КодДокумента = "КодДокумента";
    public const string НазваниеДокумента = "НазваниеДокумента";
    public const string НомерДокумента = "НомерДокумента";
    public const string ДатаДокумента = "ДатаДокумента";
}

internal static class LocationLookupColumns
{
    public const string КодРасположения = "КодРасположения";
    public const string РасположениеPath1 = "РасположениеPath1";
}
