using System.Globalization;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Территориальный fallback производственного календаря. Таблица Территории, ключ КодТерритории.</summary>
public enum Территории
{
    /// <summary>Российская Федерация.</summary>
    Российская_Федерация = 188,
}

/// <summary>Вариант фактического подтверждения дежурства.</summary>
public enum DutyConfirmation
{
    /// <summary>Дежурил плановый инженер.</summary>
    Planned = 1,
    /// <summary>Дежурил другой инженер.</summary>
    Other = 2,
    /// <summary>Никто не дежурил.</summary>
    Nobody = 3,
}

/// <summary>План и фактические данные дежурства, включая версию строки.</summary>
public sealed record DutyRow(DateTime DutyDate, int PlannedId, int? ActualId, int? EmployerId, byte? WorkDay,
    decimal? Payment, DateTime? TimeOff, byte Closed, int ChangedById, DateTime ChangedAt, string? PlannedName,
    string? ActualName, string? ChangedByName);

/// <summary>Редактируемые данные карточки. Даты отражают календарные дни.</summary>
public sealed record DutyDraft(DateOnly? Date, int? PlannedId, DutyConfirmation? Confirmation = null, int? ActualId = null,
    bool WantsTimeOff = false, DateOnly? TimeOff = null, bool Close = false)
{
    /// <summary>Сотрудник для факта и расчёта производственного календаря.</summary>
    public int? EffectiveEngineer(bool fact) => !fact ? PlannedId : Confirmation switch
    {
        DutyConfirmation.Planned => PlannedId,
        DutyConfirmation.Other => ActualId,
        DutyConfirmation.Nobody => null,
        _ => PlannedId,
    };

    /// <summary>Дата отгула, применимая для выбранного сотрудника и календаря.</summary>
    public DateOnly? EffectiveTimeOff(bool? workDay) => WantsTimeOff && workDay == false
        && Confirmation is DutyConfirmation.Planned or DutyConfirmation.Other ? TimeOff : null;

    /// <summary>Создаёт рабочую копию. Отсутствующий факт остаётся явным отсутствием выбора.</summary>
    public static DutyDraft FromRow(DutyRow row) => new(DateOnly.FromDateTime(row.DutyDate), row.PlannedId,
        row.ActualId is null ? null : row.ActualId == row.PlannedId ? DutyConfirmation.Planned : DutyConfirmation.Other,
        row.ActualId, row.TimeOff.HasValue, row.TimeOff is { } day ? DateOnly.FromDateTime(day) : null);

    /// <summary>Проверяет поля, действующие для ветви записи.</summary>
    public string? Validate(bool fact, bool? workDay)
    {
        if (Date is null) return "Невозможно выполнить сохранение, т.к. не указана дата дежурства";
        if (PlannedId is null or <= 0) return "Невозможно выполнить сохранение, т.к. не указан дежурный инженер";
        if (!fact) return null;
        if (Confirmation is not (DutyConfirmation.Planned or DutyConfirmation.Other or DutyConfirmation.Nobody))
            return "Невозможно выполнить сохранение, необходимо выбрать вариант закрытия дежурства.";
        if (Confirmation == DutyConfirmation.Nobody && !Close)
            return "Вариант закрытия дежурства 'Никто не дежурил' невозможно сохранить без подтверждения закрытия дежурства";
        if (Confirmation == DutyConfirmation.Other && ActualId is null or <= 0)
            return "Невозможно выполнить сохранение, необходимо указать сотрудника, который дежурил";
        if (WantsTimeOff && workDay == false && Confirmation != DutyConfirmation.Nobody && TimeOff is null)
            return "Невозможно выполнить сохранение, необходимо указать дату отгула";
        return EffectiveTimeOff(workDay) is { } day && day <= Date
            ? "Невозможно выполнить сохранение, дата отгула должна быть больше даты дежурства" : null;
    }
}

/// <summary>Серверная дата и права записи из текущего SQL-контекста.</summary>
public sealed record DutyContext(DateTime Today, bool CanInsert, bool CanUpdate, bool CanChangeTimeOff)
{
    /// <summary>Календарный день SQL Server.</summary>
    public DateOnly BusinessDay => DateOnly.FromDateTime(Today);
}

/// <summary>Инженер и месячная статистика для выпадающего списка.</summary>
public sealed record DutyEngineer(int Id, string Name, int? Total, int? Weekends)
{
    /// <summary>Подпись числа дежурств, в том числе в выходные.</summary>
    public string Statistics => Total is { } total ? $"{total} (в вых: {Weekends ?? 0})" : "";
}

/// <summary>Операция отклонена до подтверждённой записи.</summary>
public sealed class DutyRejectedException(string message, Exception? inner = null) : InvalidOperationException(message, inner);

/// <summary>Форматы календарного ключа карточки.</summary>
public static class DutyKey
{
    /// <summary>Разбирает канонический ключ и однозначные значения даты из ссылок грида.</summary>
    public static bool TryParse(string value, out DateOnly date)
    {
        if (DateOnly.TryParseExact(value, ["yyyyMMdd", "yyyy-MM-dd", "dd.MM.yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return date > DateOnly.MinValue && date < DateOnly.MaxValue;
        if (DateTime.TryParseExact(value, ["MM/dd/yyyy HH:mm:ss", "dd.MM.yyyy H:mm:ss", "M/d/yyyy h:mm:ss tt", "yyyy-MM-ddTHH:mm:ss.FFFFFFF"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp)
            && timestamp.TimeOfDay == TimeSpan.Zero)
        {
            date = DateOnly.FromDateTime(timestamp);
            return date > DateOnly.MinValue && date < DateOnly.MaxValue;
        }
        date = default;
        return false;
    }
}

