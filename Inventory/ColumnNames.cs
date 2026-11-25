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
    public const string Язык = "Язык";
    public const string КодСотрудника = "КодСотрудника";
    public const string Сотрудник = "Сотрудник";
    public const string Employee = "Employee";
    public const string КодЛицаЗаказчика = "КодЛицаЗаказчика";
    public const string Организация = "Организация";
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
    public const string ПодключенаКамера = "ПодключенаКамера";
    public const string Списано = "Списано";
    public const string КодДокументаСписания = "КодДокументаСписания";
    public const string КодПулаТелефонов = "КодПулаТелефонов";
    public const string КодДокументаПоступления = "КодДокументаПоступления";
    public const string Изменил = "Изменил";
    public const string Изменено = "Изменено";
}

internal static class PhonePoolColumns
{
    public const string КодПулаТелефонов = "КодПулаТелефонов";
    public const string ПулТелефона = "ПулТелефона";
}

internal static class PhoneColumns
{
    public const string КодТелефонногоНомера = "КодТелефонногоНомера";
    public const string Телефон = "Телефон";
    public const string КодТелефоннойСтанции = "КодТелефоннойСтанции";
    public const string XorcomName = "XorcomName";
    public const string Описание = "Описание";
    public const string Основной = "Основной";
    public const string КодОборудованияПодключения = "КодОборудованияПодключения";
    public const string НомерАвтонабора = "НомерАвтонабора";
    public const string ЗадержкаАвтонабора = "ЗадержкаАвтонабора";
    public const string СрокВозврата = "СрокВозврата";
    public const string Примечание = "Примечание";
    public const string Изменил = "Изменил";
    public const string ИзменилФИО = "ИзменилФИО";
    public const string Изменено = "Изменено";
    public const string ТелефонныйНомер = "ТелефонныйНомер";
    public const string ПолныйНомер = "ПолныйНомер";
    public const string КодТипаТелефонныхНомеров = "КодТипаТелефонныхНомеров";
    public const string ТипТелефонныхНомеров = "ТипТелефонныхНомеров";
    public const string ТипТелефонныхНомеровЛат = "ТипТелефонныхНомеровЛат";
    public const string ТелефоннаяСтанция = "ТелефоннаяСтанция";
    public const string КодОборудования = "КодОборудования";
    public const string ПортАТС = "ПортАТС";
    public const string ВедомыйОт = "ВедомыйОт";
    public const string ОтображаемыйОт = "ОтображаемыйОт";
    public const string КодРозетки = "КодРозетки";
    public const string Розетка = "Розетка";
    public const string КодСотрудника = "КодСотрудника";
    public const string КодЛицаЗаказчика = "КодЛицаЗаказчика";
    public const string Подключено = "Подключено";
    public const string ОписаниеАТС = "ОписаниеАТС";
    public const string ОсновнойНомер = "ОсновнойНомер";
    public const string УслугиСвязи = "УслугиСвязи";
    public const string ВСправочнике = "ВСправочнике";
    public const string Группа6 = "Группа6";
    public const string БыстрыйНабор = "БыстрыйНабор";
    public const string Примечания = "Примечания";
    public const string ПараллельныйВызов = "ПараллельныйВызов";
    public const string ОписаниеАТСзаглавными = "ОписаниеАТСзаглавными";
    public const string ВедомыеНомера = "ВедомыеНомера";
    public const string ПодменаОтображаемогоАномера = "ПодменаОтображаемогоАномера";
    public const string ПрефиксСтанции = "ПрефиксСтанции";
    public const string ПрефиксИспользуется = "ПрефиксИспользуется";
    public const string КодТелефонногоНомераГруппы = "КодТелефонногоНомераГруппы";
    public const string КодТелефонногоНомераЧленаГруппы = "КодТелефонногоНомераЧленаГруппы";
    public const string Название = "Название";
    public const string НомерТелефонаПараллельногоВызова = "НомерТелефонаПараллельногоВызова";
    public const string НеИспользовать = "НеИспользовать";
    public const string ЗадержкаПередВызовом = "ЗадержкаПередВызовом";
    public const string ЗадержкаСлишкомБыстрогоОтвета = "ЗадержкаСлишкомБыстрогоОтвета";
    public const string SN = "SN";
    public const string МодельОборудования = "МодельОборудования";
    public const string РасположениеPath = "РасположениеPath";
    public const string КодРасположения = "КодРасположения";
    public const string ЕстьТелефонныйНомер = "ЕстьТелефонныйНомер";
}

internal static class EquipmentPeriodColumns
{
    public const string От = "От";
    public const string До = "До";
    public const string Примечания = "Примечания";
    public const string ИзменилФИО = "ИзменилФИО";
}

internal static class EquipmentAccountColumns
{
    public const string Логин = "Логин";
    public const string Пароль = "Пароль";
    public const string ОграниченныеПрава = "ОграниченныеПрава";
}

internal static class EquipmentComputerColumns
{
    public const string С = "С";
    public const string RAM = "RAM";
    public const string RAMDetail = "RAMDetail";
    public const string HD = "HD";
    public const string HDDetail = "HDDetail";
    public const string SSD = "SSD";
    public const string SSDDetail = "SSDDetail";
    public const string КодСтандартнойКонфигурации1 = "КодСтандартнойКонфигурации1";
    public const string ДатаСтандартнойКонфигурации1 = "ДатаСтандартнойКонфигурации1";
    public const string КодСтандартнойКонфигурации2 = "КодСтандартнойКонфигурации2";
    public const string ДатаСтандартнойКонфигурации2 = "ДатаСтандартнойКонфигурации2";
    public const string Лицензия = "Лицензия";
    public const string Конфигурация1 = "Конфигурация1";
    public const string Конфигурация2 = "Конфигурация2";
}

internal static class StandardConfigurationColumns
{
    public const string КодСтандартнойКонфигурации = "КодСтандартнойКонфигурации";
    public const string СтандартнаяКонфигурация = "СтандартнаяКонфигурация";
}

internal static class MobileContractColumns
{
    public const string КодДокумента = "КодДокумента";
    public const string ДоговорНазвание = "ДоговорНазвание";
    public const string КодЛицаЗаказчика = "КодЛицаЗаказчика";
    public const string ТипЛицаЗаказчика = "ТипЛицаЗаказчика";
}

internal static class SimCardColumns
{
    public const string НомерТелефона = "НомерТелефона";
    public const string КодДоговора = "КодДоговора";
    public const string КодЛицаИсполнителя = "КодЛицаИсполнителя";
    public const string КодЛицаПерсональныхДанных = "КодЛицаПерсональныхДанных";
    public const string Договор = "Договор";
    public const string ЛицоПерсональныхДанных = "ЛицоПерсональныхДанных";
    public const string Подчинённый = "Подчинённый";
    public const string НДСНаСумму = "НДСНаСумму";
    public const string НДСВТарификации = "НДСВТарификации";
    public const string СтавкаНДС = "СтавкаНДС";
    public const string ОкруглениеСуммы = "ОкруглениеСуммы";
}

internal static class SimServiceColumns
{
    public const string КодSIMКартыДопУслугиСотОператора = "КодSIMКартыДопУслугиСотОператора";
    public const string КодТипаДопУслугиСотОператора = "КодТипаДопУслугиСотОператора";
    public const string ДопУслугаСотОператора = "ДопУслугаСотОператора";
    public const string СтоимостьВключения = "СтоимостьВключения";
    public const string СтоимостьВдень = "СтоимостьВдень";
    public const string СтоимостьВмесяц = "СтоимостьВмесяц";
    public const string КодОбязательнойУслугиДляКомпании = "КодОбязательнойУслугиДляКомпании";
    public const string УслугуНеобходимоОтключить = "УслугуНеобходимоОтключить";
    public const string ОбязательнаяУслуга = "ОбязательнаяУслуга";
    public const string ОтПлан = "ОтПлан";
    public const string ДоПлан = "ДоПлан";
    public const string ТарифныйПлан = "ТарифныйПлан";
}

internal static class LicenseColumns
{
    public const string КодТипаЛицензирования = "КодТипаЛицензирования";
    public const string ТипЛицензирования = "ТипЛицензирования";
    public const string Наименование = "Наименование";
    public const string Временная = "Временная";
    public const string Перепродажа = "Перепродажа";
    public const string НомерЛицензиата = "НомерЛицензиата";
    public const string URLПроверкиЛицензии = "URLПроверкиЛицензии";
    public const string КоличествоСерверов = "КоличествоСерверов";
    public const string КоличествоКлиентов = "КоличествоКлиентов";
    public const string ДействуетОт = "ДействуетОт";
    public const string ДействуетДо = "ДействуетДо";
    public const string Дистрибутив = "Дистрибутив";
}

internal static class SoftwareInstallationColumns
{
    public const string КодОборудованияПО = "КодОборудованияПО";
    public const string КодПО = "КодПО";
    public const string Установлено = "Установлено";
    public const string Дистрибутив = "Дистрибутив";
    public const string Деинсталировано = "Деинсталировано";
    public const string ОтображаемоеНазвание = "ОтображаемоеНазвание";
    public const string НазваниеПО = "НазваниеПО";
}

internal static class AccountingColumns
{
    public const string КодОборудованияБухУчета = "КодОборудованияБухУчета";
    public const string КодБухгалтерии = "КодБухгалтерии";
    public const string Бухгалтерия = "Бухгалтерия";
    public const string Арендатор = "Арендатор";
    public const string Материал = "Материал";
    public const string ИнвентарныйНомер = "ИнвентарныйНомер";
    public const string ДатаПоступления = "ДатаПоступления";
    public const string ДатаСписания = "ДатаСписания";
}

internal static class RouterColumns
{
    public const string СетевоеИмя = "СетевоеИмя";
    public const string ГолосовойШлюз = "ГолосовойШлюз";
    public const string OSVersion = "OSVersion";
    public const string ProcRAM = "ProcRAM";
    public const string ProcRAMfree = "ProcRAMfree";
    public const string Flash = "Flash";
    public const string FlashFree = "FlashFree";
    public const string BGP = "BGP";
    public const string RouterID = "RouterID";
    public const string Description = "Description";
    public const string Код = "Код";
    public const string Модуль = "Модуль";
    public const string Характеристика = "Характеристика";
}

internal static class UpsColumns
{
    public const string КодОборудованияUPS = "КодОборудованияUPS";
    public const string КодОборудованияСервера = "КодОборудованияСервера";
    public const string НастроеноВыключение = "НастроеноВыключение";
    public const string НазваниеОборудования = "НазваниеОборудования";
    public const string НазваниеСервера = "НазваниеСервера";
    public const string КодТестирования = "КодТестирования";
    public const string ДатаТестирования = "ДатаТестирования";
    public const string ВремяБатарейUPS = "ВремяБатарейUPS";
    public const string СервераПолучившиеОповещение = "СервераПолучившиеОповещение";
    public const string ОшибкиТестирования = "ОшибкиТестирования";
}

internal static class ActualInventoryColumns
{
    public const string ВремяПроверки = "ВремяПроверки";
    public const string Оборудование = "Оборудование";
    public const string КодРасположения = "КодРасположения";
    public const string СломаноРемонтуНеПодлежит = "СломаноРемонтуНеПодлежит";
    public const string Примечание = "Примечание";
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

internal static class LocalNetworkPortColumns
{
    public const string КодЛокальнойСети = "КодЛокальнойСети";
    public const string КодОборудования = "КодОборудования";
    public const string Порт = "Порт";
    public const string PoE = "PoE";
    public const string КодРозетки = "КодРозетки";
    public const string КодЛицаЗаказчика = "КодЛицаЗаказчика";
    public const string КодСотрудника = "КодСотрудника";
    public const string Подключено = "Подключено";
    public const string Примечания = "Примечания";
    public const string СрокВозврата = "СрокВозврата";
    public const string Изменил = "Изменил";
    public const string Изменено = "Изменено";
    public const string Строка = "Строка";
}

internal static class NetSocketColumns
{
    public const string КодРозетки = "КодРозетки";
    public const string КодРасположения = "КодРасположения";
    public const string Розетка = "Розетка";
    public const string Работает = "Работает";
    public const string Примечание = "Примечание";
}
