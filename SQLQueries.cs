namespace Clayzor.Lib.Entities;

/// <summary>
/// Именованные SQL-константы. Каждая содержит полный текст запроса с -- комментариями.
/// Именование: SELECT_{Данные}, INSERT_{Таблица}, UPDATE_{Таблица}, DELETE_{Таблица}, SP_{Имя}, FN_{Имя}.
/// </summary>
public static class SQLQueries
{
    /// <summary>Подтверждённая процедура синхронизации списков Safe/Blocked.</summary>
    internal const string SP_SlBlSynchronize = "dbo.sp_MailboxesSlBlSynchronize";

    /// <summary>Проверяет существование процедуры, EXECUTE и три совместимых параметра.</summary>
    internal const string SELECT_SlBlProcedureAvailable = """
        SELECT CONVERT(bit,CASE WHEN OBJECT_ID(@Name,'P') IS NOT NULL
          AND HAS_PERMS_BY_NAME(@Name,'OBJECT','EXECUTE')=1
          AND (SELECT COUNT(*) FROM sys.parameters WHERE object_id=OBJECT_ID(@Name))=3
          AND (SELECT COUNT(*) FROM sys.parameters WHERE object_id=OBJECT_ID(@Name) AND is_output=0 AND
               ((name IN ('@AddressSet','@SlBl') AND TYPE_NAME(system_type_id) IN ('varchar','nvarchar'))
                OR (name='@BlockSet' AND TYPE_NAME(system_type_id) IN ('bit','tinyint','int'))))=3
          THEN 1 ELSE 0 END)
        """;

    /// <summary>ФИО сотрудников для страницы отчёта по уникальным кодам авторов.</summary>
    internal const string SELECT_ModificationAuthors = "SELECT КодСотрудника AS Id, ФИО AS Name FROM dbo.Сотрудники WHERE КодСотрудника IN @Ids";

    #region NumPool

    internal const string SELECT_NumPoolAccess = """
        SELECT CONVERT(bit,ISNULL(HAS_PERMS_BY_NAME(N'dbo.vwНомераТфОП','OBJECT','SELECT'),0)) AS CanRead,
               CONVERT(bit,ISNULL(HAS_PERMS_BY_NAME(N'dbo.vwНомераТфОП','OBJECT','INSERT'),0)) AS CanInsert,
               CONVERT(bit,ISNULL(HAS_PERMS_BY_NAME(N'dbo.vwНомераТфОП','OBJECT','UPDATE'),0)) AS CanUpdate,
               CONVERT(bit,ISNULL(HAS_PERMS_BY_NAME(N'dbo.vwНомераТфОП','OBJECT','DELETE'),0)) AS CanDelete;
        """;

    internal const string SELECT_NumPoolHeader = """
        SELECT n.НомерТфОП AS Number,n.КодНомернойЁмкости AS CapacityId,n.ВходящийДля AS IncomingFor,
          n.ВходящийНаНомер AS IncomingTo,n.Примечания AS Notes,CONVERT(decimal(5,0),n.Контроль) AS [Control],
          n.Изменил AS ChangedBy,s.ФИО AS ChangedByName,n.Изменено AS ChangedAt
        FROM dbo.vwНомераТфОП n {0}
        LEFT JOIN dbo.Сотрудники s ON s.КодСотрудника=n.Изменил WHERE n.НомерТфОП=@Id;
        """;

    internal const string SELECT_NumPoolConnections = """
        SELECT p.КодПодключенияТелефонии AS Id,c.ПодключениеТелефонии+' '+ISNULL(c.Интерфейс,'') AS Name,CONVERT(tinyint,0) AS Private,
          p.Изменил AS ChangedBy,ISNULL(s.ФИО,'#'+CONVERT(varchar,p.Изменил)) AS ChangedByName,p.Изменено AS ChangedAt
        FROM dbo.vwПодключенияТелефонииНомераТфОП p
        JOIN dbo.vwПодключенияТелефонии c ON c.КодПодключенияТелефонии=p.КодПодключенияТелефонии
        LEFT JOIN dbo.Сотрудники s ON s.КодСотрудника=p.Изменил WHERE p.НомерТфОП=@Id ORDER BY Name,Id;
        """;

    internal const string SELECT_NumPoolEmployees = """
        SELECT p.КодСотрудника AS Id,e.Сотрудник AS Name,p.Private,
          p.Изменил AS ChangedBy,ISNULL(s.ФИО,'#'+CONVERT(varchar,p.Изменил)) AS ChangedByName,p.Изменено AS ChangedAt
        FROM dbo.vwНомераТфОП_Сотрудники p JOIN dbo.Сотрудники e ON e.КодСотрудника=p.КодСотрудника
        LEFT JOIN dbo.Сотрудники s ON s.КодСотрудника=p.Изменил WHERE p.НомерТфОП=@Id ORDER BY Name,Id;
        """;

    internal const string SELECT_NumPoolCustomers = """
        SELECT p.КодЛица AS Id,c.Кличка AS Name,CONVERT(tinyint,0) AS Private,
          p.Изменил AS ChangedBy,ISNULL(s.ФИО,'#'+CONVERT(varchar,p.Изменил)) AS ChangedByName,p.Изменено AS ChangedAt
        FROM dbo.vwНомераТфОП_Лица p JOIN dbo.ЛицаЗаказчики c ON c.КодЛица=p.КодЛица
        LEFT JOIN dbo.Сотрудники s ON s.КодСотрудника=p.Изменил WHERE p.НомерТфОП=@Id ORDER BY Name,Id;
        """;

    internal const string NumPoolPhoneSubscriber = "ISNULL(ts.ФИО+ISNULL(' ('+NULLIF(t.Подключено,'')+')',''),t.Подключено)";

    internal const string SELECT_NumPoolPhones = """
        SELECT p.КодТелефонногоНомера AS Id,ISNULL(t.ПолныйНомер+' '+
        """ + NumPoolPhoneSubscriber + """
        ,'#'+CONVERT(varchar,p.КодТелефонногоНомера)) AS Name,CONVERT(tinyint,0) AS Private,
          p.Изменил AS ChangedBy,ISNULL(s.ФИО,'#'+CONVERT(varchar,p.Изменил)) AS ChangedByName,p.Изменено AS ChangedAt
        FROM dbo.vwНомераТфОП_Телефоны p LEFT JOIN dbo.vwТелефонныеНомераИзменение t ON t.КодТелефонногоНомера=p.КодТелефонногоНомера
        LEFT JOIN dbo.Сотрудники ts ON ts.КодСотрудника=t.КодСотрудника
        LEFT JOIN dbo.Сотрудники s ON s.КодСотрудника=p.Изменил WHERE p.НомерТфОП=@Id ORDER BY Name,Id;
        """;

    internal const string INSERT_NumPoolHeader = """
        INSERT dbo.vwНомераТфОП (НомерТфОП,КодНомернойЁмкости,ВходящийДля,ВходящийНаНомер,Примечания,Контроль)
        VALUES (@Id,@CapacityId,@IncomingFor,@IncomingTo,@Notes,@Control);
        """;
    internal const string UPDATE_NumPoolHeader = """
        UPDATE dbo.vwНомераТфОП SET ВходящийДля=@IncomingFor,ВходящийНаНомер=@IncomingTo,Примечания=@Notes,Контроль=@Control
        WHERE НомерТфОП=@Id;
        """;
    internal const string DELETE_NumPoolHeader = "DELETE dbo.vwНомераТфОП WHERE НомерТфОП=@Id;";
    internal const string INSERT_NumPoolLinks = "INSERT dbo.{0} (НомерТфОП,{1}{2}) VALUES {3};";
    internal const string DELETE_NumPoolLinks = "DELETE dbo.{0} WHERE НомерТфОП=@Id AND {1} IN @Keys;";
    internal const string DELETE_NumPoolAllLinks = "DELETE dbo.{0} WHERE НомерТфОП=@Id;";

    internal const string NumPoolCapacitySource = "SELECT КодНомернойЁмкости AS Id,НомернаяЁмкость AS Name FROM dbo.vwНомерныеЁмкостиСписок";
    internal const string NumPoolConnectionSource = "SELECT КодПодключенияТелефонии AS Id,ПодключениеТелефонии+' '+ISNULL(Интерфейс,'') AS Name FROM dbo.vwПодключенияТелефонии";
    internal const string NumPoolCustomerSource = "SELECT КодЛица AS Id,Кличка AS Name FROM dbo.ЛицаЗаказчики";
    internal const string NumPoolEmployeeSource = "SELECT КодСотрудника AS Id,Сотрудник AS Name FROM dbo.Сотрудники WHERE Состояние=0";
    internal const string NumPoolPhoneSource = """
        SELECT t.КодТелефонногоНомера AS Id,ISNULL(t.ПолныйНомер+ISNULL('['+NULLIF(t.ТелефонныйНомер,t.ПолныйНомер)+']','')+' '+
        """ + NumPoolPhoneSubscriber + """
        ,'#'+CONVERT(varchar,t.КодТелефонногоНомера)) AS Name
        FROM dbo.vwТелефонныеНомераИзменение t LEFT JOIN dbo.Сотрудники ts ON ts.КодСотрудника=t.КодСотрудника
        """;
    internal const string SELECT_NumPoolOptions = "SELECT TOP (@Limit) Id,Name FROM ({0}) x WHERE (@Id IS NULL OR Id=@Id) AND Name LIKE @Pattern ORDER BY Name,Id;";
    internal const string SELECT_NumPoolLinkKeys = "SELECT Id FROM ({0}) x WHERE Id IN @Keys;";

    #endregion

    /// <summary>Пригодное рабочее место и признак склада для инвентаризации.</summary>
    public const string SELECT_LocationFactSite = """
        SELECT КодРасположения AS Id, РасположениеPath1 AS Name, CONVERT(bit,CASE WHEN РабочееМесто=4 THEN 1 ELSE 0 END) AS IsWarehouse
        FROM dbo.vwРасположения {0}
        WHERE КодРасположения=@LocationId AND РабочееМесто IN (1,2,3,4,5) AND Закрыто=0;
        """;

    /// <summary>Инвентаризация рабочего места: учётное местоположение и последний факт.</summary>
    public const string SELECT_LocationFactRows = """
        WITH Booked AS (
            SELECT e.КодОборудования AS Id, r.От AS FromUtc,
                   t.ТипОборудования + ISNULL(' '+NULLIF(e.СетевоеИмя,''),'')
                   + ISNULL(' '+NULLIF(m.МодельОборудования,''),'') + ISNULL(' PN:'+NULLIF(m.PN,''),'')
                   + ISNULL(' SN:'+NULLIF(e.SN,''),'') + ISNULL(' '+NULLIF(sim.НомерТелефона,''),'')
                   + ISNULL(' ('+NULLIF(e.Примечания,'')+')','') AS Name
            FROM dbo.ОборудованиеРасположение r
            JOIN dbo.vwОборудование e ON e.КодОборудования=r.КодОборудования AND e.Списано IS NULL
            JOIN dbo.МоделиОборудования m ON m.КодМоделиОборудования=e.КодМоделиОборудования
            JOIN dbo.ТипыОборудования t ON t.КодТипаОборудования=m.КодТипаОборудования
            OUTER APPLY (SELECT TOP (1) s.НомерТелефона FROM dbo.vwSIMКарты s
                         WHERE s.КодОборудования=e.КодОборудования ORDER BY s.НомерТелефона) sim
            WHERE r.КодРасположения=@LocationId AND (@ItOnly=0 OR m.КодТипаОборудования<100)
              AND NOT EXISTS (SELECT 1 FROM dbo.ОборудованиеРасположение x WHERE x.КодОборудования=r.КодОборудования AND x.От>r.От)
        ), Observed AS (
            SELECT a.КодОборудования AS Id, a.Оборудование AS Name, a.ВремяПроверки AS CheckedAt,
                   a.КодРасположения AS LocationId, l.РасположениеPath1 AS LocationName,
                   CONVERT(bit,a.СломаноРемонтуНеПодлежит) AS Broken, a.Примечание AS Notes
            FROM dbo.vwРеальноеРасположениеОборудования a
            JOIN dbo.vwРасположения l ON l.КодРасположения=a.КодРасположения
            JOIN dbo.vwОборудование e ON e.КодОборудования=a.КодОборудования AND e.Списано IS NULL
            JOIN dbo.МоделиОборудования m ON m.КодМоделиОборудования=e.КодМоделиОборудования
            WHERE a.КодРасположения=@LocationId AND (@ItOnly=0 OR m.КодТипаОборудования<100)
              AND NOT EXISTS (SELECT 1 FROM dbo.vwРеальноеРасположениеОборудования x WHERE x.КодОборудования=a.КодОборудования AND x.ВремяПроверки>a.ВремяПроверки)
              AND NOT EXISTS (SELECT 1 FROM dbo.ОборудованиеРасположение r WHERE r.КодОборудования=a.КодОборудования AND r.От>a.ВремяПроверки)
        )
        SELECT COALESCE(b.Id,a.Id) AS Id, COALESCE(b.Name,a.Name) AS Name, a.CheckedAt, a.LocationId, a.LocationName,
               ISNULL(a.Broken,0) AS Broken, ISNULL(a.Notes,'') AS Notes
        FROM Booked b FULL JOIN Observed a ON a.Id=b.Id
        WHERE (@ResponsibleId IS NULL OR EXISTS (SELECT 1 FROM dbo.vwОборудованиеСписок x WHERE x.КодОборудования=b.Id AND x.КодСотрудника=@ResponsibleId))
        ORDER BY COALESCE(b.Name,a.Name), CASE WHEN b.FromUtc<a.CheckedAt THEN a.CheckedAt END, COALESCE(b.Id,a.Id);
        """;

    /// <summary>Поиск по штатной функции, с IT-фильтром для конкретной найденной строки.</summary>
    public const string SELECT_LocationFactEquipment = """
        SELECT TOP (@Limit) f.КодОборудования AS Id, f.Найдено AS MatchText, f.Оборудование AS Name,
               f.SN AS SerialNumber, f.Владелец AS OwnerName, f.ИнвентарныйНомер AS InventoryNumber
        FROM dbo.fn_ПоискОборудования(@Search) f
        JOIN dbo.vwОборудование e ON e.КодОборудования=f.КодОборудования AND e.Списано IS NULL
        JOIN dbo.МоделиОборудования m ON m.КодМоделиОборудования=e.КодМоделиОборудования
        WHERE (@ItOnly=0 OR m.КодТипаОборудования<100) AND (@EquipmentId IS NULL OR e.КодОборудования=@EquipmentId)
        ORDER BY f.КодОборудования;
        """;

    /// <summary>Контекст подтверждения. Подсказка блокировки используется только при записи.</summary>
    public const string SELECT_LocationFactContext = """
        SELECT e.КодОборудования AS EquipmentId, e.КодЛицаВладельца AS OwnerId, ISNULL(o.Кличка,'') AS OwnerName,
               ISNULL(NULLIF(e.СетевоеИмя,'')+' ','')+ISNULL(m.МодельОборудования,'')+ISNULL(' SN: '+NULLIF(e.SN,''),'') AS FactName,
               ISNULL(b.ИнвентарныйНомер,'') AS InventoryNumber, r.КодРасположения AS BookedLocationId,
               ISNULL(l.РасположениеPath1,'') AS BookedLocationName, r.От AS BookedFromUtc, e.Изменено AS EquipmentChangedAt
        FROM dbo.vwОборудование e {0}
        JOIN dbo.МоделиОборудования m ON m.КодМоделиОборудования=e.КодМоделиОборудования
        LEFT JOIN dbo.ЛицаЗаказчики o ON o.КодЛица=e.КодЛицаВладельца
        OUTER APPLY (SELECT TOP (1) r.КодРасположения,r.От FROM dbo.ОборудованиеРасположение r {0}
                     WHERE r.КодОборудования=e.КодОборудования ORDER BY r.От DESC) r
        LEFT JOIN dbo.vwРасположения l ON l.КодРасположения=r.КодРасположения
        OUTER APPLY (SELECT TOP (1) b.ИнвентарныйНомер FROM dbo.ОборудованиеБухУчета b
                     WHERE b.КодОборудования=e.КодОборудования AND b.До IS NULL ORDER BY b.От DESC,b.КодОборудованияБухУчета DESC) b
        WHERE e.КодОборудования=@EquipmentId AND e.Списано IS NULL;
        """;

    /// <summary>Штатная запись проверки и контроль результата через то же представление.</summary>
    public const string INSERT_LocationFact = """
        INSERT dbo.vwРеальноеРасположениеОборудования(ВремяПроверки,КодОборудования,Оборудование,КодРасположения,СломаноРемонтуНеПодлежит,Примечание)
        VALUES(@CheckedAt,@EquipmentId,@FactName,@LocationId,@Broken,@Notes);
        SELECT COUNT(*) FROM dbo.vwРеальноеРасположениеОборудования
        WHERE КодОборудования=@EquipmentId AND КодРасположения=@LocationId AND ВремяПроверки=@CheckedAt;
        """;

    /// <summary>Фактические права SQL и время для двух записей одной операции.</summary>
    public const string SELECT_LocationFactWriteAccess = """
        SELECT GETUTCDATE() AS NowUtc,
               CONVERT(bit,ISNULL(HAS_PERMS_BY_NAME(N'dbo.vwРеальноеРасположениеОборудования','OBJECT','INSERT'),0)) AS CanConfirm,
               CONVERT(bit,ISNULL(HAS_PERMS_BY_NAME(N'dbo.ОборудованиеРасположение','OBJECT','INSERT'),0)) AS CanMove;
        """;

    /// <summary>Определения доступных пользователю разделов отчёта.</summary>
    public const string SELECT_ModificationReports = """
        SELECT КодЗапроса AS Id, Запрос AS Name, [SQL] AS [Sql], Форма AS FormReference
        FROM dbo.ЗапросыИзмененияБД ORDER BY Запрос, КодЗапроса;
        """;

    /// <summary>Схема SQL-определения отчёта, включая пустой результат.</summary>
    public const string SELECT_ModificationReportSchema = "SELECT TOP (0) * FROM ({0}) AS p";

    /// <summary>Фильтрация отчёта по календарным датам хранения и автору.</summary>
    public const string SELECT_ModificationReportFiltered = """
        SELECT p.* FROM ({0}) AS p
        WHERE CONVERT(date, p.[Изменено]) BETWEEN @DateFrom AND @DateTo
          AND (@EmployeeId IS NULL OR p.[Изменил]=@EmployeeId)
        """;

    /// <summary>Количество строк и одна страница раздела отчёта.</summary>
    public const string SELECT_ModificationReportPage = """
        SELECT COUNT_BIG(*) FROM ({0}) AS p;
        SELECT * FROM (
            SELECT p.*, ROW_NUMBER() OVER (ORDER BY p.[Изменено] DESC, p.{1}, p.[Изменил]) AS __clay_report_row
            FROM ({0}) AS p
        ) AS ranked WHERE __clay_report_row BETWEEN @FirstRow AND @LastRow ORDER BY __clay_report_row;
        """;

    /// <summary>Снимок прав на действия с FMTN через штатное представление.</summary>
    public const string SELECT_SimFmtnAccess = """
        SELECT CONVERT(bit,ISNULL(HAS_PERMS_BY_NAME(N'dbo.vwSIMКартыFMTN','OBJECT','SELECT'),0)) AS CanRead,
               CONVERT(bit,ISNULL(HAS_PERMS_BY_NAME(N'dbo.vwSIMКартыFMTN','OBJECT','INSERT'),0)) AS CanInsert,
               CONVERT(bit,ISNULL(HAS_PERMS_BY_NAME(N'dbo.vwSIMКартыFMTN','OBJECT','UPDATE'),0)) AS CanUpdate,
               CONVERT(bit,ISNULL(HAS_PERMS_BY_NAME(N'dbo.vwSIMКартыFMTN','OBJECT','DELETE'),0)) AS CanDelete;
        """;

    /// <summary>FMTN по точному строковому ключу. Место для доверенной SQL-подсказки блокировки.</summary>
    public const string SELECT_SimFmtn = """
        SELECT f.ПрефиксFMTN AS Prefix,f.КороткийНомер AS ShortNumber,f.Описание AS Description,
               f.Изменил AS ChangedBy,e.Сотрудник AS ChangedByName,f.Изменено AS ChangedAt
        FROM dbo.vwSIMКартыFMTN f {0}
        LEFT JOIN dbo.Сотрудники e ON e.КодСотрудника=f.Изменил
        WHERE f.ПрефиксFMTN=@Key;
        """;

    /// <summary>Создание отдельной записи FMTN.</summary>
    public const string INSERT_SimFmtn = """
        INSERT dbo.vwSIMКартыFMTN (ПрефиксFMTN,КороткийНомер,Описание) VALUES (@Prefix,@ShortNumber,@Description);
        """;

    /// <summary>Изменение FMTN по исходному ключу, в том числе замена строкового префикса.</summary>
    public const string UPDATE_SimFmtn = """
        UPDATE dbo.vwSIMКартыFMTN SET ПрефиксFMTN=@Prefix,КороткийНомер=@ShortNumber,Описание=@Description
        WHERE ПрефиксFMTN=@Key;
        """;

    /// <summary>Удаление FMTN по строковому ключу.</summary>
    public const string DELETE_SimFmtn = "DELETE dbo.vwSIMКартыFMTN WHERE ПрефиксFMTN=@Key;";

    /// <summary>Поиск разрешённых почтовых адресов в штатном представлении.</summary>
    public const string SELECT_SlBlAddresses = """
        SELECT DISTINCT TOP (@Limit) Address FROM dbo.vwMailBoxesAddresses
        WHERE Address LIKE @Pattern ORDER BY Address;
        """;

    /// <summary>Восстановление точного почтового адреса.</summary>
    public const string SELECT_SlBlAddressById = "SELECT Address FROM dbo.vwMailBoxesAddresses WHERE Address=@Address;";

    /// <summary>Безопасный и заблокированный списки одного почтового адреса.</summary>
    public const string SELECT_SlBlLists = """
        SELECT MAX(CASE WHEN Block=0 THEN SlBl END) AS SafeList,
               MAX(CASE WHEN Block=1 THEN SlBl END) AS BlockedList
        FROM dbo.vwMailBoxesAddressesSlBl WHERE Address=@Address GROUP BY Address;
        """;

    /// <summary>Сверка фактического и желаемого содержимого через штатный parser. Выполняется только чтение.</summary>
    public const string SELECT_SlBlDifferences = """
        DECLARE @Actual nvarchar(max);
        SET @Actual = ISNULL((SELECT SlBl FROM dbo.vwMailBoxesAddressesSlBl WHERE Address=@Address AND Block=@Block),N'');
        WITH SavedWords AS (SELECT DISTINCT word FROM dbo.fn_SplitText(@Actual)),
             DraftWords AS (SELECT DISTINCT word FROM dbo.fn_SplitText(@Text)),
             NotDeleted AS (SELECT word FROM SavedWords EXCEPT SELECT word FROM DraftWords),
             NotSaved AS (SELECT word FROM DraftWords EXCEPT SELECT word FROM SavedWords)
        SELECT ISNULL(STUFF((SELECT N', '+word FROM NotDeleted ORDER BY word FOR XML PATH(''),TYPE).value('.', 'nvarchar(max)'),1,2,N''),N'') AS NotDeleted,
               ISNULL(STUFF((SELECT N', '+word FROM NotSaved ORDER BY word FOR XML PATH(''),TYPE).value('.', 'nvarchar(max)'),1,2,N''),N'') AS NotSaved;
        """;

    /// <summary>
    /// Выборка всех медицинских исследований с названием типа.
    /// Возвращает базовый запрос без WHERE и ORDER BY —
    /// фильтрация и сортировка добавляются динамически через <see cref="Clayzor.Lib.Web.Controls.ClayDataQuery"/>.
    /// </summary>
    public const string SELECT_МедицинскиеАнализы = @"
-- Основные поля исследования
SELECT a.КодМедицинскогоАнализа,       -- идентификатор исследования
       a.КодТипаМедицинскогоАнализа,   -- код типа исследования
       a.НазваниеАнализа,             -- наименование
       a.НормаОтЖенщины,              -- нижняя граница нормы (женщины)
       a.НормаПоЖенщины,              -- верхняя граница нормы (женщины)
       a.НормаОтМужчины,              -- нижняя граница нормы (мужчины)
       a.НормаПоМужчины,              -- верхняя граница нормы (мужчины)
       a.НормаСтрока,                 -- текстовая норма
       a.Группа,                      -- признак группы
       a.Заключение,                  -- признак заключения
       a.Порядок,                     -- порядок сортировки
       t.ТипМедицинскогоАнализа AS TestTypeName  -- наименование типа из справочника
FROM МедицинскиеАнализы a
LEFT JOIN МедицинскиеАнализыТипы t
  ON a.КодТипаМедицинскогоАнализа = t.КодТипаМедицинскогоАнализа";

    /// <summary>
    /// Выборка справочника типов медицинских исследований.
    /// Используется при заполнении выпадающего списка в форме редактирования.
    /// </summary>
    public const string SELECT_МедицинскиеАнализыТипы = @"
-- Справочник типов исследований
SELECT КодТипаМедицинскогоАнализа AS Id,   -- идентификатор типа
       ТипМедицинскогоАнализа AS Name       -- наименование типа
FROM МедицинскиеАнализыТипы
ORDER BY ТипМедицинскогоАнализа     -- сортировка по алфавиту
";

    /// <summary>
    /// Добавление нового медицинского исследования.
    /// Параметры: TestTypeId, Name, NormMinFemale, NormMaxFemale,
    /// NormMinMale, NormMaxMale, NormString, IsGroup, IsConclusion, Order.
    /// </summary>
    public const string INSERT_МедицинскиеАнализы = @"
-- Добавление нового исследования
INSERT INTO МедицинскиеАнализы (
    КодТипаМедицинскогоАнализа,   -- код типа
    НазваниеАнализа,             -- наименование
    НормаОтЖенщины,              -- нижняя граница нормы (женщины)
    НормаПоЖенщины,              -- верхняя граница нормы (женщины)
    НормаОтМужчины,              -- нижняя граница нормы (мужчины)
    НормаПоМужчины,              -- верхняя граница нормы (мужчины)
    НормаСтрока,                 -- текстовая норма
    Группа,                      -- признак группы
    Заключение,                  -- признак заключения
    Порядок                      -- порядок сортировки
) VALUES (
    @TestTypeId,
    @Name,
    @NormMinFemale,
    @NormMaxFemale,
    @NormMinMale,
    @NormMaxMale,
    @NormString,
    @IsGroup,
    @IsConclusion,
    @Order
)";

    /// <summary>
    /// Обновление существующего медицинского исследования.
    /// Параметры: TestTypeId, Name, NormMinFemale, NormMaxFemale,
    /// NormMinMale, NormMaxMale, NormString, IsGroup, IsConclusion, Order, Id.
    /// </summary>
    public const string UPDATE_МедицинскиеАнализы = @"
-- Обновление исследования
UPDATE МедицинскиеАнализы SET
    КодТипаМедицинскогоАнализа = @TestTypeId,  -- код типа
    НазваниеАнализа = @Name,                    -- наименование
    НормаОтЖенщины = @NormMinFemale,            -- нижняя граница нормы (женщины)
    НормаПоЖенщины = @NormMaxFemale,            -- верхняя граница нормы (женщины)
    НормаОтМужчины = @NormMinMale,              -- нижняя граница нормы (мужчины)
    НормаПоМужчины = @NormMaxMale,              -- верхняя граница нормы (мужчины)
    НормаСтрока = @NormString,                  -- текстовая норма
    Группа = @IsGroup,                          -- признак группы
    Заключение = @IsConclusion,                 -- признак заключения
    Порядок = @Order                             -- порядок сортировки
WHERE КодМедицинскогоАнализа = @Id    -- идентификатор записи
";

    /// <summary>
    /// Удаление медицинского исследования по идентификатору.
    /// Параметры: Id (КодМедицинскогоАнализа).
    /// </summary>
    public const string DELETE_МедицинскиеАнализы = @"
-- Удаление исследования
DELETE FROM МедицинскиеАнализы
WHERE КодМедицинскогоАнализа = @Id    -- идентификатор записи
";

    /// <summary>
    /// Поиск типов оборудования по наименованию. Параметры: Search, LimitPlusOne, SortByName.
    /// </summary>
    public const string SELECT_ТипыОборудованияДляВыбора = @"
SELECT TOP (@LimitPlusOne)
       КодТипаОборудования,          -- идентификатор типа
       ТипОборудования,              -- наименование типа
       ТипОборудованияЛат,           -- наименование на английском языке
       SNобязателен,                 -- обязательность серийного номера
       БезТочногоРасположения,       -- признак отсутствия точного расположения
       ЕстьХарактеристикиКомпьютера, -- признак характеристик компьютера
       ЕстьХарактеристикиМонитора,   -- признак характеристик монитора
       ЕстьХарактеристикиSIM,        -- признак характеристик SIM-карты
       ЕстьХарактеристикиSoft,       -- признак программного обеспечения
       ЕстьТелефонныйНомер,          -- признак телефонного номера
       ЕстьMACадрес,                 -- максимальное количество MAC-адресов
       ЕстьСетевоеИмя                -- признак сетевого имени
FROM ТипыОборудования
WHERE @Search = '' OR ТипОборудования LIKE '%' + @Search + '%'
ORDER BY CASE WHEN @SortByName = 1 THEN ТипОборудования END,
         CASE WHEN @SortByName = 0 THEN КодТипаОборудования END";

    /// <summary>
    /// Тип оборудования по идентификатору. Параметр: Id.
    /// </summary>
    public const string SELECT_ТипОборудованияПоИдентификатору = @"
SELECT КодТипаОборудования,          -- идентификатор типа
       ТипОборудования,              -- наименование типа
       ТипОборудованияЛат,           -- наименование на английском языке
       SNобязателен,                 -- обязательность серийного номера
       БезТочногоРасположения,       -- признак отсутствия точного расположения
       ЕстьХарактеристикиКомпьютера, -- признак характеристик компьютера
       ЕстьХарактеристикиМонитора,   -- признак характеристик монитора
       ЕстьХарактеристикиSIM,        -- признак характеристик SIM-карты
       ЕстьХарактеристикиSoft,       -- признак программного обеспечения
       ЕстьТелефонныйНомер,          -- признак телефонного номера
       ЕстьMACадрес,                 -- максимальное количество MAC-адресов
       ЕстьСетевоеИмя                -- признак сетевого имени
FROM ТипыОборудования
WHERE КодТипаОборудования = @Id";

    /// <summary>Полная модель оборудования для формы редактирования. Параметр: Id.</summary>
    public const string SELECT_МодельОборудованияДляРедактирования = @"
SELECT m.КодМоделиОборудования, -- идентификатор модели
       m.МодельОборудования,    -- наименование модели
       m.КодТипаОборудования,   -- идентификатор типа
       NULLIF(m.КодРесурса, 0) AS КодРесурса, -- идентификатор ресурса
       m.PN,                    -- Part Number
       m.CPU,                   -- процессор
       CONVERT(float, m.РазмерМонитора) AS РазмерМонитора, -- диагональ монитора
       m.КодТипаПамяти,         -- тип памяти
       CONVERT(int, m.БанковПамяти) AS БанковПамяти, -- количество банков памяти
       CONVERT(bit, m.Мультимедиа) AS Мультимедиа, -- признак мультимедиа
       CONVERT(int, m.Net) AS Net, -- количество сетевых интерфейсов
       m.НазваниеNet,           -- название сетевого интерфейса
       m.Драйвер,               -- сведения о драйвере
       m.Ресурс,                -- срок службы
       m.ШаблонТелефона,        -- шаблон телефона
       m.Примечания,            -- примечания
       m.Изменил,               -- идентификатор изменившего сотрудника
       m.Изменено,              -- дата последнего изменения
       e.Сотрудник AS ChangedByName -- имя изменившего сотрудника
FROM МоделиОборудования m
LEFT JOIN Сотрудники e ON e.КодСотрудника = m.Изменил
WHERE m.КодМоделиОборудования = @Id";

    /// <summary>Проверка дубликата пары «модель + PN». Параметры: Id, Name, PartNumber.</summary>
    public const string SELECT_МодельОборудования_EXISTS = @"
IF EXISTS (SELECT 1 FROM МоделиОборудования
           WHERE КодМоделиОборудования <> @Id
             AND МодельОборудования = @Name
             AND PN = @PartNumber)
    SELECT 1; -- дубликат существует
ELSE
    SELECT 0; -- дубликат отсутствует";

    /// <summary>Количество всего оборудования модели. Параметр: Id.</summary>
    public const string SELECT_ID_КоличествоОборудованияМодели = @"
SELECT COUNT(*) AS N, -- общее количество оборудования
       COALESCE(SUM(CASE WHEN Списано IS NULL THEN 0 ELSE 1 END), 0) AS Списанного -- списано
FROM vwОборудование
WHERE КодМоделиОборудования = @Id";

    /// <summary>Количество оборудования при фактической смене типа. Параметры: Id, EquipmentTypeId.</summary>
    public const string SELECT_ID_КоличествоОборудованияМоделиДругогоТипа = @"
SELECT COUNT(*) AS N, -- общее количество оборудования
       COALESCE(SUM(CASE WHEN Списано IS NULL THEN 0 ELSE 1 END), 0) AS Списанного -- списано
FROM vwОборудование
WHERE КодМоделиОборудования = @Id
  AND EXISTS (SELECT 1 FROM МоделиОборудования
              WHERE КодМоделиОборудования = @Id
                AND КодТипаОборудования <> @EquipmentTypeId)";

    /// <summary>Создание модели оборудования с возвратом идентификатора.</summary>
    public const string INSERT_МоделиОборудования = @"
INSERT INTO МоделиОборудования (
    КодРесурса,         -- ресурс
    КодТипаОборудования,-- тип оборудования
    МодельОборудования, -- наименование модели
    PN,                 -- Part Number
    CPU,                -- процессор
    РазмерМонитора,     -- диагональ монитора
    КодТипаПамяти,      -- тип памяти
    БанковПамяти,       -- количество банков памяти
    Мультимедиа,        -- признак мультимедиа
    Net,                -- количество сетевых интерфейсов
    НазваниеNet,        -- название сетевого интерфейса
    Драйвер,            -- сведения о драйвере
    Ресурс,             -- срок службы
    ШаблонТелефона,     -- шаблон телефона
    Примечания          -- примечания
) VALUES (
    @ResourceId, @EquipmentTypeId, @Name, @PartNumber, @Cpu, @MonitorSize,
    @MemoryTypeId, @MemoryBanks, @IsMultimedia, @NetworkInterfaces, @NetworkName,
    @Driver, @LifeTime, @PhoneTemplate, @Description);
SELECT CAST(@@IDENTITY AS int) AS Id; -- идентификатор созданной модели";

    /// <summary>Обновление модели оборудования.</summary>
    public const string UPDATE_МоделиОборудования = @"
UPDATE МоделиОборудования SET
    КодРесурса = @ResourceId,              -- ресурс
    КодТипаОборудования = @EquipmentTypeId,-- тип оборудования
    МодельОборудования = @Name,            -- наименование модели
    PN = @PartNumber,                      -- Part Number
    CPU = @Cpu,                            -- процессор
    РазмерМонитора = @MonitorSize,         -- диагональ монитора
    КодТипаПамяти = @MemoryTypeId,         -- тип памяти
    БанковПамяти = @MemoryBanks,           -- количество банков памяти
    Мультимедиа = @IsMultimedia,           -- признак мультимедиа
    Net = @NetworkInterfaces,              -- количество сетевых интерфейсов
    НазваниеNet = @NetworkName,            -- название сетевого интерфейса
    Драйвер = @Driver,                     -- сведения о драйвере
    Ресурс = @LifeTime,                    -- срок службы
    ШаблонТелефона = @PhoneTemplate,       -- шаблон телефона
    Примечания = @Description              -- примечания
WHERE КодМоделиОборудования = @Id";

    /// <summary>Обновление только шаблона телефона. Параметры: Id, PhoneTemplate.</summary>
    public const string UPDATE_МоделиОборудования_ШаблонТелефона = @"
UPDATE МоделиОборудования SET
    ШаблонТелефона = @PhoneTemplate -- шаблон настройки телефона
WHERE КодМоделиОборудования = @Id";

    /// <summary>Список типов памяти.</summary>
    public const string SELECT_ТипыПамяти = @"
SELECT КодТипаПамяти, -- идентификатор типа памяти
       ТипПамяти      -- наименование типа памяти
FROM ТипыПамяти
ORDER BY ТипПамяти";

    /// <summary>Поиск ресурсов. Параметры: Search, LimitPlusOne.</summary>
    public const string SELECT_РесурсыДляВыбора = @"
SELECT TOP (@LimitPlusOne)
       КодРесурса, -- идентификатор ресурса
       ISNULL(NULLIF(РесурсРус, ''), '#' + CONVERT(varchar, КодРесурса)) AS РесурсРус -- наименование
FROM Ресурсы
WHERE @Search = '' OR РесурсРус LIKE '%' + @Search + '%'
ORDER BY РесурсРус";

    /// <summary>Ресурс по идентификатору. Параметр: Id.</summary>
    public const string SELECT_РесурсПоИдентификатору = @"
SELECT КодРесурса, -- идентификатор ресурса
       ISNULL(NULLIF(РесурсРус, ''), '#' + CONVERT(varchar, КодРесурса)) AS РесурсРус -- наименование
FROM Ресурсы
WHERE КодРесурса = @Id";

    /// <summary>Проверка роли текущего пользователя с учётом замещений. Параметр: RoleId.</summary>
    public const string SELECT_ТекущийПользовательИмеетРоль = @"
IF EXISTS (SELECT 1 FROM fn_ТекущиеРоли() WHERE КодРоли = @RoleId)
    SELECT 1; -- роль доступна текущему пользователю
ELSE
    SELECT 0; -- роль отсутствует";

    /// <summary>Проверка глобальной роли текущего пользователя. Параметр: RoleId.</summary>
    public const string SELECT_ТекущийПользовательИмеетГлобальнуюРоль = @"
IF EXISTS (SELECT 1 FROM fn_ТекущиеРоли() WHERE КодРоли = @RoleId AND КодЛица = 0)
    SELECT 1; -- глобальная роль доступна текущему пользователю
ELSE
    SELECT 0; -- глобальная роль отсутствует";

    /// <summary>Проверка роли текущего пользователя только для конкретного лица. Параметры: RoleId, PersonId.</summary>
    public const string SELECT_ТекущийПользовательИмеетРольТолькоПоЛицу = @"
IF EXISTS (SELECT 1 FROM fn_ТекущиеРоли() WHERE КодРоли = @RoleId AND КодЛица = @PersonId)
    SELECT 1; -- роль доступна для конкретного лица
ELSE
    SELECT 0; -- роль для конкретного лица отсутствует";

    /// <summary>Снимок effective roles текущего пользователя для отображения формы.</summary>
    public const string SELECT_ТекущиеРолиДляСотрудника = @"
SELECT КодРоли AS RoleId, -- идентификатор роли
       КодЛица AS PersonId -- область лица; 0 означает глобальную роль
FROM fn_ТекущиеРоли()";

    /// <summary>
    /// Поиск моделей с необязательным фильтром типа. Параметры: EquipmentTypeId, Search, LimitPlusOne.
    /// </summary>
    public const string SELECT_МоделиОборудованияДляВыбора = @"
SELECT TOP (@LimitPlusOne)
       m.КодМоделиОборудования, -- идентификатор модели
       m.МодельОборудования,    -- наименование модели
       m.КодТипаОборудования,   -- идентификатор типа
       NULLIF(m.КодРесурса, 0) AS КодРесурса, -- идентификатор ресурса
       m.PN,                    -- Part Number
       m.CPU,                   -- процессор
       CONVERT(float, m.РазмерМонитора) AS РазмерМонитора, -- диагональ
       m.КодТипаПамяти,         -- идентификатор типа памяти
       CONVERT(int, m.БанковПамяти) AS БанковПамяти, -- количество банков
       CONVERT(bit, m.Мультимедиа) AS Мультимедиа, -- признак мультимедиа
       CONVERT(int, m.Net) AS Net, -- количество сетевых интерфейсов
       m.НазваниеNet,           -- название сетевого интерфейса
       m.Драйвер,               -- сведения о драйвере
       m.Ресурс,                -- ресурс модели
       m.ШаблонТелефона,        -- шаблон конфигурации телефона
       m.Примечания,            -- примечания модели
       mt.ТипПамяти AS ТипПамяти -- название типа памяти
FROM МоделиОборудования m
LEFT JOIN ТипыПамяти mt ON mt.КодТипаПамяти = m.КодТипаПамяти
WHERE (@EquipmentTypeId IS NULL OR m.КодТипаОборудования = @EquipmentTypeId)
  AND (@Search = '' OR m.МодельОборудования LIKE '%' + @Search + '%')
ORDER BY m.МодельОборудования";

    /// <summary>
    /// Полная модель оборудования по идентификатору. Параметр: Id.
    /// </summary>
    public const string SELECT_МодельОборудованияПоИдентификатору = @"
SELECT m.КодМоделиОборудования, -- идентификатор модели
       m.МодельОборудования,    -- наименование модели
       m.КодТипаОборудования,   -- идентификатор типа
       NULLIF(m.КодРесурса, 0) AS КодРесурса, -- идентификатор ресурса
       m.PN,                    -- Part Number
       m.CPU,                   -- процессор
       CONVERT(float, m.РазмерМонитора) AS РазмерМонитора, -- диагональ
       m.КодТипаПамяти,         -- идентификатор типа памяти
       CONVERT(int, m.БанковПамяти) AS БанковПамяти, -- количество банков
       CONVERT(bit, m.Мультимедиа) AS Мультимедиа, -- признак мультимедиа
       CONVERT(int, m.Net) AS Net, -- количество сетевых интерфейсов
       m.НазваниеNet,           -- название сетевого интерфейса
       m.Драйвер,               -- сведения о драйвере
       m.Ресурс,                -- ресурс модели
       m.ШаблонТелефона,        -- шаблон конфигурации телефона
       m.Примечания,            -- примечания модели
       mt.ТипПамяти AS ТипПамяти -- название типа памяти
FROM МоделиОборудования m
LEFT JOIN ТипыПамяти mt ON mt.КодТипаПамяти = m.КодТипаПамяти
WHERE m.КодМоделиОборудования = @Id";

    /// <summary>
    /// Поиск сотрудников по русскому и английскому имени. Параметры: Search, LimitPlusOne.
    /// </summary>
    public const string SELECT_СотрудникиДляВыбора = @"
SELECT TOP (@LimitPlusOne)
       КодСотрудника,  -- идентификатор сотрудника
       Сотрудник,      -- отображаемое имя
       Employee        -- имя на английском языке
FROM Сотрудники
WHERE @Search = ''
   OR Сотрудник LIKE '%' + @Search + '%'
   OR Employee LIKE '%' + @Search + '%'
ORDER BY Сотрудник";

    /// <summary>
    /// Сотрудник по идентификатору. Параметр: Id.
    /// </summary>
    public const string SELECT_СотрудникПоИдентификатору = @"
SELECT S.КодСотрудника,  -- идентификатор сотрудника
       S.Сотрудник,      -- отображаемое имя
       S.Employee,       -- имя на английском языке
       S.КодЛицаЗаказчика, -- организация сотрудника
       ISNULL(O.Кличка, '') AS Организация -- название организации сотрудника
FROM Сотрудники S
LEFT JOIN ЛицаЗаказчики O ON O.КодЛица = S.КодЛицаЗаказчика
WHERE S.КодСотрудника = @Id";

    /// <summary>Текущий сотрудник при совпадении Windows и SQL principal. Параметр: WindowsPrincipal.</summary>
    public const string SELECT_ТекущийСотрудникПоPrincipal = @"
SELECT КодСотрудника, -- идентификатор сотрудника
       Сотрудник, -- отображаемое имя
       Employee, -- английское имя
       КодЛицаЗаказчика -- организация
FROM Сотрудники
WHERE SID = SUSER_SID() AND @WindowsPrincipal = SUSER_SNAME()";

    /// <summary>
    /// Базовый запрос сотрудников для серверного поиска, сортировки и пагинации ClayGrid.
    /// </summary>
    public const string SELECT_СотрудникиДляПоиска = @"
SELECT КодСотрудника AS Id,  -- идентификатор сотрудника
       Сотрудник AS Name,    -- отображаемое имя
       Employee AS NameEn    -- имя на английском языке
FROM Сотрудники";

    /// <summary>Создание оборудования и начальных связанных записей. Возвращает output @КодОборудования.</summary>
    public const string SP_Оборудование_Ins = "dbo.sp_Оборудование_Ins";

    /// <summary>Основные сведения оборудования, модель, тип и текущие связи. Параметр: Id.</summary>
    public const string SELECT_ID_Оборудование = @"
SELECT E.КодОборудования,       -- идентификатор оборудования
       E.КодМоделиОборудования, -- идентификатор модели
       M.КодТипаОборудования,   -- идентификатор типа
       M.МодельОборудования,    -- название модели
       M.PN,                    -- парт-номер модели
       T.ТипОборудования,       -- название типа
       E.SN,                    -- серийный номер
       E.ПодключенаКамера,      -- признак подключённой камеры
       E.Списано,               -- дата и время списания UTC
       E.КодДокументаСписания,  -- документ списания
       E.СетевоеИмя,            -- сетевое имя
       E.MACадрес,              -- основной MAC-адрес
       E.MACадрес2,             -- второй MAC-адрес
       E.MACадресILO,           -- MAC-адрес ILO
       E.Примечания,            -- примечания оборудования
       E.КодЛицаВладельца,      -- владелец
       E.КодЛицаАрендатора,     -- арендатор
       E.КодЛицаПоставщика,     -- поставщик
       E.КодПулаТелефонов,      -- телефонный пул
       E.КодДокументаПоступления, -- документ поступления ОУ
       E.Изменил,               -- сотрудник последнего изменения
       E.Изменено,              -- время последнего изменения UTC
       R.КодРасположения,       -- последнее расположение
       S.КодСотрудника,         -- текущий ответственный
       CH.ФИО AS ИзменилФИО     -- ФИО изменившего сотрудника
FROM vwОборудование E
INNER JOIN МоделиОборудования M ON M.КодМоделиОборудования = E.КодМоделиОборудования
INNER JOIN ТипыОборудования T ON T.КодТипаОборудования = M.КодТипаОборудования
LEFT JOIN ОборудованиеРасположение R ON R.КодОборудования = E.КодОборудования
     AND NOT EXISTS (SELECT 1 FROM ОборудованиеРасположение X
                     WHERE X.КодОборудования = R.КодОборудования AND X.От > R.От)
LEFT JOIN ОборудованиеСотрудников S ON S.КодОборудования = E.КодОборудования AND S.До IS NULL
LEFT JOIN Сотрудники CH ON CH.КодСотрудника = E.Изменил
WHERE E.КодОборудования = @Id";

    /// <summary>Фактический тип выбранной модели и правила сохранения основной карточки. Параметр: ModelId.</summary>
    public const string SELECT_МодельОборудованияТипДляСохранения = @"
SELECT T.КодТипаОборудования, -- фактический тип модели
       T.SNобязателен,        -- обязательность серийного номера
       T.ЕстьMACадрес         -- максимальное количество MAC-адресов
FROM МоделиОборудования M
INNER JOIN ТипыОборудования T ON M.КодТипаОборудования = T.КодТипаОборудования
WHERE M.КодМоделиОборудования = @ModelId";

    /// <summary>Обновление 15 основных полей оборудования. Параметры соответствуют одноимённым колонкам.</summary>
    public const string UPDATE_Оборудование = @"
UPDATE vwОборудование
SET SN = @SN, -- серийный номер
    ПодключенаКамера = @ПодключенаКамера, -- признак камеры
    Списано = @Списано, -- дата и время списания UTC
    КодДокументаСписания = @КодДокументаСписания, -- документ списания
    СетевоеИмя = @СетевоеИмя, -- сетевое имя
    MACадрес = @MACадрес, -- основной MAC-адрес
    MACадрес2 = @MACадрес2, -- второй MAC-адрес
    MACадресILO = @MACадресILO, -- MAC-адрес ILO
    Примечания = @Примечания, -- примечания
    КодЛицаВладельца = @КодЛицаВладельца, -- владелец
    КодЛицаАрендатора = @КодЛицаАрендатора, -- арендатор
    КодЛицаПоставщика = @КодЛицаПоставщика, -- поставщик
    КодПулаТелефонов = @КодПулаТелефонов, -- телефонный пул
    КодМоделиОборудования = @КодМоделиОборудования, -- модель
    КодДокументаПоступления = @КодДокументаПоступления -- документ поступления ОУ
WHERE КодОборудования = @КодОборудования
  AND Изменено = @ИсходноеИзменено";

    /// <summary>Данные оборудования для копирования. Параметр: Id.</summary>
    public const string SELECT_ОборудованиеДляКопирования = @"
SELECT КодТипаОборудования,  -- идентификатор типа
       КодМоделиОборудования, -- идентификатор модели
       КодЛицаВладельца,      -- идентификатор владельца
       КодРасположения        -- идентификатор расположения
FROM vwОборудованиеСписок
WHERE КодОборудования = @Id";

    /// <summary>Проверка роли текущего пользователя для всех лиц или конкретного лица. Параметры: RoleId, PersonId.</summary>
    public const string SELECT_ТекущийПользовательИмеетРольПоЛицу = @"
IF EXISTS (SELECT 1 FROM fn_ТекущиеРоли() WHERE КодРоли = @RoleId AND КодЛица IN (0, @PersonId))
    SELECT 1; -- роль доступна текущему пользователю
ELSE
    SELECT 0; -- роль отсутствует";

    /// <summary>Признак ведения бухгалтерского учёта лицом. Параметр: PersonId.</summary>
    public const string SELECT_ЛицоВедетБухгалтерскийУчет = @"
SELECT CONVERT(int, dbo.fn_БухгалтерскийУчет(@PersonId)); -- 1, если лицо ведёт бухгалтерский учёт";

    /// <summary>Поиск лиц в базе справочников.</summary>
    public const string SP_Лица_Поиск = "dbo.sp_Лица_Поиск";

    /// <summary>Лицо по идентификатору. Параметр: Id.</summary>
    public const string SELECT_ЛицоПоИдентификатору = @"
SELECT КодЛица, -- идентификатор лица
       Кличка   -- отображаемое название
FROM vwЛица
WHERE КодЛица = @Id";

    /// <summary>Поиск рабочих расположений. Параметры: Search, LimitPlusOne.</summary>
    public const string SELECT_РасположенияДляВыбора = @"
SELECT TOP (@LimitPlusOne)
       КодРасположения,   -- идентификатор расположения
       РасположениеPath1  -- полный путь расположения
FROM vwРасположения
WHERE РабочееМесто IN (1, 2, 3, 4, 5)
  AND Закрыто = 0
  AND (@Search = '' OR Расположение LIKE '%' + @Search + '%' OR РасположениеPath1 LIKE '%' + @Search + '%')
ORDER BY L";

    /// <summary>Расположение по идентификатору. Параметр: Id.</summary>
    public const string SELECT_РасположениеПоИдентификатору = @"
SELECT КодРасположения,  -- идентификатор расположения
       РасположениеPath1 -- полный путь расположения
FROM vwРасположения
WHERE КодРасположения = @Id";

    /// <summary>Поиск документов. Параметры: Search, PersonId, LimitPlusOne.</summary>
    public const string SELECT_ДокументыДляВыбора = @"
SELECT TOP (@LimitPlusOne)
       T0.КодДокумента,      -- идентификатор документа
       T0.НазваниеДокумента, -- название документа
       T0.НомерДокумента,    -- номер документа
       T0.ДатаДокумента      -- дата документа
FROM vwДокументы T0
WHERE (@Search = '' OR T0.НазваниеДокумента LIKE '%' + @Search + '%' OR T0.НомерДокумента LIKE '%' + @Search + '%')
  AND (@PersonId IS NULL OR EXISTS (SELECT 1 FROM vwЛицаДокументов P WHERE P.КодДокумента = T0.КодДокумента AND P.КодЛица = @PersonId))
  AND (@DocumentTypeId IS NULL OR T0.КодТипаДокумента = @DocumentTypeId)
ORDER BY T0.ДатаДокумента DESC, T0.КодДокумента DESC";

    /// <summary>Документ по идентификатору. Параметр: Id.</summary>
    public const string SELECT_ДокументПоИдентификатору = @"
SELECT КодДокумента,      -- идентификатор документа
       НазваниеДокумента, -- название документа
       НомерДокумента,    -- номер документа
       ДатаДокумента      -- дата документа
FROM vwДокументы
WHERE КодДокумента = @Id";

    /// <summary>Акты списания, содержащие оборудование. Параметр: EquipmentId.</summary>
    public const string SELECT_АктыСписанияОборудования = @"
SELECT D.КодДокумента,      -- идентификатор акта
       D.НазваниеДокумента, -- название акта
       D.НомерДокумента,    -- номер акта
       D.ДатаДокумента      -- дата акта
FROM vwПозицииАктовСписанияОборудования X
INNER JOIN vwДокументы D ON D.КодДокумента = X.КодДокумента
WHERE X.КодОборудования = @EquipmentId
ORDER BY D.КодДокумента DESC";

    /// <summary>Уникальные лица документа, кроме владельца. Параметры: DocumentId, OwnerId.</summary>
    public const string SELECT_ПоставщикиДокумента = @"
SELECT DISTINCT КодЛица -- идентификатор лица документа
FROM vwЛицаДокументов
WHERE КодДокумента = @DocumentId
  AND КодЛица <> @OwnerId";

    /// <summary>Поиск телефонных пулов. Параметры: Search, LimitPlusOne.</summary>
    public const string SELECT_ПулыТелефоновДляВыбора = @"
SELECT TOP (@LimitPlusOne)
       КодПулаТелефонов, -- идентификатор пула
       ПулТелефона       -- отображаемое название
FROM vwПулыТелефонов
WHERE @Search = '' OR ПулТелефона LIKE '%' + @Search + '%'
ORDER BY ПулТелефона";

    /// <summary>Телефонный пул по идентификатору. Параметр: Id.</summary>
    public const string SELECT_ПулТелефоновПоИдентификатору = @"
SELECT КодПулаТелефонов, -- идентификатор пула
       ПулТелефона       -- отображаемое название
FROM vwПулыТелефонов
WHERE КодПулаТелефонов = @Id";

    /// <summary>Телефонные номера оборудования. Параметр: EquipmentId.</summary>
    public const string SELECT_ТелефонныеНомераОборудования = @"
SELECT КодТелефонногоНомера,       -- идентификатор номера
       @EquipmentId AS КодОборудования, -- оборудование текущей формы
       Описание,                   -- отображаемое описание
       Основной,                   -- основной номер
       КодОборудованияПодключения, -- оборудование подключения
       НомерАвтонабора,            -- номер автонабора
       ЗадержкаАвтонабора,         -- задержка автонабора
       СрокВозврата,               -- срок возврата
       Примечание,                 -- примечание к сроку
       Изменил,                    -- изменивший сотрудник
       Изменено                    -- время изменения
FROM dbo.fn_НомераТелАппаратов(@EquipmentId)
ORDER BY Основной DESC, Описание";

    /// <summary>Актуальные привязки номера ко всем карточкам оборудования. Параметр: PhoneId.</summary>
    public const string SELECT_ТелефонныйНомерАктуальныеПривязки = @"
SELECT КодОборудования -- фактическая карточка оборудования
FROM ТелефонныеНомераОборудование
WHERE КодТелефонногоНомера = @PhoneId";

    /// <summary>Количество номеров на текущем расположении оборудования в гостиничном номере. Параметр: EquipmentId.</summary>
    public const string SELECT_UniquePhoneNumberInHotel = @"
SELECT COUNT(*) AS N -- количество телефонных номеров
FROM ТелефонныеНомераОборудование P
INNER JOIN ОборудованиеРасположение R ON P.КодОборудования = R.КодОборудования
INNER JOIN vwРасположения L ON R.КодРасположения = L.КодРасположения
WHERE P.КодОборудования = @EquipmentId
  AND L.РабочееМесто = 2
  AND NOT EXISTS (SELECT 1 FROM ОборудованиеРасположение X
                  WHERE X.КодОборудования = R.КодОборудования AND X.От > R.От)";

    /// <summary>Проверка фактических before-значений связи внутри транзакции. Формат: параметры всех исходных полей.</summary>
    public const string GUARD_ИсходнаяТелефоннаяСвязь = @"
IF NOT EXISTS (SELECT 1 FROM ТелефонныеНомераОборудование WITH (UPDLOCK, HOLDLOCK)
               WHERE КодТелефонногоНомера = {0} AND КодОборудования = @КодОборудования
                 AND (КодОборудованияПодключения = {1} OR (КодОборудованияПодключения IS NULL AND {1} IS NULL))
                 AND ISNULL(НомерАвтонабора, '') = {2}
                 AND (ЗадержкаАвтонабора = {3} OR (ЗадержкаАвтонабора IS NULL AND {3} IS NULL))
                 AND Основной = {4}
                 AND (СрокВозврата = {5} OR (СрокВозврата IS NULL AND {5} IS NULL))
                 AND ISNULL(Примечание, '') = {6})
    RAISERROR('Исходная телефонная связь была изменена или удалена. Обновите данные и повторите операцию.', 16, 1);";

    /// <summary>Удаление подтверждённой связи номера с оборудованием. Формат: параметр номера.</summary>
    public const string DELETE_ОборудованиеТелефона = @"
DELETE FROM ТелефонныеНомераОборудование
WHERE КодТелефонногоНомера = {0} AND КодОборудования = @КодОборудования;
IF @@ROWCOUNT <> 1
    RAISERROR('Исходная телефонная связь не найдена. Изменения отменены.', 16, 1);";

    /// <summary>Обновление связи и CAS по фактическим before-значениям. Формат: after- и before-параметры.</summary>
    public const string UPDATE_ОборудованиеТелефонаСвязи = @"
UPDATE ТелефонныеНомераОборудование
SET КодОборудованияПодключения = {1},
    НомерАвтонабора = {2},
    ЗадержкаАвтонабора = {3},
    Основной = {4},
    СрокВозврата = {5},
    Примечание = {6}
WHERE КодТелефонногоНомера = {0} AND КодОборудования = @КодОборудования
  AND (КодОборудованияПодключения = {7} OR (КодОборудованияПодключения IS NULL AND {7} IS NULL))
  AND ISNULL(НомерАвтонабора, '') = {8}
  AND (ЗадержкаАвтонабора = {9} OR (ЗадержкаАвтонабора IS NULL AND {9} IS NULL))
  AND Основной = {10}
  AND (СрокВозврата = {11} OR (СрокВозврата IS NULL AND {11} IS NULL))
  AND ISNULL(Примечание, '') = {12};
IF @@ROWCOUNT <> 1
    RAISERROR('Исходная телефонная связь была изменена или удалена. Изменения отменены.', 16, 1);";

    /// <summary>Обновление срока возврата связи. Формат: параметры номера, срока и примечания.</summary>
    public const string UPDATE_ОборудованиеТелефонаСрокВозврата = @"
UPDATE ТелефонныеНомераОборудование
SET СрокВозврата = {1}, Примечание = {2}
WHERE КодТелефонногоНомера = {0} AND КодОборудования = @КодОборудования";

    /// <summary>Добавление связи номера с оборудованием. Формат: параметры номера, оборудования подключения, срока и примечания.</summary>
    public const string INSERT_ОборудованиеТелефона = @"
INSERT INTO ТелефонныеНомераОборудование
    (КодТелефонногоНомера, КодОборудования, КодОборудованияПодключения, СрокВозврата, Примечание)
VALUES ({0}, @КодОборудования, {1}, {2}, {3})";

    /// <summary>Общий источник допустимых номеров телефона оборудования.</summary>
    public const string PhoneNumberCandidateSource = "dbo.fn_НомераДляТелАппаратов(@LocationId, @EmployeeId, @EquipmentTypeId, @PhoneIdBase, @EquipmentId)";

    /// <summary>Полная проекция FinderTel с ограничением видимости по рабочему представлению номеров.</summary>
    public const string SELECT_FinderTel = """
        SELECT F.КодТелефонногоНомера AS Id,F.ТелефонныйНомер AS ShortNumber,F.ПолныйНомер AS FullNumber,
               F.КодТипаТелефонныхНомеров AS PhoneTypeId,F.Абонент AS Subscriber,F.КодОборудованияПодключения AS ConnectionEquipmentId
        FROM
        """ + " " + PhoneNumberCandidateSource + """
         F
        JOIN dbo.vwТелефонныеНомераИзменение T0 ON T0.КодТелефонногоНомера=F.КодТелефонногоНомера
        WHERE 1=1
        """;

    /// <summary>Один проход справочника иконок и имён типов номеров.</summary>
    public const string SELECT_FinderTelTypes = """
        SELECT КодТипаТелефонныхНомеров AS Id,ТипТелефонныхНомеров AS Name,icon AS Icon
        FROM dbo.ТипыТелефонныхНомеров;
        """;

    /// <summary>Размер и страница списка выбора с серверной сортировкой.</summary>
    public const string SELECT_FinderPage = """
        SELECT COUNT(*) FROM ({0}) filtered;
        SELECT ranked.* FROM (SELECT filtered.*, ROW_NUMBER() OVER (ORDER BY {1}) AS __row FROM ({0}) filtered) ranked
        WHERE ranked.__row BETWEEN @FirstRow AND @LastRow ORDER BY ranked.__row;
        """;

    /// <summary>Уникальные значения колонки без собственной фильтрации.</summary>
    public const string SELECT_FinderDistinct = """
        SELECT TOP (@Limit) v FROM (SELECT DISTINCT {1} AS v FROM ({0}) filtered) valueset ORDER BY v;
        """;

    /// <summary>Поиск телефонных номеров. Параметры legacy-функции, Search, LimitPlusOne и исключённые ID.</summary>
    public const string SELECT_ТелефонныеНомераДляВыбора = @"
SELECT TOP (@LimitPlusOne)
       T0.КодТелефонногоНомера, -- идентификатор номера
       ISNULL(T0.ПолныйНомер + CASE WHEN T0.ПолныйНомер <> T0.ТелефонныйНомер
             THEN '[' + T0.ТелефонныйНомер + ']' ELSE '' END + ' '
             + ISNULL(E.ФИО + ISNULL(' (' + NULLIF(T0.Подключено, '') + ')', ''), T0.Подключено),
             '#' + CONVERT(varchar, T0.КодТелефонногоНомера)) AS Телефон, -- отображаемое имя
       T0.КодТелефоннойСтанции, -- идентификатор АТС
       ISNULL(S.XorcomName, '') AS XorcomName -- признак Xorcom
FROM " + PhoneNumberCandidateSource + @" F
INNER JOIN vwТелефонныеНомераИзменение T0 ON T0.КодТелефонногоНомера = F.КодТелефонногоНомера
LEFT JOIN Сотрудники E ON E.КодСотрудника = T0.КодСотрудника
LEFT JOIN ТелефонныеСтанции S ON S.КодТелефоннойСтанции = T0.КодТелефоннойСтанции
WHERE (@Search = '' OR T0.ТелефонныйНомер LIKE '%' + @Search + '%'
       OR T0.ПолныйНомер LIKE '%' + @Search + '%' OR T0.Подключено LIKE '%' + @Search + '%'
       OR E.ФИО LIKE '%' + @Search + '%')
ORDER BY Телефон";

    /// <summary>Телефонный номер по идентификатору. Параметр: Id.</summary>
    public const string SELECT_ТелефонныйНомерПоИдентификатору = @"
SELECT T0.КодТелефонногоНомера, -- идентификатор номера
       ISNULL(T0.ПолныйНомер + CASE WHEN T0.ПолныйНомер <> T0.ТелефонныйНомер
             THEN '[' + T0.ТелефонныйНомер + ']' ELSE '' END + ' '
             + ISNULL(E.ФИО + ISNULL(' (' + NULLIF(T0.Подключено, '') + ')', ''), T0.Подключено),
             '#' + CONVERT(varchar, T0.КодТелефонногоНомера)) AS Телефон, -- отображаемое имя
       T0.КодТелефоннойСтанции, -- идентификатор АТС
       ISNULL(S.XorcomName, '') AS XorcomName -- признак Xorcom
FROM vwТелефонныеНомераИзменение T0
LEFT JOIN Сотрудники E ON E.КодСотрудника = T0.КодСотрудника
LEFT JOIN ТелефонныеСтанции S ON S.КодТелефоннойСтанции = T0.КодТелефоннойСтанции
WHERE T0.КодТелефонногоНомера = @Id";

    /// <summary>Оборудование подключения аналогового номера сотрудника на расположении. Параметры: PhoneId, EmployeeId, LocationId.</summary>
    public const string SELECT_ТелефонныйНомерОборудованиеПодключения = @"
SELECT КодОборудованияПодключения -- оборудование, доступное для подключения аналогового номера
FROM (
    SELECT DISTINCT P.КодТелефонногоНомера, P.ТелефонныйНомер,
           E.КодОборудования AS КодОборудованияПодключения
    FROM ТелефонныеНомераОборудование L
    INNER JOIN vwТелефонныеНомераИзменение P ON L.КодТелефонногоНомера = P.КодТелефонногоНомера
    LEFT JOIN Сотрудники EMP ON P.КодСотрудника = EMP.КодСотрудника
    INNER JOIN vwОборудование E ON L.КодОборудования = E.КодОборудования
    INNER JOIN МоделиОборудования M ON E.КодМоделиОборудования = M.КодМоделиОборудования
    INNER JOIN ОборудованиеРасположение R ON E.КодОборудования = R.КодОборудования
    INNER JOIN vwРасположения LOC ON R.КодРасположения = LOC.КодРасположения
    INNER JOIN vwРасположения OFFICE ON LOC.L >= OFFICE.L AND LOC.R <= OFFICE.R
    INNER JOIN vwРасположения TARGET ON OFFICE.L <= TARGET.L AND OFFICE.R >= TARGET.R
    WHERE P.КодТипаТелефонныхНомеров = 200 AND M.КодТипаОборудования = 29
      AND (P.КодСотрудника = @EmployeeId OR @EmployeeId IS NULL)
      AND R.От = (SELECT MAX(X.От) FROM ОборудованиеРасположение X WHERE X.КодОборудования = E.КодОборудования)
      AND TARGET.КодРасположения = @LocationId AND (OFFICE.Офис = 1 OR OFFICE.Parent IS NULL)
      AND NOT EXISTS (SELECT 1 FROM ТелефонныеНомераОборудование X
                      WHERE X.КодТелефонногоНомера = L.КодТелефонногоНомера
                        AND X.КодОборудованияПодключения = E.КодОборудования)
) X
WHERE КодТелефонногоНомера = @PhoneId";

    /// <summary>Проверка наличия параметра процедуры. Параметры: ProcedureName, ParameterName.</summary>
    public const string SELECT_ПроцедураИмеетПараметр = @"
SELECT COUNT(*) -- количество совпавших параметров
FROM sys.parameters
WHERE object_id = OBJECT_ID(@ProcedureName) AND name = @ParameterName";

    /// <summary>Синхронизация номера с Xorcom.</summary>
    public const string SP_PhoneSync_Xorcom = "dbo.sp_Xorcom_PhoneNumberSync";

    /// <summary>Синхронизация оборудования с Cisco Call Manager.</summary>
    public const string SP_PhoneSync_Ccm = "dbo.sp_CCM_PhoneSync";

    /// <summary>История сетевых имён оборудования. Параметр: EquipmentId.</summary>
    public const string SELECT_ОборудованиеСетевыеИмена = @"
SELECT N.КодОборудования, -- оборудование
       N.СетевоеИмя,       -- сетевое имя
       N.Изменено,         -- время изменения UTC
       N.Изменил,          -- идентификатор изменившего
       E.ФИО AS ИзменилФИО -- ФИО изменившего
FROM vwОборудованиеСетевыеИмена N
LEFT JOIN Сотрудники E ON E.КодСотрудника = N.Изменил
WHERE N.КодОборудования = @EquipmentId
ORDER BY N.Изменено DESC";

    /// <summary>Другое оборудование той же legacy-группы на рабочем месте. Параметры: Id, IT, EquipmentId.</summary>
    public const string SELECT_ПредупреждениеОборудованиеНаРабочемМесте = @"
SELECT E.КодОборудования AS EquipmentId, -- другое оборудование на рабочем месте
       E.МодельОборудования AS ModelName, -- модель для текста предупреждения
       E.СетевоеИмя AS NetworkName -- сетевое имя для текста предупреждения
FROM vwОборудованиеСписок E
INNER JOIN vwОборудованиеСписок CurrentEquipment
  ON CurrentEquipment.КодОборудования = @EquipmentId
WHERE E.КодРасположения = @Id
  AND (@IT = 0 OR @IT = 1 AND E.КодТипаОборудования < 100)
  AND E.Списано IS NULL
  AND E.КодОборудования <> @EquipmentId
  AND E.РабочееМесто = 1
  -- Подтверждённые группы LocationEquipment_form: компьютеры, мониторы и телефоны; отдельного GroupId нет.
  AND ((CurrentEquipment.КодТипаОборудования IN (1, 3, 5) AND E.КодТипаОборудования IN (1, 3, 5))
    OR (CurrentEquipment.КодТипаОборудования IN (6, 7) AND E.КодТипаОборудования IN (6, 7))
    OR (CurrentEquipment.КодТипаОборудования IN (20, 21, 30) AND E.КодТипаОборудования IN (20, 21, 30)))
ORDER BY E.МодельОборудования";

    /// <summary>История расположений оборудования. Параметр: EquipmentId.</summary>
    public const string SELECT_ОборудованиеРасположение = @"
SELECT R.КодОборудования, -- оборудование
       R.От,              -- начало периода UTC
       R.КодРасположения, -- расположение
       L.РасположениеPath1, -- путь расположения
       R.СрокВозврата,    -- срок возврата
       R.Примечания,      -- примечания
       R.Изменил,         -- идентификатор изменившего
       E.ФИО AS ИзменилФИО, -- ФИО изменившего
       R.Изменено         -- время изменения UTC
FROM ОборудованиеРасположение R
LEFT JOIN vwРасположения L ON L.КодРасположения = R.КодРасположения
LEFT JOIN Сотрудники E ON E.КодСотрудника = R.Изменил
WHERE R.КодОборудования = @EquipmentId
ORDER BY R.От";

    /// <summary>Признак действующего ответственного. Параметр: EquipmentId.</summary>
    public const string SELECT_ОборудованиеЕстьТекущийОтветственный = @"
SELECT COUNT(*) -- количество действующих ответственных
FROM ОборудованиеСотрудников
WHERE КодОборудования = @EquipmentId AND До IS NULL";

    /// <summary>Добавление расположения и связанных SIM телефонной станции.</summary>
    public const string INSERT_ОборудованиеРасположение = @"
IF NOT EXISTS (SELECT 1 FROM ОборудованиеРасположение WHERE КодОборудования = @КодОборудования AND От > @От)
    INSERT ОборудованиеРасположение (КодОборудования, От, КодРасположения, СрокВозврата, Примечания)
    VALUES (@КодОборудования, @От, @КодРасположения, @СрокВозврата, @Примечания);
ELSE
    RAISERROR('За время пока Вы выбирали расположение оборудования кто-то уже добавил новое расположение этому оборудованию.', 12, 1);

DECLARE @СетевоеИмя varchar(50) = '';
SET @СетевоеИмя = ISNULL((
    SELECT E.СетевоеИмя
    FROM vwОборудование E
    WHERE E.КодОборудования = @КодОборудования AND E.СетевоеИмя <> '' AND E.Списано IS NULL
      AND EXISTS (SELECT 1 FROM МоделиОборудования M
                  WHERE M.КодМоделиОборудования = E.КодМоделиОборудования
                    AND M.КодТипаОборудования = 28)), '');
IF @СетевоеИмя <> ''
BEGIN
    ;WITH SimКартыВТелефоннойСтанции AS (
        SELECT R.КодОборудования, R.КодРасположения, R.СрокВозврата, R.Примечания
        FROM ОборудованиеРасположение R
        INNER JOIN vwОборудование E ON R.КодОборудования = E.КодОборудования
        INNER JOIN МоделиОборудования M ON E.КодМоделиОборудования = M.КодМоделиОборудования
        WHERE E.Списано IS NULL AND M.КодТипаОборудования = 27
          AND R.Примечания LIKE (@СетевоеИмя + '%')
          AND NOT EXISTS (SELECT 1 FROM ОборудованиеРасположение X
                          WHERE X.КодОборудования = R.КодОборудования AND X.От > R.От)
    )
    INSERT ОборудованиеРасположение (КодОборудования, От, КодРасположения, СрокВозврата, Примечания)
    SELECT S.КодОборудования, @От, @КодРасположения, S.СрокВозврата, S.Примечания
    FROM SimКартыВТелефоннойСтанции S
    WHERE S.КодРасположения <> @КодРасположения;
END";

    /// <summary>Обновление срока возврата и примечаний расположения с исходной версией строки.</summary>
    public const string UPDATE_ОборудованиеРасположение = @"
SET XACT_ABORT ON;
BEGIN TRANSACTION;
UPDATE ОборудованиеРасположение
SET СрокВозврата = @СрокВозврата, Примечания = @Примечания
WHERE КодОборудования = @КодОборудования
  AND От = @От
  AND Изменено = @ИсходноеИзменено;
IF @@ROWCOUNT <> 1
BEGIN
    ROLLBACK TRANSACTION;
    RAISERROR('Период расположения был изменён или удалён другим пользователем.', 16, 1);
    RETURN;
END;
COMMIT TRANSACTION;";

    /// <summary>Добавление ответственного сотрудника.</summary>
    public const string INSERT_ОборудованиеСотрудников = @"
INSERT ОборудованиеСотрудников (КодОборудования, От, КодСотрудника, СрокВозврата, Примечания)
VALUES (@КодОборудования, @От, @КодСотрудника, @СрокВозврата, @Примечания)";

    /// <summary>История ответственных сотрудников. Параметр: EquipmentId.</summary>
    public const string SELECT_ОборудованиеСотрудники = @"
SELECT R.КодОборудования, -- оборудование
       R.От,              -- начало периода UTC
       R.До,              -- окончание периода UTC
       R.КодСотрудника,   -- ответственный сотрудник
       E.Сотрудник,       -- ФИО сотрудника
       R.СрокВозврата,    -- срок возврата
       R.Примечания,      -- примечания
       R.Изменил,         -- идентификатор изменившего
       CH.ФИО AS ИзменилФИО, -- ФИО изменившего
       R.Изменено         -- время изменения UTC
FROM ОборудованиеСотрудников R
LEFT JOIN Сотрудники E ON E.КодСотрудника = R.КодСотрудника
LEFT JOIN Сотрудники CH ON CH.КодСотрудника = R.Изменил
WHERE R.КодОборудования = @EquipmentId
ORDER BY R.От";

    /// <summary>Добавление ответственного при перемещении оборудования «На руках».</summary>
    public const string INSERT_ОборудованиеСотрудникаИзРасположения = @"
INSERT ОборудованиеСотрудников (КодОборудования, От, КодСотрудника, СрокВозврата, Примечания)
VALUES (@КодОборудования, @От, @КодСотрудника, NULL, N'')";

    /// <summary>Закрытие только ожидаемого открытого периода ответственности.</summary>
    public const string UPDATE_ОборудованиеСотрудников_Закрытие = @"
UPDATE ОборудованиеСотрудников
SET До = @До
WHERE КодОборудования = @КодОборудования
  AND От = @ТекущийОт
  AND КодСотрудника = @ТекущийКодСотрудника
  AND Изменено = @ТекущееИзменено
  AND До IS NULL";

    /// <summary>Обновление срока возврата и примечаний с исходной версией ответственного.</summary>
    public const string UPDATE_ОборудованиеСотрудников = @"
SET XACT_ABORT ON;
BEGIN TRANSACTION;
UPDATE ОборудованиеСотрудников
SET СрокВозврата = @СрокВозврата, Примечания = @Примечания
WHERE КодОборудования = @КодОборудования
  AND От = @От
  AND Изменено = @ИсходноеИзменено;
IF @@ROWCOUNT <> 1
BEGIN
    ROLLBACK TRANSACTION;
    RAISERROR('Период ответственности был изменён или удалён другим пользователем.', 16, 1);
    RETURN;
END;
COMMIT TRANSACTION;";

    /// <summary>Учётные записи оборудования без паролей. Параметр: EquipmentId.</summary>
    public const string SELECT_ОборудованиеУчётныеЗаписи = @"
SELECT КодОборудования,   -- оборудование
       Логин,             -- логин
       ОграниченныеПрава, -- признак ограниченных прав
       Примечание         -- примечание
FROM vwОборудованиеУчётныеЗаписи
WHERE КодОборудования = @EquipmentId
ORDER BY Логин";

    /// <summary>Пароль одной учётной записи. Параметры: EquipmentId, Login.</summary>
    public const string SELECT_ОборудованиеУчётнаяЗаписьПароль = @"
SELECT Пароль -- секрет, возвращаемый только отдельным запросом
FROM vwОборудованиеУчётныеЗаписи
WHERE КодОборудования = @EquipmentId
  AND Логин = @Login";

    /// <summary>Добавление учётной записи оборудования.</summary>
    public const string INSERT_ОборудованиеУчётныеЗаписи = @"
INSERT ОборудованиеУчётныеЗаписи (КодОборудования, Логин, Пароль, ОграниченныеПрава, Примечание)
VALUES (@КодОборудования, @Логин, @Пароль, @ОграниченныеПрава, @Примечание)";

    /// <summary>Обновление учётной записи по составному ключу.</summary>
    public const string UPDATE_ОборудованиеУчётныеЗаписи = @"
UPDATE ОборудованиеУчётныеЗаписи
SET Пароль = @Пароль,             -- новый пароль
    ОграниченныеПрава = @ОграниченныеПрава, -- признак ограниченных прав
    Примечание = @Примечание      -- примечание
WHERE КодОборудования = @КодОборудования
  AND Логин = @Логин";

    /// <summary>Удаление учётной записи по составному ключу.</summary>
    public const string DELETE_ОборудованиеУчётныеЗаписи = @"
DELETE ОборудованиеУчётныеЗаписи
WHERE КодОборудования = @КодОборудования
  AND Логин = @Логин";

    /// <summary>Фактический тип и признаки дочерних секций сохранённого оборудования. Параметр: EquipmentId.</summary>
    public const string SELECT_ОборудованиеТипДляДочернихЗаписей = @"
SELECT T.КодТипаОборудования, -- фактический тип модели
       T.ЕстьMACадрес, -- максимальное количество MAC-адресов
       T.ЕстьХарактеристикиКомпьютера, -- признак характеристик компьютера
       T.ЕстьХарактеристикиSIM -- признак характеристик SIM
FROM vwОборудование E
INNER JOIN МоделиОборудования M ON E.КодМоделиОборудования = M.КодМоделиОборудования
INNER JOIN ТипыОборудования T ON M.КодТипаОборудования = T.КодТипаОборудования
WHERE E.КодОборудования = @EquipmentId";

    /// <summary>Фактические тип, модель и число банков памяти сохранённого компьютера. Параметр: EquipmentId.</summary>
    public const string SELECT_КомпьютерКонтекстСохранения = @"
SELECT E.КодМоделиОборудования AS ModelId, -- фактическая модель оборудования
       T.КодТипаОборудования AS EquipmentTypeId, -- фактический тип модели
       T.ЕстьХарактеристикиКомпьютера AS HasComputerCharacteristics, -- признак компьютера
       CONVERT(int, M.БанковПамяти) AS MemoryBanks -- число банков памяти модели
FROM vwОборудование E
INNER JOIN МоделиОборудования M ON E.КодМоделиОборудования = M.КодМоделиОборудования
INNER JOIN ТипыОборудования T ON M.КодТипаОборудования = T.КодТипаОборудования
WHERE E.КодОборудования = @EquipmentId";

    /// <summary>История конфигурации компьютера. Параметр: EquipmentId.</summary>
    public const string SELECT_Компьютеры_КодОборудования = @"
SELECT C.КодОборудования, -- оборудование
       C.С, -- дата снимка
       C.RAM, -- общий объём RAM, Мб
       C.RAMDetail, -- компоненты RAM
       C.HD, -- общий объём HD, Гб
       C.HDDetail, -- компоненты HD
       C.SSD, -- общий объём SSD, Гб
       C.SSDDetail, -- компоненты SSD
       C.КодСтандартнойКонфигурации1, -- первая конфигурация
       C.ДатаСтандартнойКонфигурации1, -- дата первой конфигурации
       C.КодСтандартнойКонфигурации2, -- вторая конфигурация
       C.ДатаСтандартнойКонфигурации2, -- дата второй конфигурации
       C.Лицензия, -- лицензия
       C.Примечания, -- примечания
       C.Изменил, -- изменивший сотрудник
       CH.ФИО AS ИзменилФИО, -- ФИО изменившего
       C.Изменено, -- время изменения
       C1.СтандартнаяКонфигурация AS Конфигурация1, -- название первой конфигурации
       C2.СтандартнаяКонфигурация AS Конфигурация2 -- название второй конфигурации
FROM Компьютеры C
LEFT JOIN Сотрудники CH ON CH.КодСотрудника = C.Изменил
LEFT JOIN СтандартныеКонфигурации C1 ON C1.КодСтандартнойКонфигурации = C.КодСтандартнойКонфигурации1
LEFT JOIN СтандартныеКонфигурации C2 ON C2.КодСтандартнойКонфигурации = C.КодСтандартнойКонфигурации2
WHERE C.КодОборудования = @EquipmentId
ORDER BY C.С";

    /// <summary>Справочник стандартных конфигураций.</summary>
    public const string SELECT_СтандартныеКонфигурации = @"
SELECT КодСтандартнойКонфигурации, -- идентификатор
       СтандартнаяКонфигурация -- название
FROM СтандартныеКонфигурации
ORDER BY СтандартнаяКонфигурация";

    /// <summary>Добавление нового или обновление исходного дневного снимка компьютера.</summary>
    public const string UPDATE_Компьютеры = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF @ОжидаетсяСуществующий = 1
    BEGIN
        UPDATE Компьютеры
        SET RAM = @RAM, -- общий объём RAM, Мб
            RAMDetail = @RAMDetail, -- компоненты RAM
            HD = @HD, -- общий объём HD, Гб
            HDDetail = @HDDetail, -- компоненты HD
            SSD = @SSD, -- общий объём SSD, Гб
            SSDDetail = @SSDDetail, -- компоненты SSD
            КодСтандартнойКонфигурации1 = @КодСтандартнойКонфигурации1, -- первая конфигурация
            ДатаСтандартнойКонфигурации1 = @ДатаСтандартнойКонфигурации1, -- дата первой конфигурации
            КодСтандартнойКонфигурации2 = @КодСтандартнойКонфигурации2, -- вторая конфигурация
            ДатаСтандартнойКонфигурации2 = @ДатаСтандартнойКонфигурации2, -- дата второй конфигурации
            Лицензия = @Лицензия, -- лицензия
            Примечания = @Примечания -- примечания
        WHERE КодОборудования = @КодОборудования
          AND С = @С
          AND Изменено = @ИсходноеИзменено;
        IF @@ROWCOUNT <> 1
            RAISERROR('Конфигурация компьютера была изменена или удалена другим пользователем.', 16, 1);
    END
    ELSE
    BEGIN
        IF EXISTS (SELECT 1 FROM Компьютеры WITH (UPDLOCK, HOLDLOCK)
                   WHERE КодОборудования = @КодОборудования AND С = @С)
            RAISERROR('Конфигурация компьютера за этот день уже добавлена другим пользователем.', 16, 1);
        INSERT Компьютеры (
            КодОборудования, -- оборудование
            С, -- дата снимка
            RAM, -- общий объём RAM, Мб
            RAMDetail, -- компоненты RAM
            HD, -- общий объём HD, Гб
            HDDetail, -- компоненты HD
            SSD, -- общий объём SSD, Гб
            SSDDetail, -- компоненты SSD
            КодСтандартнойКонфигурации1, -- первая конфигурация
            ДатаСтандартнойКонфигурации1, -- дата первой конфигурации
            КодСтандартнойКонфигурации2, -- вторая конфигурация
            ДатаСтандартнойКонфигурации2, -- дата второй конфигурации
            Лицензия, -- лицензия
            Примечания -- примечания
        )
        VALUES (@КодОборудования, @С, @RAM, @RAMDetail, @HD, @HDDetail, @SSD, @SSDDetail,
                @КодСтандартнойКонфигурации1, @ДатаСтандартнойКонфигурации1,
                @КодСтандартнойКонфигурации2, @ДатаСтандартнойКонфигурации2, @Лицензия, @Примечания);
    END;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @ErrorMessage nvarchar(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity int = ERROR_SEVERITY();
    DECLARE @ErrorState int = ERROR_STATE();
    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;";

    /// <summary>Сведения SIM без PIN/PUK. Параметр: EquipmentId.</summary>
    public const string SELECT_SIMКарты_КодОборудования = @"
SELECT S.КодОборудования, -- оборудование
       S.НомерТелефона, -- номер телефона
       S.От, -- активация UTC
       S.КодДоговора, -- договор связи
       D.КодЛицаИсполнителя, -- лицо исполнителя
       ISNULL(D.Исполнитель + ' - ' + D.Заказчик + ISNULL(' №' + NULLIF(D.НомерДокумента, ''), ''), '') AS Договор, -- договор
       S.КодЛицаПерсональныхДанных, -- лицо ПДн
       COALESCE(E.Сотрудник, C.Кличка) AS ЛицоПерсональныхДанных, -- название лица ПДн
       S.Изменил, -- изменивший сотрудник
       ISNULL(CH.ФИО, '') AS ИзменилФИО, -- ФИО изменившего
       S.Изменено -- время изменения
FROM vwSIMКарты S
LEFT JOIN ДоговорыСвязи D ON S.КодДоговора = D.КодДокумента
LEFT JOIN ЛицаЗаказчики C ON S.КодЛицаПерсональныхДанных = C.КодЛица
LEFT JOIN Сотрудники E ON S.КодЛицаПерсональныхДанных = E.КодЛица
LEFT JOIN Сотрудники CH ON S.Изменил = CH.КодСотрудника
WHERE S.КодОборудования = @EquipmentId";

    /// <summary>Данные SIM и договора для доступной текущему пользователю детализации. Параметр: EquipmentId.</summary>
    public const string SELECT_SIMКарта_Детализация = @"
SELECT S.КодОборудования, -- оборудование
       S.НомерТелефона, -- номер телефона
       S.КодДоговора, -- идентификатор договора
       D.КодЛицаИсполнителя, -- актуальный оператор договора
       ISNULL(D.Исполнитель + ' - ' + D.Заказчик + ISNULL(' №' + NULLIF(D.НомерДокумента, ''), ''), '') AS Договор, -- договор
       ISNULL(U.ОкруглениеСуммы, 2) AS ОкруглениеСуммы, -- округление суммы
       ISNULL(U.СтавкаНДС, 1) AS СтавкаНДС, -- ставка НДС
       U.НДСВТарификации, -- НДС включён в тарификацию
       U.НДСНаСумму, -- НДС начисляется на общую сумму
       CONVERT(tinyint, CASE WHEN EXISTS (SELECT 1 FROM fn_ТекущиеРоли()
                                          WHERE КодРоли IN (41, 42) AND КодЛица IN (0, D.КодЛицаЗаказчика))
                             THEN 1 ELSE 0 END) AS Подчинённый -- доступ по роли без внешней БД тарификации
FROM vwSIMКарты S
LEFT JOIN ДоговорыСвязи D ON S.КодДоговора = D.КодДокумента
LEFT JOIN УсловияДоговоровСвязи U ON S.КодДоговора = U.КодДоговора
WHERE S.КодОборудования = @EquipmentId";

    /// <summary>Признак наличия внешней БД тарификации на текущем SQL Server.</summary>
    public const string SELECT_SIMДетализация_ЕстьТарификация = @"
SELECT CASE WHEN DB_ID(N'Тарификация') IS NULL THEN 0 ELSE 1 END; -- внешняя БД доступна в текущем стенде";

    /// <summary>Проверка детализации по подчинённым сотрудникам во внешней БД. Параметр: ContractId.</summary>
    public const string SELECT_SIMДетализация_Подчинённый = @"
SELECT CASE WHEN EXISTS (SELECT 1 FROM Тарификация.dbo.vwТарификацияСотовыхИтоги T
                         INNER JOIN dbo.fn_Подчинённые() P ON T.КодСотрудника = P.КодСотрудника
                         WHERE T.КодДоговора = @ContractId)
            THEN 1 ELSE 0 END; -- доступ по подчинённым сотрудникам";

    /// <summary>SIM по идентификатору без секретов. Параметр: EquipmentId.</summary>
    public const string SELECT_SIMКарта_Одна = @"
SELECT S.КодОборудования, -- оборудование
       S.НомерТелефона, -- номер телефона
       S.От, -- активация UTC
       S.КодДоговора, -- договор связи
       S.КодЛицаПерсональныхДанных, -- лицо ПДн
       D.КодЛицаИсполнителя -- лицо исполнителя
FROM vwSIMКарты S
LEFT JOIN ДоговорыСвязи D ON S.КодДоговора = D.КодДокумента
WHERE S.КодОборудования = @EquipmentId";

    /// <summary>Секретные коды одной SIM. Параметр: EquipmentId.</summary>
    public const string SELECT_SIMКарта_Секреты = @"
SELECT PIN, -- PIN
       PIN2, -- PIN2
       PUK, -- PUK
       PUK2 -- PUK2
FROM vwSIMКарты
WHERE КодОборудования = @EquipmentId";

    /// <summary>Другая активная SIM с тем же номером. Параметры: EquipmentId, PhoneNumber.</summary>
    public const string SELECT_SimКарты_Другая_С_Тем_Же_Номером = @"
SELECT TOP 1 S.КодОборудования -- оборудование дубликата
FROM vwSIMКарты S
WHERE S.НомерТелефона = @PhoneNumber
  AND S.КодОборудования <> @EquipmentId
  AND EXISTS (SELECT 1 FROM vwОборудование E
              WHERE E.КодОборудования = S.КодОборудования AND E.Списано IS NULL)";

    /// <summary>Добавление или обновление SIM.</summary>
    public const string UPDATE_SIMКарты = @"
IF EXISTS (SELECT 1 FROM vwSIMКарты WHERE КодОборудования = @КодОборудования)
    UPDATE vwSIMКарты
    SET НомерТелефона = @НомерТелефона, -- номер телефона
        От = @От, -- активация UTC
        КодДоговора = @КодДоговора, -- договор
        КодЛицаПерсональныхДанных = @КодЛицаПерсональныхДанных, -- лицо ПДн
        PIN = @PIN, -- PIN
        PIN2 = @PIN2, -- PIN2
        PUK = @PUK, -- PUK
        PUK2 = @PUK2 -- PUK2
    WHERE КодОборудования = @КодОборудования
ELSE
    INSERT vwSIMКарты (
        КодОборудования, -- оборудование
        НомерТелефона, -- номер телефона
        От, -- активация UTC
        КодДоговора, -- договор
        КодЛицаПерсональныхДанных, -- лицо ПДн
        PIN, -- PIN
        PIN2, -- PIN2
        PUK, -- PUK
        PUK2 -- PUK2
    )
    VALUES (@КодОборудования, @НомерТелефона, @От, @КодДоговора, @КодЛицаПерсональныхДанных,
            @PIN, @PIN2, @PUK, @PUK2)";

    /// <summary>Удаление только сведений SIM. Параметр: EquipmentId.</summary>
    public const string DELETE_ID_SIMКарты = @"
DELETE vwSIMКарты
WHERE КодОборудования = @EquipmentId";

    /// <summary>Незавершённые договоры мобильной связи. Параметры: Search, LimitPlusOne.</summary>
    public const string SELECT_ДоговорыМобильнойСвязи = @"
SELECT TOP (@LimitPlusOne)
       КодДокумента, -- идентификатор договора
       Исполнитель + ' - ' + Заказчик + ISNULL(' №' + NULLIF(НомерДокумента, ''), '') AS ДоговорНазвание, -- название
       КодЛицаЗаказчика, -- заказчик
       COALESCE((SELECT P.ТипЛица FROM vwЛица P WHERE P.КодЛица = ДоговорыСвязи.КодЛицаЗаказчика), 1) AS ТипЛицаЗаказчика -- фактический тип лица заказчика
FROM ДоговорыСвязи
WHERE Сотовая = 1
  AND РаботаЗавершена = 0
  AND (@Search = '' OR Исполнитель LIKE '%' + @Search + '%' OR Заказчик LIKE '%' + @Search + '%'
       OR НомерДокумента LIKE '%' + @Search + '%')
ORDER BY Исполнитель, Заказчик, НомерДокумента";

    /// <summary>Договор мобильной связи по идентификатору. Параметр: Id.</summary>
    public const string SELECT_ДоговорМобильнойСвязиПоИдентификатору = @"
SELECT КодДокумента, -- идентификатор договора
       Исполнитель + ' - ' + Заказчик + ISNULL(' №' + NULLIF(НомерДокумента, ''), '') AS ДоговорНазвание, -- название
       КодЛицаЗаказчика, -- заказчик
       COALESCE((SELECT P.ТипЛица FROM vwЛица P WHERE P.КодЛица = ДоговорыСвязи.КодЛицаЗаказчика), 1) AS ТипЛицаЗаказчика -- фактический тип лица заказчика
FROM ДоговорыСвязи
WHERE Сотовая = 1
  AND КодДокумента = @Id";

    /// <summary>Лица ПДн по договору, оборудованию и техническим тарифам.</summary>
    public const string SELECT_ЛицаПерсональныхДанных = @"
;WITH ТарифныеПланы(КодТипаДопУслугиСотОператора) AS (
    SELECT word FROM dbo.fn_SplitText(@TechnicalTariffs)
),
Договора(КодДоговора, КодЛицаЗаказчика, Заказчик) AS (
    SELECT КодДокумента, КодЛицаЗаказчика, Заказчик
    FROM ДоговорыСвязи
    WHERE (@ContractId IS NULL OR КодДокумента = @ContractId)
      AND КодЛицаЗаказчика IS NOT NULL
),
SIMКартыДопУслуги(НомерТелефона) AS (
    SELECT U.НомерТелефона
    FROM vwSIMКартыДопУслугиСотОператора U
    WHERE EXISTS (SELECT 1 FROM ТарифныеПланы T
                  WHERE U.КодТипаДопУслугиСотОператора LIKE T.КодТипаДопУслугиСотОператора + '%')
      AND U.От < GETDATE()
      AND (U.До IS NULL OR U.До > GETDATE())
),
СотрудникиНаДолжности(КодЛица, Сотрудник) AS (
    SELECT E.КодЛица, E.Сотрудник
    FROM vwДолжности P
    INNER JOIN Сотрудники E ON P.КодСотрудника = E.КодСотрудника
    WHERE P.КодЛица IN (SELECT КодЛицаЗаказчика FROM Договора)
      AND E.КодЛица IS NOT NULL
)
SELECT TOP (@LimitPlusOne) T0.КодЛица, T0.Лицо AS Кличка, T0.Компания
FROM (
    SELECT КодЛица, Сотрудник AS Лицо, 0 AS Компания FROM СотрудникиНаДолжности
    UNION
    SELECT D.КодЛицаЗаказчика, D.Заказчик, 1
    FROM Договора D
    WHERE EXISTS (
        SELECT 1 FROM vwSIMКарты S
        WHERE (@EquipmentId IS NULL OR S.КодОборудования = @EquipmentId)
          AND S.КодДоговора = D.КодДоговора
          AND EXISTS (SELECT 1 FROM SIMКартыДопУслуги U WHERE U.НомерТелефона = S.НомерТелефона)
          AND EXISTS (SELECT 1 FROM vwОборудование E
                      WHERE E.КодОборудования = S.КодОборудования AND E.Списано IS NULL)
    )
    UNION
    SELECT КодЛицаЗаказчика, Заказчик, 0 FROM Договора WHERE @CustomerType = 2
) T0
WHERE @Search = '' OR T0.Лицо LIKE '%' + @Search + '%'
ORDER BY T0.Компания DESC, T0.Лицо";

    /// <summary>Read-only предупреждение ПДн. Параметры: EquipmentId, ContractId, PersonId, TechnicalTariffs, BusinessToday.</summary>
    public const string SELECT_ЛицоПерсональныхДанныхСоответствие = @"
;WITH ТарифныеПланы(КодТипаДопУслугиСотОператора) AS (
    SELECT word -- шаблон технического тарифа
    FROM dbo.fn_SplitText(@TechnicalTariffs)
),
АктивныеТарифы(НомерТелефона) AS (
    SELECT U.НомерТелефона -- номер с активным техническим тарифом
    FROM vwSIMКартыДопУслугиСотОператора U
    WHERE EXISTS (SELECT 1 FROM ТарифныеПланы T
                  WHERE U.КодТипаДопУслугиСотОператора LIKE T.КодТипаДопУслугиСотОператора + '%')
      AND U.От < DATEADD(day, 1, @BusinessToday)
      AND (U.До IS NULL OR U.До > @BusinessToday)
)
SELECT CASE
    WHEN D.КодДокумента IS NULL THEN -1 -- договор не найден
    WHEN @PersonId = D.КодЛицаЗаказчика
         AND COALESCE((SELECT P.ТипЛица FROM vwЛица P WHERE P.КодЛица = D.КодЛицаЗаказчика), 1) = 2 THEN 1 -- физлицо-заказчик
    WHEN @PersonId = D.КодЛицаЗаказчика AND EXISTS (
        SELECT 1 FROM vwSIMКарты S
        WHERE S.КодДоговора = D.КодДокумента
          AND (@EquipmentId IS NULL OR S.КодОборудования = @EquipmentId)
          AND EXISTS (SELECT 1 FROM АктивныеТарифы U WHERE U.НомерТелефона = S.НомерТелефона)
          AND EXISTS (SELECT 1 FROM vwОборудование E
                      WHERE E.КодОборудования = S.КодОборудования AND E.Списано IS NULL)) THEN 1 -- заказчик с техническим тарифом
    WHEN EXISTS (
        SELECT 1 FROM vwДолжности P
        INNER JOIN Сотрудники E ON P.КодСотрудника = E.КодСотрудника
        WHERE P.КодЛица = D.КодЛицаЗаказчика AND E.КодЛица = @PersonId) THEN 1 -- сотрудник заказчика
    ELSE 0 -- соответствие не подтверждено
END AS Eligibility
FROM (SELECT @ContractId AS КодДокумента) Requested
LEFT JOIN ДоговорыСвязи D ON D.КодДокумента = Requested.КодДокумента";

    /// <summary>Лицо ПДн по идентификатору. Параметр: Id.</summary>
    public const string SELECT_ЛицоПерсональныхДанныхПоИдентификатору = @"
SELECT P.КодЛица, -- идентификатор лица
       COALESCE(E.Сотрудник, C.Кличка) AS Кличка -- отображаемое название
FROM (SELECT @Id AS КодЛица) P
LEFT JOIN Сотрудники E ON P.КодЛица = E.КодЛица
LEFT JOIN ЛицаЗаказчики C ON P.КодЛица = C.КодЛица";

    /// <summary>Лицо сотрудника. Параметр: EmployeeId.</summary>
    public const string SELECT_ЛицоСотрудника = @"
SELECT КодЛица -- идентификатор лица
FROM Сотрудники
WHERE КодСотрудника = @EmployeeId";

    /// <summary>Дополнительные услуги текущей SIM. Параметры: EquipmentId, ShowHistory, ShowRequired, BusinessToday.</summary>
    public const string SELECT_SIMКартыДопУслугиСотОператора_КодОборудования = @"
SELECT U.КодSIMКартыДопУслугиСотОператора, -- идентификатор записи
       U.НомерТелефона, -- номер телефона
       U.КодТипаДопУслугиСотОператора, -- тип услуги
       U.ДопУслугаСотОператора, -- название услуги
       U.От, -- дата подключения
       DATEADD(day, -1, U.До) AS До, -- включительная дата прекращения
       CONVERT(datetime, NULL) AS ОтПлан, -- источник планового подключения в legacy отключён
       CONVERT(datetime, NULL) AS ДоПлан, -- источник планового отключения в legacy отключён
       U.СтоимостьВключения, -- стоимость включения
       U.СтоимостьВдень, -- стоимость в день
       U.СтоимостьВмесяц, -- стоимость в месяц
       CONVERT(bit, CASE WHEN T.КодОбязательнойУслугиДляКомпании IS NULL OR T.КодОбязательнойУслугиДляКомпании < 0
                         THEN 0 ELSE 1 END) AS КодОбязательнойУслугиДляКомпании, -- обязательная услуга
       CONVERT(tinyint, CASE WHEN T.КодОбязательнойУслугиДляКомпании < 0
                             THEN 1 ELSE 0 END) AS УслугуНеобходимоОтключить, -- услуга требует отключения
       ISNULL(R.Описание, '') AS ОбязательнаяУслуга, -- описание обязательной услуги
       T.ТарифныйПлан, -- признак тарифного плана
       U.Изменил, -- идентификатор изменившего
       ISNULL(CH.ФИО, '') AS ИзменилФИО, -- ФИО изменившего
       U.Изменено -- время изменения
FROM vwSIMКартыДопУслугиСотОператора U
LEFT JOIN ТипыДопУслугСотОператора T ON U.КодТипаДопУслугиСотОператора = T.КодТипаДопУслугиСотОператора
LEFT JOIN SIMКартыДопУслугиСотОператораОбязательные R ON T.КодОбязательнойУслугиДляКомпании = R.КодОбязательнойУслугиДляКомпании
LEFT JOIN Сотрудники CH ON U.Изменил = CH.КодСотрудника
WHERE EXISTS (SELECT 1 FROM vwSIMКарты S
              WHERE S.НомерТелефона = U.НомерТелефона AND S.КодОборудования = @EquipmentId)
  AND (@ShowHistory = 1 OR U.До IS NULL OR U.До > @BusinessToday)
  AND (@ShowRequired = 1 OR T.КодОбязательнойУслугиДляКомпании IS NULL OR T.КодОбязательнойУслугиДляКомпании < 0)
ORDER BY T.ТарифныйПлан DESC,
         CASE WHEN U.КодТипаДопУслугиСотОператора = '_BLOCK' THEN 0 ELSE 1 END,
         U.СтоимостьВмесяц DESC, U.ДопУслугаСотОператора";

    /// <summary>Типы дополнительных услуг сотового оператора.</summary>
    public const string SELECT_ТипыДопУслугСотОператора = @"
SELECT КодТипаДопУслугиСотОператора, -- строковый идентификатор
       ДопУслугаСотОператора, -- название
       СтоимостьВключения, -- стоимость включения
       СтоимостьВдень, -- стоимость в день
       СтоимостьВмесяц, -- стоимость в месяц
       ТарифныйПлан -- признак тарифного плана
FROM ТипыДопУслугСотОператора
ORDER BY ДопУслугаСотОператора";

    /// <summary>Добавление или изменение услуги только для исходной актуальной SIM.</summary>
    public const string UPDATE_SIMКартыДопУслугиСотОператора = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF ISNULL(@НомерТелефона, '') = ''
        SELECT @НомерТелефона = НомерТелефона FROM vwSIMКарты WHERE КодОборудования = @КодОборудования;
    IF NOT EXISTS (SELECT 1 FROM vwSIMКарты WITH (UPDLOCK, HOLDLOCK)
                   WHERE КодОборудования = @КодОборудования AND НомерТелефона = @НомерТелефона)
        RAISERROR('SIM или её привязка к оборудованию изменилась. Обновите данные.', 16, 1);
    IF EXISTS (SELECT 1 FROM vwSIMКартыДопУслугиСотОператора
               WHERE КодТипаДопУслугиСотОператора = @КодТипаДопУслугиСотОператора
                 AND ДопУслугаСотОператора = @ДопУслугаСотОператора
                 AND НомерТелефона = @НомерТелефона
                 AND КодSIMКартыДопУслугиСотОператора <> ISNULL(@КодSIMКартыДопУслугиСотОператора, 0)
                 AND @От < ISNULL(До, '20500101')
                 AND От < ISNULL(@До, '20500101'))
        RAISERROR('Такая услуга уже добавлена на пересекающийся период.', 16, 1);
    IF @КодSIMКартыДопУслугиСотОператора IS NOT NULL AND @КодSIMКартыДопУслугиСотОператора <> 0
    BEGIN
        UPDATE vwSIMКартыДопУслугиСотОператора
        SET От = @От, -- дата подключения
            До = @До, -- эксклюзивная дата прекращения
            КодТипаДопУслугиСотОператора = @КодТипаДопУслугиСотОператора, -- тип услуги
            ДопУслугаСотОператора = @ДопУслугаСотОператора, -- название
            СтоимостьВключения = @СтоимостьВключения, -- стоимость включения
            СтоимостьВдень = @СтоимостьВдень, -- стоимость в день
            СтоимостьВмесяц = @СтоимостьВмесяц -- стоимость в месяц
        WHERE КодSIMКартыДопУслугиСотОператора = @КодSIMКартыДопУслугиСотОператора
          AND НомерТелефона = @НомерТелефона
          AND Изменено = @ИсходноеИзменено;
        IF @@ROWCOUNT <> 1
            RAISERROR('Дополнительная услуга SIM была изменена или удалена другим пользователем.', 16, 1);
    END
    ELSE
    BEGIN
        INSERT vwSIMКартыДопУслугиСотОператора (
            НомерТелефона, -- номер телефона
            КодТипаДопУслугиСотОператора, -- тип услуги
            ДопУслугаСотОператора, -- название
            От, -- дата подключения
            До, -- эксклюзивная дата прекращения
            СтоимостьВключения, -- стоимость включения
            СтоимостьВдень, -- стоимость в день
            СтоимостьВмесяц, -- стоимость в месяц
            Сохранено -- время сохранения UTC
        )
        SELECT @НомерТелефона, @КодТипаДопУслугиСотОператора, @ДопУслугаСотОператора, @От, @До,
               @СтоимостьВключения, @СтоимостьВдень, @СтоимостьВмесяц, GETUTCDATE()
        WHERE EXISTS (SELECT 1 FROM vwSIMКарты
                      WHERE КодОборудования = @КодОборудования AND НомерТелефона = @НомерТелефона);
        IF @@ROWCOUNT <> 1
            RAISERROR('Привязка SIM изменилась до добавления услуги.', 16, 1);
    END;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @ErrorMessage nvarchar(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity int = ERROR_SEVERITY();
    DECLARE @ErrorState int = ERROR_STATE();
    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;";

    /// <summary>Лицензия оборудования. Параметр: EquipmentId.</summary>
    public const string SELECT_Soft_КодОборудования = @"
SELECT S.КодОборудования, -- оборудование
       S.КодТипаЛицензирования, -- тип лицензирования
       T.ТипЛицензирования, -- название типа
       S.НомерЛицензиата, -- номер лицензиата
       S.URLПроверкиЛицензии, -- URL проверки
       S.КоличествоСерверов, -- количество серверов
       S.КоличествоКлиентов, -- количество клиентов
       S.ДействуетОт, -- начало действия
       DATEADD(day, -1, S.ДействуетДо) AS ДействуетДо, -- включительное окончание
       S.Дистрибутив, -- дистрибутив
       S.Изменил, -- идентификатор изменившего
       ISNULL(CH.ФИО, '') AS ИзменилФИО, -- ФИО изменившего
       S.Изменено -- время изменения
FROM vwSoft S
INNER JOIN ТипыЛицензирования T ON S.КодТипаЛицензирования = T.КодТипаЛицензирования
LEFT JOIN Сотрудники CH ON S.Изменил = CH.КодСотрудника
WHERE S.КодОборудования = @EquipmentId";

    /// <summary>Типы лицензирования с пояснением.</summary>
    public const string SELECT_ТипыЛицензирования = @"
SELECT КодТипаЛицензирования, -- идентификатор
       ТипЛицензирования + ' (' + Компания + ')' AS Наименование, -- отображаемое название
       Описание, -- пояснение типа лицензии
       Временная, -- временная лицензия
       Перепродажа -- перепродажа
FROM ТипыЛицензирования
ORDER BY ТипЛицензирования, Компания";

    /// <summary>Добавление новой или обновление исходной лицензии.</summary>
    public const string UPDATE_Soft = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF @ОжидаетсяСуществующий = 1
    BEGIN
        UPDATE vwSoft
        SET КодТипаЛицензирования = @КодТипаЛицензирования, -- тип
            НомерЛицензиата = @НомерЛицензиата, -- номер лицензиата
            URLПроверкиЛицензии = @URLПроверкиЛицензии, -- URL проверки
            КоличествоСерверов = @КоличествоСерверов, -- серверы
            КоличествоКлиентов = @КоличествоКлиентов, -- клиенты
            ДействуетОт = @ДействуетОт, -- начало
            ДействуетДо = @ДействуетДо, -- эксклюзивное окончание
            Дистрибутив = @Дистрибутив -- дистрибутив
        WHERE КодОборудования = @КодОборудования
          AND Изменено = @ИсходноеИзменено;
        IF @@ROWCOUNT <> 1
            RAISERROR('Лицензия была изменена или удалена другим пользователем.', 16, 1);
    END
    ELSE
    BEGIN
        IF EXISTS (SELECT 1 FROM vwSoft WITH (UPDLOCK, HOLDLOCK) WHERE КодОборудования = @КодОборудования)
            RAISERROR('Лицензия уже добавлена другим пользователем.', 16, 1);
        INSERT vwSoft (
            КодОборудования, -- оборудование
            КодТипаЛицензирования, -- тип
            НомерЛицензиата, -- номер лицензиата
            URLПроверкиЛицензии, -- URL проверки
            КоличествоСерверов, -- серверы
            КоличествоКлиентов, -- клиенты
            ДействуетОт, -- начало
            ДействуетДо, -- эксклюзивное окончание
            Дистрибутив -- дистрибутив
        )
        VALUES (@КодОборудования, @КодТипаЛицензирования, @НомерЛицензиата, @URLПроверкиЛицензии,
                @КоличествоСерверов, @КоличествоКлиентов, @ДействуетОт, @ДействуетДо, @Дистрибутив);
    END;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @ErrorMessage nvarchar(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity int = ERROR_SEVERITY();
    DECLARE @ErrorState int = ERROR_STATE();
    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;";

    /// <summary>Установленное ПО оборудования. Параметр: EquipmentId.</summary>
    public const string SELECT_ПО_КодОборудования = @"
SELECT U.КодОборудованияПО, -- идентификатор установки
       U.КодОборудования, -- оборудование
       U.КодПО, -- программное обеспечение
       M.МодельОборудования AS НазваниеПО, -- название ПО
       U.От, -- дата установки
       DATEADD(day, -1, U.До) AS До, -- включительная деинсталляция
       CONVERT(tinyint, CASE WHEN U.До IS NULL THEN 0 ELSE 1 END) AS Деинсталировано, -- признак деинсталляции
       U.Установлено, -- установленная версия
       U.Дистрибутив, -- дистрибутив
       U.Примечания, -- примечания
       U.Изменил, -- идентификатор изменившего
       ISNULL(CH.ФИО, '') AS ИзменилФИО, -- ФИО изменившего
       U.Изменено -- время изменения
FROM vwОборудованиеПО U
LEFT JOIN vwОборудование E ON U.КодПО = E.КодОборудования
LEFT JOIN МоделиОборудования M ON E.КодМоделиОборудования = M.КодМоделиОборудования
LEFT JOIN Сотрудники CH ON U.Изменил = CH.КодСотрудника
WHERE U.КодОборудования = @EquipmentId
ORDER BY U.От";

    /// <summary>Программное оборудование для выбора. Параметры: Search, LimitPlusOne.</summary>
    public const string SELECT_ПрограммноеОборудование = @"
SELECT TOP (@LimitPlusOne)
       E.КодОборудования, -- идентификатор ПО
       T.ТипОборудования + ' ' + M.МодельОборудования + ISNULL(' (' + NULLIF(E.SN, '') + ')', '') AS ОтображаемоеНазвание, -- отображаемое название
       M.МодельОборудования -- модель для поля «Установлено»
FROM vwОборудование E
INNER JOIN МоделиОборудования M ON E.КодМоделиОборудования = M.КодМоделиОборудования
INNER JOIN ТипыОборудования T ON M.КодТипаОборудования = T.КодТипаОборудования
WHERE T.КодТипаОборудования > 200
  AND E.Списано IS NULL
  AND (@Search = '' OR T.ТипОборудования LIKE '%' + @Search + '%'
       OR M.МодельОборудования LIKE '%' + @Search + '%' OR E.SN LIKE '%' + @Search + '%')
ORDER BY T.ТипОборудования, M.МодельОборудования";

    /// <summary>Программное оборудование по идентификатору. Параметр: Id.</summary>
    public const string SELECT_ПрограммноеОборудованиеПоИдентификатору = @"
SELECT E.КодОборудования, -- идентификатор ПО
       T.ТипОборудования + ' ' + M.МодельОборудования + ISNULL(' (' + NULLIF(E.SN, '') + ')', '') AS ОтображаемоеНазвание, -- отображаемое название
       M.МодельОборудования -- модель для поля «Установлено»
FROM vwОборудование E
INNER JOIN МоделиОборудования M ON E.КодМоделиОборудования = M.КодМоделиОборудования
INNER JOIN ТипыОборудования T ON M.КодТипаОборудования = T.КодТипаОборудования
WHERE T.КодТипаОборудования > 200
  AND E.КодОборудования = @Id";

    /// <summary>Добавление установленного ПО с эксклюзивной датой деинсталляции.</summary>
    public const string INSERT_ОборудованиеПО = @"
INSERT vwОборудованиеПО (
    КодОборудования, -- оборудование
    КодПО, -- программное обеспечение
    От, -- дата установки
    До, -- эксклюзивная дата деинсталляции
    Установлено, -- установленная версия
    Дистрибутив, -- дистрибутив
    Примечания -- примечания
)
VALUES (@КодОборудования, @КодПО, @От, CASE WHEN @До IS NOT NULL THEN DATEADD(day, 1, @До) ELSE NULL END,
        @Установлено, @Дистрибутив, @Примечания)";

    /// <summary>Обновление исходной установки ПО без переноса на другое оборудование.</summary>
    public const string UPDATE_ID_ОборудованиеПО = @"
SET XACT_ABORT ON;
BEGIN TRANSACTION;
UPDATE vwОборудованиеПО
SET КодПО = @КодПО, -- программное обеспечение
    От = @От, -- дата установки
    До = CASE WHEN @До IS NOT NULL THEN DATEADD(day, 1, @До) ELSE NULL END, -- эксклюзивная деинсталляция
    Установлено = @Установлено, -- установленная версия
    Дистрибутив = @Дистрибутив, -- дистрибутив
    Примечания = @Примечания -- примечания
WHERE КодОборудованияПО = @КодОборудованияПО
  AND КодОборудования = @ИсходныйКодОборудования
  AND Изменено = @ИсходноеИзменено;
IF @@ROWCOUNT <> 1
BEGIN
    ROLLBACK TRANSACTION;
    RAISERROR('Установка ПО была изменена, удалена или принадлежит другому оборудованию.', 16, 1);
    RETURN;
END;
COMMIT TRANSACTION;";

    /// <summary>История бухгалтерского учёта. Параметр: EquipmentId.</summary>
    public const string SELECT_ОборудованиеБухУчета_КодОборудования = @"
SELECT A.КодОборудованияБухУчета, -- идентификатор записи
       A.КодОборудования, -- оборудование
       A.КодЛица AS КодБухгалтерии, -- бухгалтерия-владелец
       ISNULL(B.Кличка, '') AS Бухгалтерия, -- название бухгалтерии
       A.Материал, -- материал или основное средство
       A.КодЛицаАрендатора, -- арендатор
       ISNULL(T.Кличка, '') AS Арендатор, -- название арендатора
       CASE WHEN A.Материал = 1 THEN 'Материал' ELSE A.ИнвентарныйНомер END AS ИнвентарныйНомер, -- номер
       A.От AS ДатаПоступления, -- поступление
       A.КодДокументаПоступления, -- документ поступления
       A.До AS ДатаСписания, -- списание
       A.КодДокументаСписания, -- документ списания
       A.Примечания, -- примечания
       A.Изменил, -- идентификатор изменившего
       ISNULL(CH.ФИО, '') AS ИзменилФИО, -- ФИО изменившего
       A.Изменено -- время изменения
FROM ОборудованиеБухУчета A
LEFT JOIN ЛицаЗаказчики B ON A.КодЛица = B.КодЛица
LEFT JOIN ЛицаЗаказчики T ON A.КодЛицаАрендатора = T.КодЛица
LEFT JOIN Сотрудники CH ON A.Изменил = CH.КодСотрудника
WHERE A.КодОборудования = @EquipmentId
ORDER BY A.От";

    /// <summary>Добавление записи бухгалтерского учёта.</summary>
    public const string INSERT_ОборудованиеБухУчета = @"
INSERT ОборудованиеБухУчета (
    КодОборудования, -- оборудование
    КодЛица, -- бухгалтерия-владелец
    Материал, -- материал или основное средство
    КодЛицаАрендатора, -- арендатор
    ИнвентарныйНомер, -- инвентарный номер
    От, -- дата поступления
    КодДокументаПоступления, -- документ поступления
    До, -- дата списания
    КодДокументаСписания, -- документ списания
    Примечания -- примечания
)
VALUES (@КодОборудования, @КодЛица, @Материал, @КодЛицаАрендатора, @ИнвентарныйНомер,
        @От, @КодДокументаПоступления, @До, @КодДокументаСписания, @Примечания)";

    /// <summary>Атомарно обновляет существующую запись бухучёта по ID, оборудованию, версии, правам обоих владельцев и доступности документов.</summary>
    public const string UPDATE_ID_ОборудованиеБухУчета = @"
SET NOCOUNT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @ActualEquipmentId int = NULL;
    DECLARE @ActualOwnerId int = NULL;
    DECLARE @ActualChangedAt datetime = NULL;
    SELECT @ActualEquipmentId = КодОборудования,
           @ActualOwnerId = КодЛица,
           @ActualChangedAt = Изменено
    FROM ОборудованиеБухУчета WITH (UPDLOCK, HOLDLOCK)
    WHERE КодОборудованияБухУчета = @КодОборудованияБухУчета;
    IF @ActualEquipmentId IS NULL OR @ActualEquipmentId <> @КодОборудования OR @ActualChangedAt <> @ExpectedChangedAt
    BEGIN ROLLBACK TRANSACTION; SELECT 1; RETURN; END; -- запись отсутствует, перенесена или изменена
    IF @ActualOwnerId IS NULL OR dbo.fn_БухгалтерскийУчет(@ActualOwnerId) <> 1
       OR NOT EXISTS (SELECT 1 FROM fn_ТекущиеРоли()
                      WHERE КодРоли IN (@RoleIdPrimary, @RoleIdSecondary)
                        AND КодЛица IN (0, @ActualOwnerId))
    BEGIN ROLLBACK TRANSACTION; SELECT 2; RETURN; END; -- нет права по фактическому владельцу
    IF @КодЛица IS NULL OR dbo.fn_БухгалтерскийУчет(@КодЛица) <> 1
       OR NOT EXISTS (SELECT 1 FROM fn_ТекущиеРоли()
                      WHERE КодРоли IN (@RoleIdPrimary, @RoleIdSecondary)
                        AND КодЛица IN (0, @КодЛица))
    BEGIN ROLLBACK TRANSACTION; SELECT 3; RETURN; END; -- нет права по новому владельцу
    IF (@КодДокументаСписания IS NOT NULL AND NOT EXISTS (
            SELECT 1 FROM [Документы].dbo.vwДокументы WHERE КодДокумента = @КодДокументаСписания))
       OR (@КодДокументаПоступления IS NOT NULL AND NOT EXISTS (
            SELECT 1 FROM [Документы].dbo.vwДокументы WHERE КодДокумента = @КодДокументаПоступления))
    BEGIN ROLLBACK TRANSACTION; SELECT 4; RETURN; END; -- документ недоступен текущему пользователю
    UPDATE ОборудованиеБухУчета
    SET КодОборудования = @КодОборудования, -- оборудование
        КодЛица = @КодЛица, -- бухгалтерия-владелец
        Материал = @Материал, -- материал или основное средство
        КодЛицаАрендатора = @КодЛицаАрендатора, -- арендатор
        ИнвентарныйНомер = @ИнвентарныйНомер, -- инвентарный номер
        От = @От, -- поступление
        КодДокументаПоступления = @КодДокументаПоступления, -- документ поступления
        До = @До, -- списание
        КодДокументаСписания = @КодДокументаСписания, -- документ списания
        Примечания = @Примечания -- примечания
    WHERE КодОборудованияБухУчета = @КодОборудованияБухУчета;
    IF @@ROWCOUNT <> 1
    BEGIN ROLLBACK TRANSACTION; SELECT 5; RETURN; END; -- обновлено нечисло строк
    COMMIT TRANSACTION;
    SELECT 0; -- запись обновлена
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @ErrorMessage nvarchar(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity int = ERROR_SEVERITY();
    DECLARE @ErrorState int = ERROR_STATE();
    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;";

    /// <summary>Атомарно удаляет подтверждённую последнюю запись бухучёта после проверки актуального владельца и ролей без изменения настроек SQL-сессии.</summary>
    public const string DELETE_КодОборудования_ОборудованиеБухУчета = @"
SET NOCOUNT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @LatestId int = NULL; -- актуальный идентификатор последней записи
    DECLARE @ActualOwnerId int = NULL; -- актуальный владелец последней записи
    SELECT TOP 1
           @LatestId = КодОборудованияБухУчета, -- идентификатор записи
           @ActualOwnerId = КодЛица -- бухгалтерия-владелец
    FROM ОборудованиеБухУчета WITH (UPDLOCK, HOLDLOCK)
    WHERE КодОборудования = @КодОборудования
    ORDER BY От DESC, КодОборудованияБухУчета DESC;
    IF @LatestId IS NULL
    BEGIN ROLLBACK TRANSACTION; SELECT 1; RETURN; END; -- история пуста
    IF @LatestId <> @КодОборудованияБухУчета
    BEGIN ROLLBACK TRANSACTION; SELECT 2; RETURN; END; -- подтверждённая запись устарела
    IF @ActualOwnerId IS NULL OR dbo.fn_БухгалтерскийУчет(@ActualOwnerId) <> 1
       OR NOT EXISTS (SELECT 1 FROM fn_ТекущиеРоли()
                      WHERE КодРоли IN (@RoleIdPrimary, @RoleIdSecondary)
                        AND КодЛица IN (0, @ActualOwnerId))
    BEGIN ROLLBACK TRANSACTION; SELECT 3; RETURN; END; -- нет права у актуального владельца
    DELETE ОборудованиеБухУчета
    WHERE КодОборудованияБухУчета = @LatestId
      AND КодОборудования = @КодОборудования;
    COMMIT TRANSACTION;
    SELECT 0; -- запись удалена
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @ErrorMessage nvarchar(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity int = ERROR_SEVERITY();
    DECLARE @ErrorState int = ERROR_STATE();
    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;";

    /// <summary>Лица с бухгалтерским учётом. Параметры: Search, LimitPlusOne.</summary>
    public const string SELECT_ЛицаСБухгалтерскимУчетом = @"
SELECT TOP (@LimitPlusOne)
       КодЛица, -- идентификатор лица
       Кличка -- название
FROM ЛицаЗаказчики
WHERE dbo.fn_БухгалтерскийУчет(КодЛица) = 1
  AND (@Search = '' OR Кличка LIKE '%' + @Search + '%')
ORDER BY Кличка";

    /// <summary>Лицо с бухгалтерским учётом по идентификатору. Параметр: Id.</summary>
    public const string SELECT_ЛицоСБухгалтерскимУчетомПоИдентификатору = @"
SELECT КодЛица, -- идентификатор лица
       Кличка -- название
FROM ЛицаЗаказчики
WHERE dbo.fn_БухгалтерскийУчет(КодЛица) = 1
  AND КодЛица = @Id";

    /// <summary>Документы поступления бухгалтерского учёта. Параметры: Search, PersonId, LimitPlusOne.</summary>
    public const string SELECT_ДокументыПоступленияБухУчета = @"
SELECT TOP (@LimitPlusOne)
       D.КодДокумента, -- идентификатор документа
       D.НазваниеДокумента, -- название
       D.НомерДокумента, -- номер
       D.ДатаДокумента -- дата
FROM vwДокументы D
WHERE D.КодТипаДокумента IN (2079, 2145, 2070, 2071)
  AND (@Search = '' OR D.НазваниеДокумента LIKE '%' + @Search + '%' OR D.НомерДокумента LIKE '%' + @Search + '%')
  AND (@PersonId IS NULL OR EXISTS (SELECT 1 FROM vwЛицаДокументов P
       WHERE P.КодДокумента = D.КодДокумента AND P.КодЛица = @PersonId))
ORDER BY D.ДатаДокумента DESC, D.КодДокумента DESC";

    /// <summary>Документы списания бухгалтерского учёта. Параметры: Search, PersonId, LimitPlusOne.</summary>
    public const string SELECT_ДокументыСписанияБухУчета = @"
SELECT TOP (@LimitPlusOne)
       D.КодДокумента, -- идентификатор документа
       D.НазваниеДокумента, -- название
       D.НомерДокумента, -- номер
       D.ДатаДокумента -- дата
FROM vwДокументы D
INNER JOIN ТипыДокументов DT ON D.КодТипаДокумента = DT.КодТипаДокумента
WHERE (D.КодТипаДокумента IN (2145, 2070)
       OR EXISTS (SELECT 1 FROM ТипыДокументов Parent
                  WHERE Parent.КодТипаДокумента IN (2079, 2338)
                    AND Parent.L <= DT.L AND Parent.R >= DT.R))
  AND (@Search = '' OR D.НазваниеДокумента LIKE '%' + @Search + '%' OR D.НомерДокумента LIKE '%' + @Search + '%')
  AND (@PersonId IS NULL OR EXISTS (SELECT 1 FROM vwЛицаДокументов P
       WHERE P.КодДокумента = D.КодДокумента AND P.КодЛица = @PersonId))
ORDER BY D.ДатаДокумента DESC, D.КодДокумента DESC";

    /// <summary>Документ бухгалтерского учёта по идентификатору. Параметр: Id.</summary>
    public const string SELECT_ДокументБухУчетаПоИдентификатору = @"
SELECT КодДокумента, -- идентификатор документа
       НазваниеДокумента, -- название
       НомерДокумента, -- номер
       ДатаДокумента -- дата
FROM vwДокументы
WHERE КодДокумента = @Id";

    /// <summary>Основные сведения маршрутизатора. Параметр: EquipmentId.</summary>
    public const string SELECT_Routers_КодОборудования = @"
SELECT КодОборудования, -- оборудование
       СетевоеИмя, -- сетевое имя
       ГолосовойШлюз, -- голосовой шлюз
       OSVersion, -- версия ОС
       ProcRAM, -- объём RAM
       ProcRAMfree, -- свободная RAM
       Flash, -- объём Flash
       FlashFree, -- свободная Flash
       BGP, -- номер BGP
       RouterID, -- идентификатор маршрутизатора
       Description -- описание конфигурации
FROM vwRouters
WHERE КодОборудования = @EquipmentId";

    /// <summary>Инвентаризация маршрутизатора. Параметр: EquipmentId.</summary>
    public const string SELECT_КодОборудования_RouterInventory = @"
SELECT Код, -- идентификатор строки
       КодОборудования, -- оборудование
       СетевоеИмя, -- сетевое имя
       Модуль, -- модуль
       Характеристика -- характеристика
FROM vwRoutersInventory
WHERE КодОборудования = @EquipmentId
ORDER BY Код";

    /// <summary>UPS, подключённые к серверу. Параметр: ServerEquipmentId.</summary>
    public const string SELECT_ID_vwПодключенияUPS = @"
SELECT U.КодОборудованияUPS, -- идентификатор UPS
       T.ТипОборудования + ' ' + M.МодельОборудования
       + CASE WHEN E.SN <> '' THEN ' SN ' + E.SN ELSE '' END
       + CASE WHEN E.СетевоеИмя <> '' THEN ' ' + E.СетевоеИмя ELSE '' END
       + CASE WHEN M.PN <> '' THEN ' (' + M.PN + ')' ELSE '' END AS НазваниеОборудования -- отображаемое название
FROM vwПодключенияUPS U
INNER JOIN vwОборудование E ON U.КодОборудованияUPS = E.КодОборудования
INNER JOIN МоделиОборудования M ON E.КодМоделиОборудования = M.КодМоделиОборудования
INNER JOIN ТипыОборудования T ON M.КодТипаОборудования = T.КодТипаОборудования
WHERE U.КодОборудованияСервера = @ServerEquipmentId
ORDER BY E.СетевоеИмя, M.МодельОборудования, U.КодОборудованияUPS";

    /// <summary>Серверы выбранного UPS. Параметр: UpsEquipmentId.</summary>
    public const string SELECT_ID_СервераПодключенияUPS = @"
SELECT U.КодОборудованияUPS, -- идентификатор UPS
       U.КодОборудованияСервера, -- идентификатор сервера
       U.НастроеноВыключение, -- настроенное выключение
       T.КодТипаОборудования, -- тип оборудования сервера
       E.СетевоеИмя AS НазваниеСервера, -- настоящее сетевое имя без display fallback
       T.ТипОборудования + ' ' + M.МодельОборудования
       + CASE WHEN E.SN <> '' THEN ' SN ' + E.SN ELSE '' END
       + CASE WHEN E.СетевоеИмя <> '' THEN ' ' + E.СетевоеИмя ELSE '' END
       + CASE WHEN M.PN <> '' THEN ' (' + M.PN + ')' ELSE '' END AS НазваниеОборудования, -- отображаемое название
       ISNULL(CH.ФИО, '') AS ИзменилФИО, -- ФИО изменившего
       U.Изменено -- время изменения UTC
FROM vwПодключенияUPS U
INNER JOIN vwОборудование E ON U.КодОборудованияСервера = E.КодОборудования
INNER JOIN МоделиОборудования M ON E.КодМоделиОборудования = M.КодМоделиОборудования
INNER JOIN ТипыОборудования T ON M.КодТипаОборудования = T.КодТипаОборудования
LEFT JOIN Сотрудники CH ON U.Изменил = CH.КодСотрудника
WHERE U.КодОборудованияUPS = @UpsEquipmentId
ORDER BY НазваниеСервера, НазваниеОборудования";

    /// <summary>Тесты выбранного UPS. Параметр: UpsEquipmentId.</summary>
    public const string SELECT_ID_ТестыПодключенияUPS = @"
SELECT T.КодТестирования, -- идентификатор тестирования
       T.КодОборудованияUPS, -- идентификатор UPS
       T.ДатаТестирования, -- дата тестирования
       T.ВремяБатарейUPS, -- время батарей
       T.СервераПолучившиеОповещение, -- оповещённые серверы
       T.ОшибкиТестирования, -- ошибки
       ISNULL(CH.ФИО, '') AS ИзменилФИО, -- ФИО изменившего
       T.Изменено -- время изменения
FROM vwПодключенияUPSтестирование T
LEFT JOIN Сотрудники CH ON T.Изменил = CH.КодСотрудника
WHERE T.КодОборудованияUPS = @UpsEquipmentId
ORDER BY T.ДатаТестирования DESC";

    /// <summary>Быстрое подключение UPS к серверу.</summary>
    public const string INSERT_СерверПодключенияUPSОборудование = @"
INSERT vwПодключенияUPS (КодОборудованияUPS, КодОборудованияСервера, НастроеноВыключение)
VALUES (@КодОборудованияUPS, @КодОборудованияСервера, 0)";

    /// <summary>Удаление связи UPS и сервера.</summary>
    public const string DELETE_СерверПодключенияUPS = @"
DELETE vwПодключенияUPS
WHERE КодОборудованияUPS = @КодОборудованияUPS
  AND КодОборудованияСервера = @КодОборудованияСервера";

    /// <summary>Оборудование для связи UPS. Параметры: UpsOnly, LocationId, Search, LimitPlusOne.</summary>
    public const string SELECT_ОборудованиеДляUPS = @"
SELECT TOP (@LimitPlusOne)
       E.КодОборудования, -- идентификатор оборудования
       T.ТипОборудования + ' ' + M.МодельОборудования + ISNULL(' (' + NULLIF(E.SN, '') + ')', '') AS НазваниеОборудования, -- название
       T.КодТипаОборудования, -- тип оборудования
       E.СетевоеИмя AS НазваниеСервера -- сетевое имя сервера
FROM vwОборудование E
INNER JOIN МоделиОборудования M ON E.КодМоделиОборудования = M.КодМоделиОборудования
INNER JOIN ТипыОборудования T ON M.КодТипаОборудования = T.КодТипаОборудования
WHERE E.Списано IS NULL
  AND ((@UpsOnly = 1 AND T.КодТипаОборудования = 53)
       OR (@UpsOnly = 0 AND T.КодТипаОборудования <> 53
           AND T.КодТипаОборудования < 100 AND T.ЕстьХарактеристикиSIM = 0))
  AND (@LocationId IS NULL OR EXISTS (
       SELECT 1
       FROM ОборудованиеРасположение ER
       INNER JOIN vwРасположения ChildLocation ON ChildLocation.КодРасположения = ER.КодРасположения
       INNER JOIN vwРасположения ParentLocation
               ON ParentLocation.L <= ChildLocation.L AND ParentLocation.R >= ChildLocation.R
       WHERE ER.КодОборудования = E.КодОборудования
         AND NOT EXISTS (
             SELECT 1 FROM ОборудованиеРасположение Later
             WHERE Later.КодОборудования = ER.КодОборудования AND Later.От > ER.От)
         AND ParentLocation.Офис = 1
         AND ChildLocation.РабочееМесто <> 4
         AND ParentLocation.КодРасположения IN (
             SELECT Office.КодРасположения
             FROM vwРасположения Office
             INNER JOIN vwРасположения CurrentLocation
                     ON Office.L <= CurrentLocation.L AND Office.R >= CurrentLocation.R
             WHERE CurrentLocation.КодРасположения = @LocationId AND Office.Офис = 1)))
  AND (@Search = '' OR T.ТипОборудования LIKE '%' + @Search + '%'
       OR M.МодельОборудования LIKE '%' + @Search + '%' OR E.SN LIKE '%' + @Search + '%')
ORDER BY T.ТипОборудования, M.МодельОборудования";

    /// <summary>Оборудование для связи UPS по идентификатору. Параметры: Id, UpsOnly.</summary>
    public const string SELECT_ОборудованиеДляUPSПоИдентификатору = @"
SELECT E.КодОборудования, -- идентификатор оборудования
       T.ТипОборудования + ' ' + M.МодельОборудования + ISNULL(' (' + NULLIF(E.SN, '') + ')', '') AS НазваниеОборудования, -- название
       T.КодТипаОборудования, -- тип оборудования
       E.СетевоеИмя AS НазваниеСервера -- сетевое имя сервера
FROM vwОборудование E
INNER JOIN МоделиОборудования M ON E.КодМоделиОборудования = M.КодМоделиОборудования
INNER JOIN ТипыОборудования T ON M.КодТипаОборудования = T.КодТипаОборудования
WHERE E.КодОборудования = @Id
  AND E.Списано IS NULL
  AND ((@UpsOnly = 1 AND T.КодТипаОборудования = 53)
       OR (@UpsOnly = 0 AND T.КодТипаОборудования <> 53
           AND T.КодТипаОборудования < 100 AND T.ЕстьХарактеристикиSIM = 0))";

    /// <summary>Последняя инвентаризация оборудования. Параметр: EquipmentId.</summary>
    public const string SELECT_ID_РеальноеРасположениеОборудования_ПоследнееРасположение = @"
SELECT TOP 1 A.ВремяПроверки, -- время проверки
       A.КодОборудования, -- оборудование
       A.Оборудование, -- название оборудования
       A.КодРасположения, -- фактическое расположение
       ISNULL(L.РасположениеPath1, '') AS РасположениеPath1, -- полный путь расположения
       A.СломаноРемонтуНеПодлежит, -- неремонтопригодность
       A.Примечание, -- примечание
       A.Изменил, -- сотрудник
       ISNULL(CH.ФИО, '') AS ИзменилФИО, -- ФИО сотрудника
       A.Изменено -- время изменения
FROM vwРеальноеРасположениеОборудования A
LEFT JOIN Сотрудники CH ON A.Изменил = CH.КодСотрудника
LEFT JOIN vwРасположения L ON A.КодРасположения = L.КодРасположения
WHERE A.КодОборудования = @EquipmentId
ORDER BY A.ВремяПроверки DESC";

    /// <summary>Состояние подключения рабочей сети. Параметр: EquipmentId.</summary>
    public const string SELECT_ID_ConnectedWorkNet = @"
SELECT Открыто -- счётчик подключений
FROM vwЛокальнаяСетьПрисоединениеРабочейСети
WHERE КодОборудования = @EquipmentId
  AND Открыто <> 0";

    /// <summary>Порты локальной сети, в тексте «Подключено» которых встречается сетевое имя. Параметр: СетевоеИмя.</summary>
    public const string SELECT_NAME_vwЛокальнаяСеть = @"
IF CHARINDEX('\', @СетевоеИмя) > 0
    SET @СетевоеИмя = REVERSE(SUBSTRING(REVERSE(@СетевоеИмя), 1, CHARINDEX('\', REVERSE(@СетевоеИмя)) - 1));

SELECT N.КодЛокальнойСети, -- идентификатор порта
       E.СетевоеИмя, -- сетевое имя оборудования порта
       N.Порт -- номер порта
FROM vwЛокальнаяСеть N
INNER JOIN vwОборудование E ON N.КодОборудования = E.КодОборудования
WHERE (' ' + dbo.fn_SplitWords(N.Подключено) + ' ') LIKE '% ' + @СетевоеИмя + ' %'
   OR (' ' + N.Подключено + ' ') LIKE '% ' + @СетевоеИмя + ' %'
ORDER BY E.СетевоеИмя, N.Порт";

    /// <summary>Подключение рабочей сети.</summary>
    public const string INSERT_Net = @"
INSERT vwЛокальнаяСетьПрисоединениеРабочейСети (КодОборудования)
VALUES (@КодОборудования)";

    /// <summary>Отключение рабочей сети.</summary>
    public const string UPDATE_Net = @"
UPDATE vwЛокальнаяСетьПрисоединениеРабочейСети
SET Открыто = 0
WHERE КодОборудования = @КодОборудования
  AND Открыто > 0";

    /// <summary>Основная запись телефона. Параметр: Id.</summary>
    public const string SELECT_ТелефонДляФормы = @"
SELECT P.КодТелефонногоНомера, -- идентификатор номера
       P.ТелефонныйНомер, -- короткий номер
       P.ПолныйНомер, -- полный номер
       P.КодТипаТелефонныхНомеров, -- тип номера
       P.КодТелефоннойСтанции, -- телефонная станция
       P.ПортАТС, -- порт АТС
       P.ВедомыйОт, -- ведущий номер
       P.ОтображаемыйОт, -- отображаемый номер
       P.КодРозетки, -- розетка
       P.КодСотрудника, -- сотрудник
       P.КодЛицаЗаказчика, -- заказчик
       P.Подключено, -- текст подключения
       P.ОписаниеАТС, -- описание АТС
       P.ОсновнойНомер, -- основной номер сотрудника
       P.СрокВозврата, -- срок возврата
       P.УслугиСвязи, -- услуги связи
       P.ВСправочнике, -- отображение в справочнике
       P.Группа6, -- группа перехвата
       P.БыстрыйНабор, -- переадресация
       P.НомерАвтонабора, -- номер автонабора
       P.Примечания, -- примечания
       P.Изменил, -- изменивший сотрудник
       ISNULL(E.ФИО, '#' + CONVERT(varchar, P.Изменил)) AS ИзменилФИО, -- имя изменившего
       P.Изменено -- время изменения UTC
FROM vwТелефонныеНомераИзменение P
LEFT JOIN Сотрудники E ON E.КодСотрудника = P.Изменил
WHERE P.КодТелефонногоНомера = @Id";

    /// <summary>Обновление 20 полей телефона. Параметры соответствуют PhoneSaveBuilder.</summary>
    public const string UPDATE_ТелефонДляФормы = @"
UPDATE vwТелефонныеНомераИзменение
SET ТелефонныйНомер = @ТелефонныйНомер, -- короткий номер
    ПолныйНомер = @ПолныйНомер, -- предварительный полный номер
    КодТипаТелефонныхНомеров = @КодТипаТелефонныхНомеров, -- тип номера
    КодТелефоннойСтанции = @КодТелефоннойСтанции, -- станция
    ПортАТС = @ПортАТС, -- порт
    ВедомыйОт = @ВедомыйОт, -- ведущий номер
    ОтображаемыйОт = @ОтображаемыйОт, -- отображаемый номер
    КодРозетки = @КодРозетки, -- розетка
    КодСотрудника = @КодСотрудника, -- сотрудник
    КодЛицаЗаказчика = @КодЛицаЗаказчика, -- заказчик
    Подключено = @Подключено, -- подключение
    ОписаниеАТС = @ОписаниеАТС, -- описание АТС
    ОсновнойНомер = @ОсновнойНомер, -- основной номер
    СрокВозврата = @СрокВозврата, -- срок возврата
    УслугиСвязи = @УслугиСвязи, -- услуги связи
    ВСправочнике = @ВСправочнике, -- справочник
    Группа6 = @Группа6, -- группа перехвата
    БыстрыйНабор = @БыстрыйНабор, -- переадресация
    НомерАвтонабора = @НомерАвтонабора, -- автонабор
    Примечания = @Примечания -- примечания
WHERE КодТелефонногоНомера = @Id";

    /// <summary>Добавление 20 полей телефона с сохранением @@IDENTITY. Параметры соответствуют PhoneSaveBuilder.</summary>
    public const string INSERT_ТелефонДляФормы = @"
INSERT INTO vwТелефонныеНомераИзменение
    (ТелефонныйНомер, ПолныйНомер, КодТипаТелефонныхНомеров, КодТелефоннойСтанции,
     ПортАТС, ВедомыйОт, ОтображаемыйОт, КодРозетки, КодСотрудника, КодЛицаЗаказчика,
     Подключено, ОписаниеАТС, ОсновнойНомер, СрокВозврата, УслугиСвязи, ВСправочнике,
     Группа6, БыстрыйНабор, НомерАвтонабора, Примечания)
VALUES (@ТелефонныйНомер, @ПолныйНомер, @КодТипаТелефонныхНомеров, @КодТелефоннойСтанции,
        @ПортАТС, @ВедомыйОт, @ОтображаемыйОт, @КодРозетки, @КодСотрудника, @КодЛицаЗаказчика,
        @Подключено, @ОписаниеАТС, @ОсновнойНомер, @СрокВозврата, @УслугиСвязи, @ВСправочнике,
        @Группа6, @БыстрыйНабор, @НомерАвтонабора, @Примечания);
SET @PhoneId = CONVERT(int, @@IDENTITY); -- идентификатор новой записи после триггеров представления";

    /// <summary>Члены PBX-группы. Параметр: Id.</summary>
    public const string SELECT_ТелефонЧленыГруппы = @"
SELECT G.КодТелефонногоНомераГруппы, -- идентификатор группы
       G.КодТелефонногоНомераЧленаГруппы, -- идентификатор члена
       P.ПолныйНомер + CASE WHEN P.ПолныйНомер <> P.ТелефонныйНомер THEN ' (' + P.ТелефонныйНомер + ')' ELSE '' END
           + ' ' + CASE WHEN P.ОписаниеАТС = '' THEN P.Подключено ELSE P.ОписаниеАТС END AS Название, -- отображаемое имя
       ISNULL(E.ФИО, '#' + CONVERT(varchar, G.Изменил)) AS Изменил, -- изменивший сотрудник
       G.Изменено -- время изменения
FROM ТелефонныеНомераГруппы G
INNER JOIN vwТелефонныеНомераИзменение P ON P.КодТелефонногоНомера = G.КодТелефонногоНомераЧленаГруппы
LEFT JOIN Сотрудники E ON E.КодСотрудника = G.Изменил
WHERE G.КодТелефонногоНомераГруппы = @Id
ORDER BY Название";

    /// <summary>Группы, в которые входит телефон. Параметр: Id.</summary>
    public const string SELECT_ТелефонВходитВГруппы = @"
SELECT G.КодТелефонногоНомераГруппы, -- идентификатор группы
       P.ПолныйНомер + ' ' + CASE WHEN P.ОписаниеАТС = '' THEN P.Подключено ELSE P.ОписаниеАТС END AS Название -- имя группы
FROM ТелефонныеНомераГруппы G
INNER JOIN vwТелефонныеНомераИзменение P ON P.КодТелефонногоНомера = G.КодТелефонногоНомераГруппы
WHERE G.КодТелефонногоНомераЧленаГруппы = @Id
ORDER BY Название";

    /// <summary>Параллельные вызовы телефона. Параметр: Id.</summary>
    public const string SELECT_ТелефонПараллельныеВызовы = @"
SELECT P.КодТелефонногоНомера, -- исходный номер
       P.НомерТелефонаПараллельногоВызова, -- номер вызова
       P.НеИспользовать, -- признак отключения
       P.ЗадержкаПередВызовом, -- задержка перед вызовом
       P.ЗадержкаСлишкомБыстрогоОтвета, -- задержка быстрого ответа
       ISNULL(E.ФИО, '#' + CONVERT(varchar, P.Изменил)) AS Изменил, -- изменивший сотрудник
       P.Изменено -- время изменения
FROM vwТелефонныеНомераПараллельныеВызовы P
LEFT JOIN Сотрудники E ON E.КодСотрудника = P.Изменил
WHERE P.КодТелефонногоНомера = @Id
ORDER BY P.НомерТелефонаПараллельногоВызова";

    /// <summary>Оборудование телефона с текущим расположением. Параметр: Id.</summary>
    public const string SELECT_ТелефонОборудование = @"
SELECT F.КодОборудования, -- оборудование
       F.КодОборудованияПодключения, -- оборудование подключения
       F.SN, -- серийный номер
       F.МодельОборудования, -- модель
       F.РасположениеPath, -- расположение
       F.КодРасположения, -- идентификатор расположения
       F.ЕстьТелефонныйНомер, -- поддерживаемый тип номера
       F.Основной, -- основной номер на оборудовании
       F.СрокВозврата, -- срок возврата связи
       F.Примечание, -- примечание связи
       F.Изменил, -- изменивший сотрудник
       F.Изменено -- время изменения
FROM dbo.fn_ОборудованиеТелНомера(@Id) F
ORDER BY F.КодОборудованияПодключения, F.МодельОборудования, F.SN";

    /// <summary>Другие номера на связанном оборудовании одним запросом. Параметр: Id.</summary>
    public const string SELECT_ТелефонДругиеНомераОборудования = @"
SELECT L.КодОборудования, -- оборудование
       ISNULL(STUFF((SELECT ', ' + P.ПолныйНомер + CASE WHEN P.ПолныйНомер <> P.ТелефонныйНомер THEN ' (' + P.ТелефонныйНомер + ')' ELSE '' END
                     FROM ТелефонныеНомераОборудование X
                     INNER JOIN vwТелефонныеНомераИзменение P ON P.КодТелефонногоНомера = X.КодТелефонногоНомера
                     WHERE X.КодОборудования = L.КодОборудования AND X.КодТелефонногоНомера <> @Id
                     ORDER BY P.ПолныйНомер FOR XML PATH(''), TYPE).value('.', 'varchar(1000)'), 1, 2, ''), '') AS ДругиеНомера -- прочие номера
FROM ТелефонныеНомераОборудование L
WHERE L.КодТелефонногоНомера = @Id
GROUP BY L.КодОборудования";

    /// <summary>Добавление члена PBX-группы. Параметры: PhoneId, MemberPhoneId.</summary>
    public const string INSERT_ТелефонЧленГруппы = @"
INSERT INTO ТелефонныеНомераГруппы (КодТелефонногоНомераГруппы, КодТелефонногоНомераЧленаГруппы)
VALUES (@PhoneId, @MemberPhoneId) -- группа и новый член";

    /// <summary>Удаление члена PBX-группы. Параметры: PhoneId, MemberPhoneId.</summary>
    public const string DELETE_ТелефонЧленГруппы = @"
DELETE FROM ТелефонныеНомераГруппы
WHERE КодТелефонногоНомераГруппы = @PhoneId AND КодТелефонногоНомераЧленаГруппы = @MemberPhoneId -- точный член группы";

    /// <summary>Добавление связи оборудования. Параметры: PhoneId, EquipmentId, ConnectionEquipmentId, ReturnDate, Notes.</summary>
    public const string INSERT_ТелефонОборудование = @"
INSERT INTO ТелефонныеНомераОборудование
    (КодТелефонногоНомера, КодОборудования, КодОборудованияПодключения, СрокВозврата, Примечание)
VALUES (@PhoneId, @EquipmentId, @ConnectionEquipmentId, @ReturnDate, @Notes) -- связь и её срок возврата";

    /// <summary>Обновление метаданных связи по составному ключу. Параметры: PhoneId, EquipmentId, ConnectionEquipmentId, ReturnDate, Notes.</summary>
    public const string UPDATE_ТелефонОборудование = @"
UPDATE ТелефонныеНомераОборудование
SET СрокВозврата = @ReturnDate, -- новый срок возврата
    Примечание = @Notes -- новое примечание
WHERE КодТелефонногоНомера = @PhoneId AND КодОборудования = @EquipmentId
  AND ((@ConnectionEquipmentId IS NULL AND NULLIF(КодОборудованияПодключения, 0) IS NULL)
       OR NULLIF(КодОборудованияПодключения, 0) = @ConnectionEquipmentId) -- точная пара оборудования и подключения";

    /// <summary>Удаление связи оборудования по составному ключу. Параметры: PhoneId, EquipmentId, ConnectionEquipmentId.</summary>
    public const string DELETE_ТелефонОборудование = @"
DELETE FROM ТелефонныеНомераОборудование
WHERE КодТелефонногоНомера = @PhoneId AND КодОборудования = @EquipmentId
  AND ((@ConnectionEquipmentId IS NULL AND NULLIF(КодОборудованияПодключения, 0) IS NULL)
       OR NULLIF(КодОборудованияПодключения, 0) = @ConnectionEquipmentId) -- точная пара оборудования и подключения";

    /// <summary>Добавление параллельного вызова. Параметры: PhoneId, CallNumber, NotUsed, DelayBeforeCall, DelayTooQuickResponse.</summary>
    public const string INSERT_ТелефонПараллельныйВызов = @"
INSERT INTO vwТелефонныеНомераПараллельныеВызовы
    (КодТелефонногоНомера, НомерТелефонаПараллельногоВызова, НеИспользовать, ЗадержкаПередВызовом, ЗадержкаСлишкомБыстрогоОтвета)
VALUES (@PhoneId, @CallNumber, @NotUsed, @DelayBeforeCall, @DelayTooQuickResponse) -- вызов и две задержки";

    /// <summary>Обновление параллельного вызова. Параметры: PhoneId, CallNumber, NotUsed, DelayBeforeCall, DelayTooQuickResponse.</summary>
    public const string UPDATE_ТелефонПараллельныйВызов = @"
UPDATE vwТелефонныеНомераПараллельныеВызовы
SET НеИспользовать = @NotUsed, -- признак отключения
    ЗадержкаПередВызовом = @DelayBeforeCall, -- первая задержка
    ЗадержкаСлишкомБыстрогоОтвета = @DelayTooQuickResponse -- вторая задержка
WHERE КодТелефонногоНомера = @PhoneId AND НомерТелефонаПараллельногоВызова = @CallNumber";

    /// <summary>Удаление параллельного вызова. Параметры: PhoneId, CallNumber.</summary>
    public const string DELETE_ТелефонПараллельныйВызов = @"
DELETE FROM vwТелефонныеНомераПараллельныеВызовы
WHERE КодТелефонногоНомера = @PhoneId AND НомерТелефонаПараллельногоВызова = @CallNumber";

    /// <summary>Удаление телефона и его связей. Параметр: Id.</summary>
    public const string DELETE_ТелефонДляФормы = @"
DELETE FROM ТелефонныеНомераГруппы WHERE КодТелефонногоНомераГруппы = @Id OR КодТелефонногоНомераЧленаГруппы = @Id; -- членство и состав группы
DELETE FROM ТелефонныеНомераОборудование WHERE КодТелефонногоНомера = @Id; -- связи оборудования
DELETE FROM vwТелефонныеНомераПараллельныеВызовы WHERE КодТелефонногоНомера = @Id; -- параллельные вызовы
DELETE FROM vwТелефонныеНомераИзменение WHERE КодТелефонногоНомера = @Id; -- основная запись";

    /// <summary>Запрет удаления ведущего или отображаемого номера. Параметр: Id.</summary>
    public const string SELECT_ТелефонЗависимыеПриУдалении = @"
SELECT COUNT(*) -- число зависимых номеров
FROM vwТелефонныеНомераИзменение
WHERE ВедомыйОт = @Id OR ОтображаемыйОт = @Id";

    /// <summary>Типы номеров с фильтром по станции. Параметры: StationId, Search, LimitPlusOne.</summary>
    public const string SELECT_ТелефонТипыДляВыбора = @"
SELECT TOP (@LimitPlusOne) T.КодТипаТелефонныхНомеров, -- идентификатор типа
       T.ТипТелефонныхНомеров, -- название типа
       T.ТипТелефонныхНомеровЛат -- латинское имя
FROM ТипыТелефонныхНомеров T
WHERE (@Search = '' OR T.ТипТелефонныхНомеров LIKE '%' + @Search + '%')
  AND (@StationId IS NULL OR EXISTS (
      SELECT 1 FROM vwТелефонныеСтанции S
      WHERE S.КодТелефоннойСтанции = @StationId
        AND ((T.КодТипаТелефонныхНомеров = 1 AND S.Digit = 1)
          OR (T.КодТипаТелефонныхНомеров = 2 AND S.Analog = 1)
          OR (T.КодТипаТелефонныхНомеров = 3 AND S.ISDN = 1)
          OR (T.КодТипаТелефонныхНомеров = 10 AND S.DECT = 1)
          OR (T.КодТипаТелефонныхНомеров = 200 AND S.IP = 1)
          OR T.КодТипаТелефонныхНомеров NOT IN (1, 2, 3, 10, 200))))
ORDER BY T.КодТипаТелефонныхНомеров";

    /// <summary>Тип номера по ID без скрытия несовместимого сохранённого значения. Параметр: Id.</summary>
    public const string SELECT_ТелефонТипПоИдентификатору = @"
SELECT КодТипаТелефонныхНомеров, -- идентификатор типа
       ТипТелефонныхНомеров, -- название типа
       ТипТелефонныхНомеровЛат -- латинское имя
FROM ТипыТелефонныхНомеров
WHERE КодТипаТелефонныхНомеров = @Id";

    /// <summary>Проверка совместимости типа и станции. Параметры: StationId, PhoneTypeId.</summary>
    public const string SELECT_ТелефонТипДоступенНаСтанции = @"
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM ТипыТелефонныхНомеров T
    INNER JOIN vwТелефонныеСтанции S ON S.КодТелефоннойСтанции = @StationId
    WHERE T.КодТипаТелефонныхНомеров = @PhoneTypeId
      AND ((T.КодТипаТелефонныхНомеров = 1 AND S.Digit = 1)
        OR (T.КодТипаТелефонныхНомеров = 2 AND S.Analog = 1)
        OR (T.КодТипаТелефонныхНомеров = 3 AND S.ISDN = 1)
        OR (T.КодТипаТелефонныхНомеров = 10 AND S.DECT = 1)
        OR (T.КодТипаТелефонныхНомеров = 200 AND S.IP = 1)
        OR T.КодТипаТелефонныхНомеров NOT IN (1, 2, 3, 10, 200))) THEN 1 ELSE 0 END -- доступность типа";

    /// <summary>Станции с особыми правилами сортировки legacy. Параметры: Search, LimitPlusOne.</summary>
    public const string SELECT_ТелефонСтанцииДляВыбора = @"
SELECT TOP (@LimitPlusOne) S.КодТелефоннойСтанции, -- идентификатор станции
       S.ТелефоннаяСтанция, -- название станции
       S.КодОборудования, -- оборудование станции
       S.ПараллельныйВызов, -- параллельные вызовы
       S.ОписаниеАТСзаглавными, -- заглавное описание
       S.ВедомыеНомера, -- ведомые номера
       S.ПодменаОтображаемогоАномера, -- отображаемые номера
       S.ПрефиксСтанции, -- префикс полного номера
       S.ПрефиксИспользуется, -- шаблон применения префикса
       S.XorcomName -- имя Xorcom
FROM vwТелефонныеСтанции S
WHERE @Search = '' OR S.ТелефоннаяСтанция LIKE '%' + @Search + '%'
ORDER BY CASE WHEN S.КодТелефоннойСтанции < 0 THEN S.КодТелефоннойСтанции * -1000 ELSE S.КодТелефоннойСтанции END";

    /// <summary>Станция по ID. Параметр: Id.</summary>
    public const string SELECT_ТелефонСтанцияПоИдентификатору = @"
SELECT КодТелефоннойСтанции, -- идентификатор станции
       ТелефоннаяСтанция, -- название станции
       КодОборудования, -- оборудование станции
       ПараллельныйВызов, -- параллельные вызовы
       ОписаниеАТСзаглавными, -- заглавное описание
       ВедомыеНомера, -- ведомые номера
       ПодменаОтображаемогоАномера, -- отображаемые номера
       ПрефиксСтанции, -- префикс полного номера
       ПрефиксИспользуется, -- шаблон применения префикса
       XorcomName -- имя Xorcom
FROM vwТелефонныеСтанции
WHERE КодТелефоннойСтанции = @Id";

    /// <summary>Телефонные номера для PBX-группы. Параметры: StationId, Search, LimitPlusOne и исключённые ID.</summary>
    public const string SELECT_ТелефонЧленыГруппыДляВыбора = @"
SELECT TOP (@LimitPlusOne) P.КодТелефонногоНомера, -- идентификатор номера
       ISNULL(P.ПолныйНомер + CASE WHEN P.ПолныйНомер <> P.ТелефонныйНомер THEN '[' + P.ТелефонныйНомер + ']' ELSE '' END + ' '
            + ISNULL(E.ФИО + ISNULL(' (' + NULLIF(P.Подключено, '') + ')', ''), P.Подключено), '#' + CONVERT(varchar, P.КодТелефонногоНомера)) AS Телефон, -- имя
       P.ТелефонныйНомер, -- короткий номер
       P.ПолныйНомер, -- полный номер
       P.КодТелефоннойСтанции -- станция
FROM vwТелефонныеНомераИзменение P
LEFT JOIN Сотрудники E ON E.КодСотрудника = P.КодСотрудника
WHERE P.КодТелефоннойСтанции = @StationId
  AND (@Search = '' OR P.ТелефонныйНомер LIKE '%' + @Search + '%' OR P.ПолныйНомер LIKE '%' + @Search + '%'
       OR P.Подключено LIKE '%' + @Search + '%' OR E.ФИО LIKE '%' + @Search + '%')
ORDER BY Телефон";

    /// <summary>Возможные ведущие номера. Параметры: StationId, Search, LimitPlusOne и исключённые ID.</summary>
    public const string SELECT_ТелефонВедущиеДляВыбора = @"
SELECT TOP (@LimitPlusOne) P.КодТелефонногоНомера, -- идентификатор номера
       ISNULL(P.ПолныйНомер + CASE WHEN P.ПолныйНомер <> P.ТелефонныйНомер THEN '[' + P.ТелефонныйНомер + ']' ELSE '' END + ' '
            + ISNULL(E.ФИО + ISNULL(' (' + NULLIF(P.Подключено, '') + ')', ''), P.Подключено), '#' + CONVERT(varchar, P.КодТелефонногоНомера)) AS Телефон, -- имя
       P.ТелефонныйНомер, -- короткий номер
       P.ПолныйНомер, -- полный номер
       P.КодТелефоннойСтанции -- станция
FROM vwТелефонныеНомераИзменение P
LEFT JOIN Сотрудники E ON E.КодСотрудника = P.КодСотрудника
WHERE P.КодТелефоннойСтанции = @StationId AND P.ВедомыйОт IS NULL
  AND (@Search = '' OR P.ТелефонныйНомер LIKE '%' + @Search + '%' OR P.ПолныйНомер LIKE '%' + @Search + '%'
       OR P.Подключено LIKE '%' + @Search + '%' OR E.ФИО LIKE '%' + @Search + '%')
ORDER BY Телефон";

    /// <summary>Возможные отображаемые номера. Параметры: StationId, Search, LimitPlusOne и исключённые ID.</summary>
    public const string SELECT_ТелефонОтображаемыеДляВыбора = @"
SELECT TOP (@LimitPlusOne) P.КодТелефонногоНомера, -- идентификатор номера
       ISNULL(P.ПолныйНомер + CASE WHEN P.ПолныйНомер <> P.ТелефонныйНомер THEN '[' + P.ТелефонныйНомер + ']' ELSE '' END + ' '
            + ISNULL(E.ФИО + ISNULL(' (' + NULLIF(P.Подключено, '') + ')', ''), P.Подключено), '#' + CONVERT(varchar, P.КодТелефонногоНомера)) AS Телефон, -- имя
       P.ТелефонныйНомер, -- короткий номер
       P.ПолныйНомер, -- полный номер
       P.КодТелефоннойСтанции -- станция
FROM vwТелефонныеНомераИзменение P
LEFT JOIN Сотрудники E ON E.КодСотрудника = P.КодСотрудника
WHERE P.КодТелефоннойСтанции = @StationId AND P.ВедомыйОт IS NULL AND P.ОтображаемыйОт IS NULL
  AND (@Search = '' OR P.ТелефонныйНомер LIKE '%' + @Search + '%' OR P.ПолныйНомер LIKE '%' + @Search + '%'
       OR P.Подключено LIKE '%' + @Search + '%' OR E.ФИО LIKE '%' + @Search + '%')
ORDER BY Телефон";

    /// <summary>Телефон по ID для справочного выбора. Параметр: Id.</summary>
    public const string SELECT_ТелефонСправочныйПоИдентификатору = @"
SELECT P.КодТелефонногоНомера, -- идентификатор номера
       ISNULL(P.ПолныйНомер + CASE WHEN P.ПолныйНомер <> P.ТелефонныйНомер THEN '[' + P.ТелефонныйНомер + ']' ELSE '' END + ' '
            + ISNULL(E.ФИО + ISNULL(' (' + NULLIF(P.Подключено, '') + ')', ''), P.Подключено), '#' + CONVERT(varchar, P.КодТелефонногоНомера)) AS Телефон, -- имя
       P.ТелефонныйНомер, -- короткий номер
       P.ПолныйНомер, -- полный номер
       P.КодТелефоннойСтанции -- станция
FROM vwТелефонныеНомераИзменение P
LEFT JOIN Сотрудники E ON E.КодСотрудника = P.КодСотрудника
WHERE P.КодТелефонногоНомера = @Id";

    /// <summary>Общий источник допустимого оборудования для телефонного номера.</summary>
    public const string PhoneEquipmentCandidateSource = "dbo.fn_ОборудованиеДляТелНомера(@PhoneTypeId, @StationId, @SocketId, @EmployeeId, @PhoneId)";

    /// <summary>Полная проекция функции Finder с ограничением видимости оборудования.</summary>
    public const string SELECT_FinderEquipment = """
        SELECT F.КодОборудования AS EquipmentId,F.SN AS SerialNumber,F.МодельОборудования AS Model,
               F.РасположениеPath AS LocationPath,F.КодОборудованияПодключения AS ConnectionEquipmentId
        FROM
        """ + " " + PhoneEquipmentCandidateSource + """
         F
        JOIN dbo.vwОборудование E ON E.КодОборудования=F.КодОборудования
        WHERE 1=1
        """;

    /// <summary>Оборудование, доступное номеру. Параметры функции, Search, LimitPlusOne и исключённое оборудование.</summary>
    public const string SELECT_ТелефонОборудованиеДляВыбора = @"
SELECT TOP (@LimitPlusOne) F.КодОборудования, -- оборудование
       F.КодОборудованияПодключения, -- оборудование подключения
       F.МодельОборудования, -- модель
       F.SN, -- серийный номер
       F.РасположениеPath, -- расположение
       ISNULL(R.КодРасположения, 0) AS КодРасположения, -- идентификатор расположения
       T.ЕстьТелефонныйНомер, -- поддерживаемый тип номера
       '[#' + CONVERT(varchar, F.КодОборудования) + '] ' + F.МодельОборудования + '; ' + F.РасположениеPath
           + '; SN: ' + F.SN + ISNULL('; оборудование подключения: ' + CONVERT(varchar, F.КодОборудованияПодключения), '') AS Название -- имя
FROM " + PhoneEquipmentCandidateSource + @" F
INNER JOIN vwОборудование E ON E.КодОборудования = F.КодОборудования
INNER JOIN МоделиОборудования M ON M.КодМоделиОборудования = E.КодМоделиОборудования
INNER JOIN ТипыОборудования T ON T.КодТипаОборудования = M.КодТипаОборудования
LEFT JOIN ОборудованиеРасположение R ON R.КодОборудования = F.КодОборудования
    AND NOT EXISTS (SELECT 1 FROM ОборудованиеРасположение X WHERE X.КодОборудования = R.КодОборудования AND X.От > R.От)
WHERE (@Search = '' OR F.МодельОборудования LIKE '%' + @Search + '%' OR F.SN LIKE '%' + @Search + '%'
   OR F.РасположениеPath LIKE '%' + @Search + '%')
ORDER BY Название";

    /// <summary>Оборудование по составному ключу для восстановления выбора. Параметры: EquipmentId, ConnectionEquipmentId.</summary>
    public const string SELECT_ТелефонОборудованиеПоИдентификатору = @"
SELECT E.КодОборудования, -- оборудование
       @ConnectionEquipmentId AS КодОборудованияПодключения, -- оборудование подключения
       M.МодельОборудования, -- модель
       E.SN, -- серийный номер
       ISNULL(L.РасположениеPath0, '') AS РасположениеPath, -- расположение
       ISNULL(R.КодРасположения, 0) AS КодРасположения, -- идентификатор расположения
       T.ЕстьТелефонныйНомер, -- поддерживаемый тип номера
       '[#' + CONVERT(varchar, E.КодОборудования) + '] ' + M.МодельОборудования + '; SN: ' + E.SN AS Название -- имя
FROM vwОборудование E
INNER JOIN МоделиОборудования M ON M.КодМоделиОборудования = E.КодМоделиОборудования
INNER JOIN ТипыОборудования T ON T.КодТипаОборудования = M.КодТипаОборудования
LEFT JOIN ОборудованиеРасположение R ON R.КодОборудования = E.КодОборудования
    AND NOT EXISTS (SELECT 1 FROM ОборудованиеРасположение X WHERE X.КодОборудования = R.КодОборудования AND X.От > R.От)
LEFT JOIN vwРасположения L ON L.КодРасположения = R.КодРасположения
WHERE E.КодОборудования = @EquipmentId";

    /// <summary>Текущий офис станции для fail-closed поиска розеток. Параметр: StationId.</summary>
    public const string SELECT_ТелефонОфисСтанции = @"
SELECT TOP 1 O.КодРасположения -- офис станции
FROM vwТелефонныеСтанции S
INNER JOIN ОборудованиеРасположение R ON R.КодОборудования = S.КодОборудования
INNER JOIN vwРасположения L ON L.КодРасположения = R.КодРасположения
INNER JOIN vwРасположения O ON O.Офис = 1 AND L.L >= O.L AND L.R <= O.R
WHERE S.КодТелефоннойСтанции = @StationId
  AND NOT EXISTS (SELECT 1 FROM ОборудованиеРасположение X WHERE X.КодОборудования = R.КодОборудования AND X.От > R.От)";

    /// <summary>Свободные розетки в офисе станции. Параметры: OfficeLocationId, SocketId, Search, LimitPlusOne.</summary>
    public const string SELECT_ТелефонРозеткиВОфисе = @"
SELECT TOP (@LimitPlusOne) S.КодРозетки, -- идентификатор розетки
       S.Розетка AS Название -- название розетки
FROM vwРозетки S
WHERE S.Работает = 1
  AND (S.КодРозетки = @SocketId OR (NOT EXISTS (SELECT 1 FROM vwТелефонныеНомераИзменение P WHERE P.КодРозетки = S.КодРозетки)
       AND NOT EXISTS (SELECT 1 FROM vwЛокальнаяСеть N WHERE N.КодРозетки = S.КодРозетки)))
  AND EXISTS (SELECT 1 FROM vwРасположения O INNER JOIN vwРасположения L ON L.L >= O.L AND L.R <= O.R
              WHERE O.КодРасположения = @OfficeLocationId AND L.КодРасположения = S.КодРасположения)
  AND EXISTS (SELECT 1 FROM vwРасположения O INNER JOIN vwРасположения L ON L.L >= O.L AND L.R <= O.R
              WHERE O.Офис = 1 AND L.КодРасположения = S.КодРасположения)
  AND (@Search = '' OR S.Розетка LIKE '%' + @Search + '%')
ORDER BY Название";

    /// <summary>Свободные розетки на расположениях оборудования. Параметры: SocketId, Search, LimitPlusOne и LocationIds.</summary>
    public const string SELECT_ТелефонРозеткиОборудования = @"
SELECT TOP (@LimitPlusOne) S.КодРозетки, -- идентификатор розетки
       S.Розетка AS Название -- название розетки
FROM vwРозетки S
WHERE S.Работает = 1
  AND (S.КодРозетки = @SocketId OR (NOT EXISTS (SELECT 1 FROM vwТелефонныеНомераИзменение P WHERE P.КодРозетки = S.КодРозетки)
       AND NOT EXISTS (SELECT 1 FROM vwЛокальнаяСеть N WHERE N.КодРозетки = S.КодРозетки)))
  AND (@Search = '' OR S.Розетка LIKE '%' + @Search + '%')";

    /// <summary>Розетка по ID. Параметр: Id.</summary>
    public const string SELECT_ТелефонРозеткаПоИдентификатору = @"
SELECT КодРозетки, -- идентификатор розетки
       Розетка AS Название -- название розетки
FROM vwРозетки
WHERE КодРозетки = @Id";

    /// <summary>Лица-заказчики. Параметры: Search, LimitPlusOne.</summary>
    public const string SELECT_ТелефонЗаказчикиДляВыбора = @"
SELECT TOP (@LimitPlusOne) КодЛица, -- идентификатор заказчика
       Кличка -- отображаемое имя
FROM ЛицаЗаказчики
WHERE @Search = '' OR Кличка LIKE '%' + @Search + '%'
ORDER BY Кличка";

    /// <summary>Заказчик по ID. Параметр: Id.</summary>
    public const string SELECT_ТелефонЗаказчикПоИдентификатору = @"
SELECT КодЛица, -- идентификатор заказчика
       Кличка -- отображаемое имя
FROM ЛицаЗаказчики
WHERE КодЛица = @Id";

    /// <summary>Сотрудники для формы телефона. Параметры: Search, LimitPlusOne.</summary>
    public const string SELECT_ТелефонСотрудникиДляВыбора = @"
SELECT TOP (@LimitPlusOne) КодСотрудника, -- идентификатор сотрудника
       Сотрудник, -- отображаемое имя
       Employee -- английское имя
FROM Сотрудники
WHERE Состояние = 0 AND (@Search = '' OR Сотрудник LIKE '%' + @Search + '%' OR Employee LIKE '%' + @Search + '%')
ORDER BY Сотрудник";

    /// <summary>Организация сотрудника для подстановки заказчика. Параметр: EmployeeId.</summary>
    public const string SELECT_ТелефонОрганизацияСотрудника = @"
SELECT КодЛицаЗаказчика -- организация сотрудника, как Employee.OrganizationId в legacy
FROM Сотрудники
WHERE КодСотрудника = @EmployeeId";

    /// <summary>Английское имя и транслитерация сотрудника для описания АТС. Параметр: EmployeeId.</summary>
    public const string SELECT_ТелефонОписаниеАТССотрудника = @"
SELECT CASE WHEN NULLIF(LTRIM(RTRIM(Employee)), '') IS NOT NULL THEN LTRIM(RTRIM(Employee))
            ELSE dbo.fn_TransLit(Сотрудник) END AS ОписаниеАТС -- английское имя или подтверждённая транслитерация
FROM Сотрудники
WHERE КодСотрудника = @EmployeeId";

    /// <summary>Основной номер сотрудника. Параметр: EmployeeId.</summary>
    public const string SELECT_ТелефонОсновнойНомерСотрудника = @"
SELECT TOP 1 P.КодТелефонногоНомера, -- идентификатор номера
       P.ТелефонныйНомер -- короткий номер
FROM vwТелефонныеНомераИзменение P
WHERE P.КодСотрудника = @EmployeeId AND P.ОсновнойНомер <> 0";

    /// <summary>Признак портов для типа и станции. Параметры: StationId, PhoneTypeId.</summary>
    public const string SELECT_ТелефонСуществуютПорты = @"
SELECT CASE WHEN EXISTS (SELECT 1 FROM ТелефонныеСтанцииПорты
                         WHERE КодТелефоннойСтанции = @StationId AND КодТипаТелефонныхНомеров = @PhoneTypeId)
            THEN 1 ELSE 0 END -- наличие портов";

    /// <summary>Свободные порты с включением текущего. Параметры: StationId, PhoneTypeId, PhoneId.</summary>
    public const string SELECT_ТелефонСвободныеПорты = @"
SELECT P.ПортАТС -- порт АТС
FROM ТелефонныеСтанцииПорты P
WHERE P.КодТелефоннойСтанции = @StationId AND P.КодТипаТелефонныхНомеров = @PhoneTypeId
  AND NOT EXISTS (SELECT 1 FROM vwТелефонныеНомераИзменение X
                  WHERE X.КодТелефоннойСтанции = P.КодТелефоннойСтанции
                    AND X.КодТипаТелефонныхНомеров = P.КодТипаТелефонныхНомеров
                    AND X.ПортАТС = P.ПортАТС AND X.КодТелефонногоНомера <> @PhoneId)
UNION
SELECT P.ПортАТС -- собственный текущий порт
FROM vwТелефонныеНомераИзменение P
WHERE P.КодТелефоннойСтанции = @StationId AND P.КодТипаТелефонныхНомеров = @PhoneTypeId
  AND P.КодТелефонногоНомера = @PhoneId AND LEN(P.ПортАТС) > 0
ORDER BY ПортАТС";

    /// <summary>Доступность автонабора по белому списку латинских имён типов. Параметры: StationId, PhoneTypeId.</summary>
    public const string SELECT_ТелефонАвтонаборДоступен = @"
DECLARE @ColumnName sysname = CASE @PhoneTypeId WHEN 1 THEN 'DigitHot' WHEN 2 THEN 'AnalogHot'
                                 WHEN 3 THEN 'ISDNHot' WHEN 10 THEN 'DECTHot' WHEN 200 THEN 'IPHot' ELSE NULL END;
DECLARE @Result int = 0;
IF @ColumnName IS NOT NULL AND COL_LENGTH('vwТелефонныеСтанции', @ColumnName) IS NOT NULL
BEGIN
    DECLARE @Sql nvarchar(max) = N'SELECT @Result = CASE WHEN EXISTS (SELECT 1 FROM vwТелефонныеСтанции WHERE КодТелефоннойСтанции = @StationId AND '
        + QUOTENAME(@ColumnName) + N' = 1) THEN 1 ELSE 0 END';
    EXEC sp_executesql @Sql, N'@StationId int, @Result int OUTPUT', @StationId = @StationId, @Result = @Result OUTPUT;
END;
SELECT @Result -- доступность автонабора";

    /// <summary>Дубликат короткого номера на станции. Параметры: Id, Number, StationId.</summary>
    public const string SELECT_ТелефонДубликатНаСтанции = @"
SELECT TOP 1 КодТелефонногоНомера -- существующий номер
FROM vwТелефонныеНомераИзменение
WHERE КодТелефоннойСтанции = @StationId AND ТелефонныйНомер = @Number AND КодТелефонногоНомера <> @Id";

    /// <summary>Полный номер по авторитетным данным станции и зависимых номеров. Параметры: Number, StationId, DrivenById, DisplayedFromId.</summary>
    public const string SELECT_ТелефонАвторитетныйПолныйНомер = @"
SELECT CASE WHEN NULLIF(Displayed.ПолныйНомер, '') IS NOT NULL THEN Displayed.ПолныйНомер
            WHEN NULLIF(Driven.ПолныйНомер, '') IS NOT NULL THEN Driven.ПолныйНомер
            ELSE CASE WHEN NULLIF(S.ПрефиксИспользуется, '') IS NOT NULL
                           AND @Number LIKE REPLACE(S.ПрефиксИспользуется, '%', '') + '%'
                      THEN S.ПрефиксСтанции ELSE '' END + @Number END -- приоритет отображаемого, ведущего и префикса
FROM vwТелефонныеСтанции S
LEFT JOIN vwТелефонныеНомераИзменение Displayed ON Displayed.КодТелефонногоНомера = @DisplayedFromId
LEFT JOIN vwТелефонныеНомераИзменение Driven ON Driven.КодТелефонногоНомера = @DrivenById
WHERE S.КодТелефоннойСтанции = @StationId";

    /// <summary>Дубликат независимого полного номера. Параметры: Id, FullNumber.</summary>
    public const string SELECT_ТелефонДубликатПолногоНомера = @"
SELECT COUNT(*) -- число дубликатов
FROM vwТелефонныеНомераИзменение
WHERE ВедомыйОт IS NULL AND ОтображаемыйОт IS NULL AND ПолныйНомер = @FullNumber AND КодТелефонногоНомера <> @Id";

    /// <summary>Пересечения полных номеров с экранированным LIKE. Параметры: Id, FullNumberEscaped.</summary>
    public const string SELECT_ТелефонПересеченияПолногоНомера = @"
SELECT COUNT(*) -- число пересечений в обе стороны
FROM vwТелефонныеНомераИзменение P
WHERE P.КодТелефонногоНомера <> @Id
  AND (P.ПолныйНомер LIKE @FullNumberEscaped + '_%' ESCAPE '\' OR @FullNumberEscaped LIKE P.ПолныйНомер + '_%' ESCAPE '\')";

    /// <summary>Проверка выбранного порта среди доступных. Параметры: StationId, PhoneTypeId, PhoneId, AtsPort.</summary>
    public const string SELECT_ТелефонПортДоступен = @"
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM ТелефонныеСтанцииПорты P
    WHERE P.КодТелефоннойСтанции = @StationId AND P.КодТипаТелефонныхНомеров = @PhoneTypeId AND P.ПортАТС = @AtsPort
      AND NOT EXISTS (SELECT 1 FROM vwТелефонныеНомераИзменение X
                      WHERE X.КодТелефоннойСтанции = P.КодТелефоннойСтанции
                        AND X.КодТипаТелефонныхНомеров = P.КодТипаТелефонныхНомеров
                        AND X.ПортАТС = P.ПортАТС AND X.КодТелефонногоНомера <> @PhoneId))
   OR EXISTS (SELECT 1 FROM vwТелефонныеНомераИзменение P
              WHERE P.КодТелефонногоНомера = @PhoneId AND P.КодТелефоннойСтанции = @StationId
                AND P.КодТипаТелефонныхНомеров = @PhoneTypeId AND P.ПортАТС = @AtsPort)
   THEN 1 ELSE 0 END -- доступность порта";

    /// <summary>Проверка ссылок ведущего/отображаемого номера на цикл и допустимую станцию. Параметры: Id, StationId, DrivenById, DisplayedFromId.</summary>
    public const string SELECT_ТелефонНедопустимыеЗависимости = @"
;WITH DependencyTree AS (
    SELECT P.КодТелефонногоНомера, P.КодТелефоннойСтанции, P.ВедомыйОт, P.ОтображаемыйОт, 0 AS Depth
    FROM vwТелефонныеНомераИзменение P
    WHERE P.КодТелефонногоНомера = @DrivenById OR P.КодТелефонногоНомера = @DisplayedFromId
    UNION ALL
    SELECT P.КодТелефонногоНомера, P.КодТелефоннойСтанции, P.ВедомыйОт, P.ОтображаемыйОт, D.Depth + 1
    FROM vwТелефонныеНомераИзменение P
    INNER JOIN DependencyTree D ON P.КодТелефонногоНомера = D.ВедомыйОт OR P.КодТелефонногоНомера = D.ОтображаемыйОт
    WHERE D.Depth < 20)
SELECT (SELECT COUNT(*) FROM DependencyTree D
        WHERE D.КодТелефонногоНомера = @Id OR D.КодТелефоннойСтанции <> @StationId OR D.Depth >= 20
           OR D.Depth = 0 AND @DrivenById IS NOT NULL AND D.КодТелефонногоНомера = @DrivenById AND D.ВедомыйОт IS NOT NULL
           OR D.Depth = 0 AND @DisplayedFromId IS NOT NULL AND D.КодТелефонногоНомера = @DisplayedFromId
              AND (D.ВедомыйОт IS NOT NULL OR D.ОтображаемыйОт IS NOT NULL))
     + CASE WHEN @DrivenById IS NOT NULL AND NOT EXISTS (SELECT 1 FROM vwТелефонныеНомераИзменение WHERE КодТелефонногоНомера = @DrivenById)
            THEN 1 ELSE 0 END
     + CASE WHEN @DisplayedFromId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM vwТелефонныеНомераИзменение WHERE КодТелефонногоНомера = @DisplayedFromId)
            THEN 1 ELSE 0 END -- самоссылка, цикл, другая станция или отсутствующая зависимость";

    /// <summary>Предупреждения о работодателях сотрудника. Параметр: EmployeeId.</summary>
    public const string SELECT_ТелефонРаботодателиСотрудника = @"
SELECT DISTINCT D.КодЛица, -- идентификатор работодателя
       P.Кличка AS Работодатель -- название работодателя
FROM vwДолжности D
INNER JOIN ЛицаЗаказчики P ON P.КодЛица = D.КодЛица
WHERE D.КодСотрудника = @EmployeeId";

    /// <summary>Предупреждения о сотрудниках, которым выдан номер переадресации. Параметры: Id, EmployeeId, QuickCall.</summary>
    public const string SELECT_ТелефонПереадресацияДругогоСотрудника = @"
SELECT DISTINCT E.Сотрудник -- сотрудник с тем же номером
FROM vwТелефонныеНомераИзменение P
INNER JOIN Сотрудники E ON E.КодСотрудника = P.КодСотрудника
WHERE P.КодТелефонногоНомера <> @Id AND P.КодСотрудника <> @EmployeeId
  AND @QuickCall IN (P.ПолныйНомер, P.ТелефонныйНомер)";

    /// <summary>Признак участия номера в чужих параллельных вызовах. Параметр: CallNumber.</summary>
    public const string SELECT_ТелефонУчастникПараллельногоВызова = @"
SELECT COUNT(*) -- число исходных номеров
FROM vwТелефонныеНомераПараллельныеВызовы
WHERE НомерТелефонаПараллельногоВызова = @CallNumber";

    /// <summary>Признак группы посменной работы сотрудника. Параметр: EmployeeId.</summary>
    public const string SELECT_ТелефонСотрудникГрупповойРаботы = @"
SELECT CASE WHEN КодЛица IS NULL AND Состояние = 0 THEN 1 ELSE 0 END -- группа посменной работы
FROM Сотрудники
WHERE КодСотрудника = @EmployeeId";

    /// <summary>Другие заказчики SIM для короткого мобильного номера. Параметры: QuickCall, CustomerId.</summary>
    public const string SELECT_ТелефонЗаказчикиКороткогоМобильного = @"
SELECT DISTINCT P.Кличка AS ЗаказчикSimКарты -- заказчик договора связи
FROM vwSIMКарты S
INNER JOIN ДоговорыСвязи D ON S.КодДоговора = D.КодДокумента
INNER JOIN ЛицаЗаказчики P ON D.КодЛицаЗаказчика = P.КодЛица
WHERE '+' + S.НомерТелефона = @QuickCall
  AND EXISTS (SELECT 1 FROM vwОборудование E WHERE E.КодОборудования = S.КодОборудования AND E.Списано IS NULL)
  AND D.КодЛицаЗаказчика <> @CustomerId";

    /// <summary>Другие допустимые заказчики на расположении станции. Параметры: CustomerId, StationId, PhoneTypeId, DrivenById, DisplayedFromId, FullNumber.</summary>
    public const string SELECT_ТелефонЗаказчикиНаРасположенииСтанции = @"
DECLARE @КодРасположения int;
DECLARE @Лица TABLE(КодЛица int, Кличка varchar(50));
SET @КодРасположения = (
    SELECT R.КодРасположения
    FROM ОборудованиеРасположение R
    INNER JOIN vwТелефонныеСтанции S ON R.КодОборудования = S.КодОборудования
    WHERE S.КодТелефоннойСтанции = @StationId
      AND NOT EXISTS (SELECT 1 FROM ОборудованиеРасположение X WHERE X.КодОборудования = R.КодОборудования AND X.От > R.От)
      AND @PhoneTypeId NOT IN (0, 100, 101) AND S.ГолосовойШлюз = '' AND S.КодТелефоннойСтанции BETWEEN 1 AND 399
      AND @DisplayedFromId IS NULL AND @DrivenById IS NULL
      AND @FullNumber NOT LIKE '2149[12]%' AND @FullNumber NOT LIKE '339493%');
IF @КодРасположения IS NOT NULL
BEGIN
    INSERT @Лица
    SELECT P.КодЛица, P.Кличка
    FROM vwРасположенияЛиц RL
    INNER JOIN ЛицаЗаказчики P ON RL.КодЛица = P.КодЛица
    INNER JOIN vwРасположения O ON RL.КодРасположения = O.КодРасположения
    INNER JOIN vwРасположения R ON O.L <= R.L AND O.R >= R.R
    WHERE R.КодРасположения = @КодРасположения;
END;
IF EXISTS (SELECT 1 FROM @Лица WHERE КодЛица = @CustomerId)
    DELETE FROM @Лица; -- выбранный заказчик допустим: предупреждение не требуется
SELECT КодЛица, -- идентификатор допустимого заказчика
       Кличка -- название заказчика
FROM @Лица ORDER BY Кличка";

    /// <summary>Член группы на другой или отсутствующей станции. Параметры: MemberPhoneId, StationId.</summary>
    public const string SELECT_ТелефонЧленГруппыНедопустим = @"
SELECT COUNT(*) -- ноль означает допустимого члена группы
FROM (SELECT @MemberPhoneId AS КодТелефонногоНомера) M
LEFT JOIN vwТелефонныеНомераИзменение P ON P.КодТелефонногоНомера = M.КодТелефонногоНомера
WHERE P.КодТелефонногоНомера IS NULL OR P.КодТелефоннойСтанции <> @StationId";

    /// <summary>Допустимость составной связи оборудования с сохранением существующей. Параметры функции и EquipmentId, ConnectionEquipmentId.</summary>
    public const string SELECT_ТелефонОборудованиеДопустимо = @"
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM dbo.fn_ОборудованиеДляТелНомера(@PhoneTypeId, @StationId, @SocketId, @EmployeeId, @PhoneId) F
    WHERE F.КодОборудования = @EquipmentId
      AND ((@ConnectionEquipmentId IS NULL AND NULLIF(F.КодОборудованияПодключения, 0) IS NULL)
           OR NULLIF(F.КодОборудованияПодключения, 0) = @ConnectionEquipmentId))
   OR EXISTS (SELECT 1 FROM ТелефонныеНомераОборудование L
              WHERE L.КодТелефонногоНомера = @PhoneId AND L.КодОборудования = @EquipmentId
                AND ((@ConnectionEquipmentId IS NULL AND NULLIF(L.КодОборудованияПодключения, 0) IS NULL)
                     OR NULLIF(L.КодОборудованияПодключения, 0) = @ConnectionEquipmentId))
   THEN 1 ELSE 0 END -- доступная новая либо уже существующая связь";

    /// <summary>Допустимость розетки в дереве офиса станции. Собственная связь освобождается только от проверки занятости. Параметры: SocketId, StationId, PhoneId.</summary>
    public const string SELECT_ТелефонРозеткаДопустимаВОфисе = @"
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM vwРозетки S
    WHERE S.КодРозетки = @SocketId
      AND S.Работает = 1
      AND (EXISTS (SELECT 1 FROM vwТелефонныеНомераИзменение P WHERE P.КодТелефонногоНомера = @PhoneId AND P.КодРозетки = @SocketId)
           OR (NOT EXISTS (SELECT 1 FROM vwТелефонныеНомераИзменение P WHERE P.КодРозетки = S.КодРозетки)
               AND NOT EXISTS (SELECT 1 FROM vwЛокальнаяСеть N WHERE N.КодРозетки = S.КодРозетки)))
      AND EXISTS (SELECT 1 FROM vwТелефонныеСтанции ST
                  INNER JOIN ОборудованиеРасположение R ON R.КодОборудования = ST.КодОборудования
                  INNER JOIN vwРасположения SL ON SL.КодРасположения = R.КодРасположения
                  INNER JOIN vwРасположения O ON O.Офис = 1 AND SL.L >= O.L AND SL.R <= O.R
                  INNER JOIN vwРасположения L ON L.L >= O.L AND L.R <= O.R
                  WHERE ST.КодТелефоннойСтанции = @StationId AND L.КодРасположения = S.КодРасположения
                    AND NOT EXISTS (SELECT 1 FROM ОборудованиеРасположение X WHERE X.КодОборудования = R.КодОборудования AND X.От > R.От)))
   THEN 1 ELSE 0 END -- работающая розетка офиса, свободная либо занятая текущим номером";

    /// <summary>Допустимость розетки на расположениях чернового оборудования. Собственная связь освобождается только от проверки занятости. Параметры: SocketId, PhoneId, LocationIds.</summary>
    public const string SELECT_ТелефонРозеткаДопустимаОборудования = @"
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM vwРозетки S
    WHERE S.КодРозетки = @SocketId
      AND S.Работает = 1
      AND (EXISTS (SELECT 1 FROM vwТелефонныеНомераИзменение P WHERE P.КодТелефонногоНомера = @PhoneId AND P.КодРозетки = @SocketId)
           OR (NOT EXISTS (SELECT 1 FROM vwТелефонныеНомераИзменение P WHERE P.КодРозетки = S.КодРозетки)
               AND NOT EXISTS (SELECT 1 FROM vwЛокальнаяСеть N WHERE N.КодРозетки = S.КодРозетки)))
      AND S.КодРасположения IN (@LocationIds))
   THEN 1 ELSE 0 END -- работающая розетка оборудования, свободная либо занятая текущим номером";

    /// <summary>Синхронизация порта Meridian.</summary>
    public const string SP_ТелефонСинхронизацияMeridian = "dbo.sp_Meridian_PortSync";

    /// <summary>Пункты меню Inventory типов 1–3 в порядке legacy КодТипаЗапроса, КодЗапроса. Параметров нет.</summary>
    public const string SELECT_ЗапросыДляМенюИнвентаризации = @"
SELECT КодЗапроса,       -- стабильный идентификатор запроса
       Запрос,           -- отображаемое название
       ТипЗапроса,       -- 1: инвентаризация; 2: несоответствия; 3: отчёты
       ФормаСписка,      -- внешняя форма либо @ключ конфигурации
       Пиктограмма,      -- изображение пункта меню
       ПунктМенюОтчета,  -- название контекстного отчёта
       ФормаОтчета       -- @ключ формы контекстного отчёта
FROM Запросы
WHERE ТипЗапроса IN (1, 2, 3)
ORDER BY ТипЗапроса, КодЗапроса";

    /// <summary>Строка меню Inventory по идентификатору запроса. Параметр: Id.</summary>
    public const string SELECT_ЗапросДляМенюИнвентаризации = @"
SELECT КодЗапроса,       -- стабильный идентификатор запроса
       Запрос,           -- отображаемое название
       ТипЗапроса,       -- тип запроса
       ФормаСписка,      -- внешняя форма либо @ключ конфигурации
       Пиктограмма,      -- изображение пункта меню
       ПунктМенюОтчета,  -- название контекстного отчёта
       ФормаОтчета       -- @ключ формы контекстного отчёта
FROM Запросы
WHERE КодЗапроса = @Id";

    /// <summary>Пакетный resolver токенов порта. {0} — параметризованные SELECT Token через UNION ALL; TOP 1 без ORDER BY сохраняет legacy.</summary>
    public const string SELECT_Оборудование_СсылкиПоТокенам = @"
SELECT T.Token, R.КодОборудования
FROM ({0}) T
CROSS APPLY (
    SELECT TOP 1 КодОборудования -- идентификатор оборудования
    FROM vwОборудование
    WHERE СетевоеИмя = T.Token -- точное сетевое имя
       OR (CHARINDEX('\', СетевоеИмя) > 0 AND СетевоеИмя LIKE '%\' + T.Token) -- доменный суффикс
       OR MACадрес = T.Token -- основной MAC
       OR MACадрес2 = T.Token -- второй MAC
       OR MACадресILO = T.Token -- MAC ILO
) R";

    /// <summary>Существующий порт локальной сети. Параметр: Id.</summary>
    public const string SELECT_ID_ЛокальнаяСеть = @"
SELECT N.КодЛокальнойСети, -- идентификатор порта
       N.КодОборудования, -- оборудование
       N.Порт, -- номер порта
       N.PoE, -- поддержка PoE
       N.КодРозетки, -- розетка
       N.КодЛицаЗаказчика, -- заказчик
       N.КодСотрудника, -- сотрудник
       N.Подключено, -- исходный текст подключения
       N.Примечания, -- исходное примечание
       N.СрокВозврата, -- календарный срок возврата
       N.Изменил, -- сотрудник изменения
       ISNULL(CH.Сотрудник, '') AS Сотрудник, -- имя изменившего
       N.Изменено -- время изменения UTC
FROM vwЛокальнаяСеть N
LEFT JOIN Сотрудники CH ON N.Изменил = CH.КодСотрудника
WHERE N.КодЛокальнойСети = @Id";

    /// <summary>Сетевое имя оборудования. Параметр: EquipmentId.</summary>
    public const string SELECT_СетевоеИмяОборудованияПоИдентификатору = @"
SELECT СетевоеИмя -- сетевое имя
FROM vwОборудование
WHERE КодОборудования = @EquipmentId";

    /// <summary>Свободные соседние порты. Параметр: КодЛокальнойСети.</summary>
    public const string SELECT_СоседниеПортыНеЗанятые = @"
SELECT КодЛокальнойСети, -- идентификатор порта
       СетевоеИмя + ', порт:' + CONVERT(varchar, Порт) AS Строка -- legacy-подпись
FROM dbo.fn_ЛокальнаяСеть_СоседниеПорты(@КодЛокальнойСети) X
WHERE Занят = 0
ORDER BY СетевоеИмя, Порт";

    /// <summary>Все соседние порты. Параметр: КодЛокальнойСети.</summary>
    public const string SELECT_СоседниеПортыВсе = @"
SELECT КодЛокальнойСети, -- идентификатор порта
       СетевоеИмя + ', порт:' + CONVERT(varchar, Порт) AS Строка -- legacy-подпись
FROM dbo.fn_ЛокальнаяСеть_СоседниеПорты(@КодЛокальнойСети) X
ORDER BY СетевоеИмя, Порт";

    /// <summary>Ближайший офис последней записи расположения оборудования. Параметр: КодОборудования.</summary>
    public const string SELECT_ОфисПоКодуОборудования = @"
DECLARE @LatestLocationId int;
SELECT TOP 1 @LatestLocationId = КодРасположения -- последнее расположение без предположения об офисе
FROM ОборудованиеРасположение
WHERE КодОборудования = @КодОборудования
ORDER BY От DESC;

SELECT TOP 1 O.КодРасположения -- ближайший офисный предок только последнего расположения
FROM vwРасположения L
INNER JOIN vwРасположения O ON O.Офис <> 0 AND O.L <= L.L AND O.R >= L.R
WHERE L.КодРасположения = @LatestLocationId
ORDER BY O.L DESC";

    /// <summary>Работает и свободна либо является текущей розеткой порта. Параметры: CurrentSocketId, OfficeLocationId, Search, LimitPlusOne.</summary>
    public const string SELECT_РозеткиДляСети = @"
SELECT TOP (@LimitPlusOne)
       S.КодРозетки, -- идентификатор
       S.Розетка, -- название
       S.Примечание, -- примечание
       S.КодРасположения, -- расположение
       S.Работает -- работает
FROM vwРозетки S
WHERE S.Работает = 1
  AND (S.КодРозетки = @CurrentSocketId
       OR (NOT EXISTS (SELECT 1 FROM vwТелефонныеНомераИзменение P WHERE P.КодРозетки = S.КодРозетки)
           AND NOT EXISTS (SELECT 1 FROM vwЛокальнаяСеть N WHERE N.КодРозетки = S.КодРозетки)))
  AND (@OfficeLocationId IS NULL OR EXISTS (
       SELECT 1 FROM vwРасположения Parent
       INNER JOIN vwРасположения Child ON Parent.L <= Child.L AND Parent.R >= Child.R
       WHERE Parent.КодРасположения = @OfficeLocationId AND Child.КодРасположения = S.КодРасположения))
  AND (@Search = '' OR S.Розетка LIKE '%' + @Search + '%')
ORDER BY S.Розетка";

    /// <summary>Розетка по ID без ограничения занятостью для восстановления текущего значения. Параметр: Id.</summary>
    public const string SELECT_РозеткаДляСетиПоИдентификатору = @"
SELECT КодРозетки, -- идентификатор
       Розетка, -- название
       Примечание, -- примечание
       КодРасположения, -- расположение
       Работает -- работает
FROM vwРозетки
WHERE КодРозетки = @Id";

    /// <summary>Обычное атомарное обновление порта с защитой от устаревшей записи.</summary>
    public const string UPDATE_ЛокальнаяСеть = @"
SET NOCOUNT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @SourceChanged datetime;
    SELECT @SourceChanged = Изменено
    FROM vwЛокальнаяСеть WITH (UPDLOCK, HOLDLOCK)
    WHERE КодЛокальнойСети = @Id;
    IF @SourceChanged IS NULL OR @SourceChanged <> @ExpectedChangedAt
    BEGIN ROLLBACK TRANSACTION; SELECT 1; RETURN; END; -- запись недоступна или устарела
    IF NOT EXISTS (SELECT 1 FROM ЛицаЗаказчики WHERE КодЛица = @PersonCustomerId)
    BEGIN ROLLBACK TRANSACTION; SELECT 2; RETURN; END; -- заказчик не существует
    UPDATE vwЛокальнаяСеть
    SET КодРозетки = @SocketId,
        КодЛицаЗаказчика = @PersonCustomerId,
        КодСотрудника = @EmployeeId,
        Подключено = @Connected,
        Примечания = @Description,
        СрокВозврата = @ReturnDate
    WHERE КодЛокальнойСети = @Id;
    COMMIT TRANSACTION;
    SELECT 0; -- успех
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @ErrorMessage nvarchar(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity int = ERROR_SEVERITY();
    DECLARE @ErrorState int = ERROR_STATE();
    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;";

    /// <summary>Атомарное переключение на свободный соседний порт; сроки возврата обеих записей не меняются.</summary>
    public const string SWITCH_ЛокальнаяСеть = @"
SET NOCOUNT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @SourceChanged datetime;
    DECLARE @TargetFree bit;
    SELECT @SourceChanged = Изменено
    FROM vwЛокальнаяСеть WITH (UPDLOCK, HOLDLOCK)
    WHERE КодЛокальнойСети = @Id;
    SELECT @TargetFree = CASE WHEN КодРозетки IS NULL AND КодСотрудника IS NULL AND Подключено = '' THEN 1 ELSE 0 END
    FROM vwЛокальнаяСеть WITH (UPDLOCK, HOLDLOCK)
    WHERE КодЛокальнойСети = @TargetId;
    IF @SourceChanged IS NULL OR @SourceChanged <> @ExpectedChangedAt
    BEGIN ROLLBACK TRANSACTION; SELECT 1; RETURN; END; -- источник недоступен или устарел
    IF NOT EXISTS (SELECT 1 FROM ЛицаЗаказчики WHERE КодЛица = @PersonCustomerId)
    BEGIN ROLLBACK TRANSACTION; SELECT 2; RETURN; END; -- заказчик не существует
    IF @TargetFree <> 1 OR @TargetId = @Id OR NOT EXISTS (
        SELECT 1 FROM dbo.fn_ЛокальнаяСеть_СоседниеПорты(@Id) WHERE КодЛокальнойСети = @TargetId)
    BEGIN ROLLBACK TRANSACTION; SELECT 3; RETURN; END; -- цель не является свободным соседним портом
    UPDATE vwЛокальнаяСеть
    SET КодЛицаЗаказчика = @PersonCustomerId, Примечания = '', Подключено = '', КодСотрудника = NULL, КодРозетки = NULL
    WHERE КодЛокальнойСети = @Id;
    UPDATE vwЛокальнаяСеть
    SET КодЛицаЗаказчика = @PersonCustomerId, Примечания = @Description, Подключено = @Connected,
        КодСотрудника = @EmployeeId, КодРозетки = @SocketId
    WHERE КодЛокальнойСети = @TargetId;
    COMMIT TRANSACTION;
    SELECT 0; -- успех
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @ErrorMessage nvarchar(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity int = ERROR_SEVERITY();
    DECLARE @ErrorState int = ERROR_STATE();
    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;";

    /// <summary>Атомарный обмен двух соседних портов с временным освобождением розетки источника; сроки возврата не меняются.</summary>
    public const string EXCHANGE_ЛокальнаяСеть = @"
SET NOCOUNT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @SourceChanged datetime;
    DECLARE @TargetCustomerId int;
    DECLARE @TargetDescription varchar(max);
    DECLARE @TargetConnected varchar(max);
    DECLARE @TargetEmployeeId int;
    DECLARE @TargetSocketId int;
    SELECT @SourceChanged = Изменено
    FROM vwЛокальнаяСеть WITH (UPDLOCK, HOLDLOCK)
    WHERE КодЛокальнойСети = @Id;
    SELECT @TargetCustomerId = КодЛицаЗаказчика,
           @TargetDescription = Примечания,
           @TargetConnected = Подключено,
           @TargetEmployeeId = КодСотрудника,
           @TargetSocketId = КодРозетки
    FROM vwЛокальнаяСеть WITH (UPDLOCK, HOLDLOCK)
    WHERE КодЛокальнойСети = @TargetId;
    IF @SourceChanged IS NULL OR @SourceChanged <> @ExpectedChangedAt
    BEGIN ROLLBACK TRANSACTION; SELECT 1; RETURN; END; -- источник недоступен или устарел
    IF NOT EXISTS (SELECT 1 FROM ЛицаЗаказчики WHERE КодЛица = @PersonCustomerId)
    BEGIN ROLLBACK TRANSACTION; SELECT 2; RETURN; END; -- заказчик не существует
    IF @TargetCustomerId IS NULL OR @TargetId = @Id OR NOT EXISTS (
        SELECT 1 FROM dbo.fn_ЛокальнаяСеть_СоседниеПорты(@Id) WHERE КодЛокальнойСети = @TargetId)
    BEGIN ROLLBACK TRANSACTION; SELECT 3; RETURN; END; -- цель не является соседним портом
    UPDATE vwЛокальнаяСеть
    SET КодЛицаЗаказчика = @TargetCustomerId, Примечания = @TargetDescription, Подключено = @TargetConnected,
        КодСотрудника = @TargetEmployeeId, КодРозетки = NULL
    WHERE КодЛокальнойСети = @Id;
    UPDATE vwЛокальнаяСеть
    SET КодЛицаЗаказчика = @PersonCustomerId, Примечания = @Description, Подключено = @Connected,
        КодСотрудника = @EmployeeId, КодРозетки = @SocketId
    WHERE КодЛокальнойСети = @TargetId;
    UPDATE vwЛокальнаяСеть
    SET КодРозетки = @TargetSocketId
    WHERE КодЛокальнойСети = @Id;
    COMMIT TRANSACTION;
    SELECT 0; -- успех
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @ErrorMessage nvarchar(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity int = ERROR_SEVERITY();
    DECLARE @ErrorState int = ERROR_STATE();
    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;";

    /// <summary>Проверка актуального PoE-порта и вызов legacy-процедуры. Параметр: КодЛокальнойСети; timeout 120 секунд.</summary>
    public const string EXEC_ResetPoEport = @"
IF NOT EXISTS (SELECT 1 FROM vwЛокальнаяСеть WHERE КодЛокальнойСети = @КодЛокальнойСети AND PoE = 1)
    RAISERROR('Операция PoE недоступна для этой записи.', 16, 1);
ELSE
    EXEC dbo.sp_ResetPoEport @КодЛокальнойСети = @КодЛокальнойСети;";
}
