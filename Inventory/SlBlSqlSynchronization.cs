using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Проверка полномочий на изменение конкретного списка, предоставляемая владельцем почтового сервиса.</summary>
public interface ISlBlMailboxWritePolicy
{
    /// <summary>Разрешает сотруднику запись конкретного адреса и вида списка.</summary>
    Task<bool> AllowsAsync(Employee actor, string address, SlBlKind kind, CancellationToken ct);
}

/// <summary>SQL-адаптер синхронизации с проверкой владельца, адреса и совместимой процедуры.</summary>
public sealed class SlBlSqlSynchronization(DbManager db, string? principal, ISlBlMailboxWritePolicy? policy) : ISlBlSynchronization
{
    /// <inheritdoc />
    public async Task<bool> CanWriteAsync(string address, SlBlKind kind, CancellationToken ct) =>
        await AuthorizeAsync(address, kind, ct) is not null;

    private async Task<Employee?> AuthorizeAsync(string address, SlBlKind kind, CancellationToken ct)
    {
        if (policy is null || !Enum.IsDefined(kind)) return null;
        var actor = await Employee.GetCurrentAsync(db, principal, ct);
        return actor is not null && await policy.AllowsAsync(actor, address, kind, ct)
            && await SlBlData.FindAddressAsync(db, address, ct) == address
            && (await db.QueryRequiredAsync<bool>(SQLQueries.SELECT_SlBlProcedureAvailable,
                new { Name = SQLQueries.SP_SlBlSynchronize, AddressLength = address.Length, ListLength = SlBlData.MaxLength }, ct)).Single()
            ? actor : null;
    }

    /// <inheritdoc />
    public async Task<string> ReadOriginalAsync(string address, SlBlKind kind, CancellationToken ct) =>
        (await SlBlData.ReadAsync(db, address, ct))?.GetText(kind) ?? "";

    /// <inheritdoc />
    public async Task DispatchAsync(SlBlIntent intent, CancellationToken ct)
    {
        var actor = await AuthorizeAsync(intent.Address, intent.Kind, ct);
        if (actor is null)
            throw new SlBlNotDispatchedException("Команда не отправлена. Право синхронизации больше не подтверждено.");
        if (SlBlData.Validate(intent.Desired) is not null)
            throw new SlBlNotDispatchedException("Команда не отправлена. Снимок списка не подтверждён.");
        if ((await AuthorizeAsync(intent.Address, intent.Kind, ct))?.Id != actor.Id ||
            (await SlBlData.ReadAsync(db, intent.Address, ct))?.GetText(intent.Kind) != intent.Original)
            throw new SlBlNotDispatchedException("Команда не отправлена. Изменились права или исходный список.");

        ct.ThrowIfCancellationRequested();
        await db.RunDbAsync(connection => connection.ExecuteAsync(new CommandDefinition(SQLQueries.SP_SlBlSynchronize,
            new { AddressSet = intent.Address, BlockSet = (byte)intent.Kind, SlBl = intent.Desired },
            commandType: CommandType.StoredProcedure, commandTimeout: 180, cancellationToken: ct)));
    }

    /// <inheritdoc />
    public async Task<SlBlDifferences> CompareAsync(SlBlIntent intent, CancellationToken ct)
    {
        if (await Employee.GetCurrentAsync(db, principal, ct) is null ||
            await SlBlData.FindAddressAsync(db, intent.Address, ct) != intent.Address)
            throw new UnauthorizedAccessException("Ящик недоступен для сверки.");
        return await SlBlData.CompareAsync(db, new(intent.Address, intent.Kind), intent.Desired, ct);
    }
}
