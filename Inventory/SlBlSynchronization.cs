namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Неизменяемый снимок одного явно подтверждённого действия.</summary>
public sealed record SlBlIntent(Guid OperationId, string Address, SlBlKind Kind, string Original, string Desired);

/// <summary>Серверный адаптер текущего пользователя и разрешённой операции для конкретного адреса.</summary>
public interface ISlBlSynchronization
{
    /// <summary>Проверяет право изменения конкретного списка.</summary>
    Task<bool> CanWriteAsync(string address, SlBlKind kind, CancellationToken ct);

    /// <summary>Возвращает актуальное исходное содержимое списка.</summary>
    Task<string> ReadOriginalAsync(string address, SlBlKind kind, CancellationToken ct);

    /// <summary>Передаёт подтверждённую команду на синхронизацию.</summary>
    Task DispatchAsync(SlBlIntent intent, CancellationToken ct);

    /// <summary>Сравнивает фактический список с переданной командой.</summary>
    Task<SlBlDifferences> CompareAsync(SlBlIntent intent, CancellationToken ct);
}

/// <summary>Исход выполнения или сверки синхронизации.</summary>
public enum SlBlOutcome { Rejected, Verified, Partial, Unknown }

/// <summary>Результат операции и оставшиеся различия.</summary>
public sealed record SlBlResult(SlBlOutcome Outcome, SlBlDifferences? Differences = null, string? Message = null);

/// <summary>Выполнение и безопасная сверка одной подтверждённой синхронизации.</summary>
public static class SlBlSynchronization
{
    /// <summary>Проверяет снимок и права, выполняет не более одной отправки и сверяет результат.</summary>
    public static async Task<SlBlResult> ExecuteAsync(ISlBlSynchronization adapter, SlBlIntent intent, CancellationToken ct)
    {
        if (intent.OperationId == Guid.Empty || !Enum.IsDefined(intent.Kind) ||
            !SlBlKey.TryParse(intent.Address, out var parsed) || parsed?.Address != intent.Address ||
            parsed.Kind is not null || SlBlData.Validate(intent.Desired) is not null)
            return new(SlBlOutcome.Rejected);

        try
        {
            if (!await adapter.CanWriteAsync(intent.Address, intent.Kind, ct) ||
                await adapter.ReadOriginalAsync(intent.Address, intent.Kind, ct) != intent.Original)
                return new(SlBlOutcome.Rejected);
            ct.ThrowIfCancellationRequested();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // До вызова адаптера синхронизации почтовый эффект невозможен.
            return new(SlBlOutcome.Rejected, Message: "Команда не отправлена. Не удалось проверить права или прочитать исходный список.");
        }
        try
        {
            try { await adapter.DispatchAsync(intent, ct); }
            catch (SlBlNotDispatchedException ex) { return new(SlBlOutcome.Rejected, Message: ex.Message); }
            return await ReconcileAsync(adapter, intent, ct);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new(SlBlOutcome.Unknown);
        }
    }

    /// <summary>Только читает фактический список для сохранённого intent, не повторяя отправку.</summary>
    public static async Task<SlBlResult> ReconcileAsync(ISlBlSynchronization adapter, SlBlIntent intent, CancellationToken ct)
    {
        var diff = await adapter.CompareAsync(intent, ct);
        return new(string.IsNullOrEmpty(diff.NotDeleted) && string.IsNullOrEmpty(diff.NotSaved)
            ? SlBlOutcome.Verified : SlBlOutcome.Partial, diff);
    }
}

/// <summary>Адаптер подтвердил отказ до передачи команды почтовой процедуре.</summary>
public sealed class SlBlNotDispatchedException(string message) : InvalidOperationException(message);
