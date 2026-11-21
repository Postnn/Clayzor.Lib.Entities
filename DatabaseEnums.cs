namespace Clayzor.Lib.Entities;

/// <summary>Фиксированные записи таблицы «ТипыОборудования», колонка «КодТипаОборудования».</summary>
public enum ТипыОборудования
{
    /// <summary>Компьютер.</summary>
    Компьютер = 1,
    /// <summary>SIM-карта.</summary>
    SIM_карта = 27,
}

/// <summary>Фиксированные записи таблицы «ТипыТелефонныхНомеров», колонка «КодТипаТелефонныхНомеров».</summary>
public enum ТипыТелефонныхНомеров
{
    /// <summary>PBX группа / Фиктивный.</summary>
    ГруппаИлиФиктивный = 0,
    /// <summary>Цифровой.</summary>
    Цифровой = 1,
    /// <summary>Аналоговый.</summary>
    Аналоговый = 2,
    /// <summary>ISDN.</summary>
    ISDN = 3,
    /// <summary>DECT.</summary>
    DECT = 10,
    /// <summary>Короткий номер.</summary>
    Короткий_номер = 100,
    /// <summary>Короткий номер мобильного.</summary>
    Короткий_номер_мобильного = 101,
    /// <summary>IP.</summary>
    IP = 200,
}

/// <summary>Фиксированные записи таблицы «ТелефонныеСтанции», колонка «КодТелефоннойСтанции».</summary>
public enum ТелефонныеСтанции
{
    /// <summary>Cisco Call Manager.</summary>
    CCM = 0,
    /// <summary>Meridian 1 11C.</summary>
    Meridian = 32,
    /// <summary>sip-hkrd.</summary>
    Xorcom_Krd = 33,
    /// <summary>sip-hnov.</summary>
    Xorcom_Nov = 37,
}

/// <summary>Фиксированные записи таблицы «Роли», колонка «КодРоли».</summary>
public enum Роли
{
    /// <summary>Ведёт учёт оборудования в Инвентаризации.</summary>
    Специалист_по_инвентаризации_оборудования = 43,
}
