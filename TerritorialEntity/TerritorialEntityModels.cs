using System.Globalization;

namespace Clayzor.Lib.Entities.TerritorialEntity;

/// <summary>Справочник dbo.ТипыТерриитории, ключ КодТТерритории.</summary>
public enum ТипыТерриитории
{
    /// <summary>Часть света.</summary>
    Часть_света = 1,
    /// <summary>Страны.</summary>
    Страны = 2,
    /// <summary>Области,Штаты.</summary>
    Области_Штаты = 3,
    /// <summary>Населённые пункты.</summary>
    Населённые_пункты = 4,
    /// <summary>Объединение стран.</summary>
    Объединение_стран = 5,
}

/// <summary>Запись справочника типов с оригинальной подписью.</summary>
public sealed record TerritorialEntityOption(int Id, string Name) : ILookupEntity;

/// <summary>Снимок территории, включая неизменяемый контекст родителя и аудит.</summary>
public sealed record TerritorialEntityRecord
{
    /// <summary>Код территории.</summary>
    public int Id { get; init; }
    /// <summary>Ключ родителя либо NULL для корня.</summary>
    public int? ParentId { get; init; }
    /// <summary>Тип территории из справочника.</summary>
    public ТипыТерриитории Type { get; init; }
    /// <summary>Основное русское название.</summary>
    public string Name { get; init; } = "";
    /// <summary>Альтернативное русское название.</summary>
    public string AlternateName { get; init; } = "";
    /// <summary>Международное английское название.</summary>
    public string Caption { get; init; } = "";
    /// <summary>Альтернативное международное название.</summary>
    public string AlternateCaption { get; init; } = "";
    /// <summary>Краткое буквенное обозначение.</summary>
    public string Abbreviation { get; init; } = "";
    /// <summary>Код ОКСМ либо NULL при отсутствии.</summary>
    public int? CountryClassification { get; init; }
    /// <summary>Строковый телефонный код страны.</summary>
    public string CountryCode { get; init; } = "";
    /// <summary>Направление.</summary>
    public string Direction { get; init; } = "";
    /// <summary>Код автора последнего изменения.</summary>
    public int ChangedBy { get; init; }
    /// <summary>Подпись автора либо NULL, если сотрудник недоступен.</summary>
    public string? ChangedByName { get; init; }
    /// <summary>Момент последнего изменения в UTC.</summary>
    public DateTime ChangedAt { get; init; }
}

/// <summary>Снимок телефонного кода с исходным строковым ключом.</summary>
public sealed record PhoneCodeEntityRecord
{
    /// <summary>Исходный строковый ключ телефонного кода.</summary>
    public string Code { get; init; } = "";
    /// <summary>Ключ территории, к которой относится код.</summary>
    public int TerritoryId { get; init; }
    /// <summary>Длина кода области по справочнику.</summary>
    public int Length { get; init; }
    /// <summary>Ключ типа связи, в том числе допустимый 0.</summary>
    public int TypeId { get; init; }
    /// <summary>Комментарий, нормализованный в непустой объект строки.</summary>
    public string Comment { get; init; } = "";
    /// <summary>Код автора последнего изменения.</summary>
    public int ChangedBy { get; init; }
    /// <summary>Подпись автора либо NULL, если сотрудник недоступен.</summary>
    public string? ChangedByName { get; init; }
    /// <summary>Момент последнего изменения в UTC.</summary>
    public DateTime ChangedAt { get; init; }
}

/// <summary>Территория для выпадающего поиска и исторического выбранного значения.</summary>
public sealed record TerritorialEntityLookup
{
    /// <summary>Ключ территории.</summary>
    public int Id { get; init; }
    /// <summary>Тип территории.</summary>
    public ТипыТерриитории Type { get; init; }
    /// <summary>Русское название.</summary>
    public string Name { get; init; } = "";
    /// <summary>Международное название.</summary>
    public string Caption { get; init; } = "";
    /// <summary>Телефонный код страны без числовой нормализации.</summary>
    public string CountryCode { get; init; } = "";

    /// <summary>Подпись по текущей культуре с русским значением в качестве fallback.</summary>
    public string DisplayName => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ||
                                 string.IsNullOrWhiteSpace(Caption) ? Name : Caption;

    /// <summary>Типы территории, допустимые для выбора в карточке телефонного кода.</summary>
    public bool IsPhoneCodeTerritory => Type is ТипыТерриитории.Страны or ТипыТерриитории.Области_Штаты
        or ТипыТерриитории.Населённые_пункты;
}

/// <summary>Нативные данные узла. Настоящий корень имеет NULL в ParentId.</summary>
public sealed record TerritorialEntityTreeRow
{
    /// <summary>Ключ настоящей территории.</summary>
    public int Id { get; init; }
    /// <summary>Ключ родителя внутри области показа.</summary>
    public int? ParentId { get; init; }
    /// <summary>Название узла.</summary>
    public string Text { get; init; } = "";
    /// <summary>Левая граница вложенного множества.</summary>
    public int Left { get; init; }
    /// <summary>Правая граница вложенного множества.</summary>
    public int Right { get; init; }
    /// <summary>Тип территории.</summary>
    public ТипыТерриитории Type { get; init; }
    /// <summary>У узла есть доступные непосредственные дети.</summary>
    public bool HasChildren { get; init; }
    /// <summary>Узел входит в отображаемые совпадения запроса.</summary>
    public bool IsMatch { get; init; }
    /// <summary>Отображаемые совпадения включают одного из потомков узла.</summary>
    public bool HasMatchChildren { get; init; }
}

/// <summary>Данные фильтра и полный счётчик без предков и отсечения.</summary>
public sealed record TerritorialEntityTreeSearch(int Total, IReadOnlyList<TerritorialEntityTreeRow> Rows);

/// <summary>Доказанный отказ до записи либо отказ, после которого транзакция не была подтверждена.</summary>
public sealed class TerritorialEntityRuleException : InvalidOperationException
{
    /// <summary>Создаёт предметный отказ, не относящийся к конкурентному изменению.</summary>
    public TerritorialEntityRuleException(string message) : this(message, false) { }

    internal TerritorialEntityRuleException(string message, bool baselineConflict) : base(message) => BaselineConflict = baselineConflict;

    /// <summary>Прочитанная в транзакции запись отличается от исходного снимка, в том числе при удалении.</summary>
    public bool BaselineConflict { get; }
}

/// <summary>Общие для формы и доменного сохранения правила схемы и ввода.</summary>
public static class TerritorialEntityRules
{
    /// <summary>ОКСМ: пробелы и разделители разрядов удаляются как в legacy. Пусто даёт NULL.</summary>
    public static bool TryParseCountryClassification(string? text, out int? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        var digits = string.Concat(text.Where(c => !char.IsWhiteSpace(c) && c is not ('.' or ',')));
        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0)
            return false;
        value = number;
        return true;
    }

    /// <summary>Длина области обязательна и хранится в tinyint. Формат не округляется.</summary>
    public static bool TryParseAreaLength(string? text, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value is >= 1 and <= byte.MaxValue;

    /// <summary>Проверяет строку на обязательность по правилам legacy.</summary>
    public static bool IsRequiredText(string? value) => !string.IsNullOrWhiteSpace(value) && value != "0";

    /// <summary>Проверяет, что поля территории укладываются в установленную схему.</summary>
    public static IReadOnlyList<string> Validate(TerritorialEntityRecord value)
    {
        var errors = new List<string>();
        if (!Enum.IsDefined(value.Type)) errors.Add("Выберите тип территории.");
        if (!IsRequiredText(value.Name) || value.Name.Length > 80) errors.Add("Укажите название территории на русском языке (до 80 символов).");
        if (!IsRequiredText(value.CountryCode) || value.CountryCode.Length > 20) errors.Add("Укажите телефонный код страны (до 20 символов).");
        if (!IsRequiredText(value.Direction) || value.Direction.Length > 300) errors.Add("Укажите направление (до 300 символов).");
        if (value.AlternateName.Length > 80 || value.Caption.Length > 50 || value.AlternateCaption.Length > 50 || value.Abbreviation.Length > 4)
            errors.Add("Проверьте длину альтернативных названий и аббревиатуры.");
        if (value.CountryClassification is <= 0) errors.Add("Код ОКСМ должен быть положительным целым числом.");
        return errors;
    }

    /// <summary>Проверяет редактируемые поля телефонного кода. Нулевой тип связи допустим.</summary>
    public static IReadOnlyList<string> Validate(PhoneCodeEntityRecord value)
    {
        var errors = new List<string>();
        if (value.TerritoryId <= 0) errors.Add("Выберите территорию.");
        if (!IsRequiredText(value.Code) || value.Code.Length > 20) errors.Add("Укажите телефонный код (до 20 символов).");
        if (value.Length is < 1 or > byte.MaxValue) errors.Add("Укажите целую длину кода области от 1 до 255.");
        if (value.TypeId < 0) errors.Add("Выберите тип телефонной связи.");
        if (value.Comment.Length > 100) errors.Add("Комментарий не должен превышать 100 символов.");
        return errors;
    }
}
