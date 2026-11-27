using System.Data;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

internal static class InventoryPresenceData
{
    internal const string HeartbeatProcedure = "dbo.ClayzorPresenceHeartbeat";
    internal const string LeaveProcedure = "dbo.ClayzorPresenceLeave";
    private const int CommandTimeoutSeconds = 5;

    internal sealed record Heartbeat(Guid LeaseId, bool Renew, byte[] EntityHash,
        int EmployeeId, string DisplayName, byte Activity, int LifetimeMilliseconds);

    internal sealed class Participant
    {
        /// <summary>Подтверждённый код сотрудника.</summary>
        public int EmployeeId { get; init; }
        /// <summary>Разрешённое имя для списка присутствующих.</summary>
        public string DisplayName { get; init; } = "";
        /// <summary>Состояние присутствия, принятое от сервера приложения.</summary>
        public byte Activity { get; init; }
    }

    internal static Task<IEnumerable<Participant>> HeartbeatAsync(DbManager db, Heartbeat heartbeat, CancellationToken ct) =>
        db.RunDbAsync(connection =>
        {
            ct.ThrowIfCancellationRequested();
            return connection.QueryAsync<Participant>(new CommandDefinition(HeartbeatProcedure, heartbeat,
                commandType: CommandType.StoredProcedure, commandTimeout: CommandTimeoutSeconds, cancellationToken: ct));
        });

    internal static Task LeaveAsync(DbManager db, Guid lease, CancellationToken ct) =>
        db.RunDbAsync(connection =>
        {
            ct.ThrowIfCancellationRequested();
            return connection.ExecuteAsync(new CommandDefinition(LeaveProcedure, new { LeaseId = lease },
                commandType: CommandType.StoredProcedure, commandTimeout: CommandTimeoutSeconds, cancellationToken: ct));
        });
}
