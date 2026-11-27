using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

internal static class EquipmentPeriodJournalWrite
{
    internal static DateTime NormalizeStartUtc(DateTime value) => DateTime.SpecifyKind(new SqlDateTime(value).Value, DateTimeKind.Utc);

    internal static async Task<int> ExecuteAsync(DbConnection connection, DbTransaction transaction, string sql,
        object parameters, int equipmentId, CancellationToken ct, Func<int, string?>? explainRefusal = null)
    {
        var proof = await connection.QuerySingleOrDefaultAsync<PeriodWriteProof>(new CommandDefinition(
            sql, parameters, transaction, commandType: CommandType.Text, cancellationToken: ct));
        var rejection = (proof?.Status is int status ? explainRefusal?.Invoke(status) : null) ?? (proof?.Status switch
        {
            1 => "Исходный период изменился или больше недоступен для редактирования. Обновите данные.",
            2 => "Период с таким началом уже существует. Обновите данные.",
            3 => "База не подтвердила сохранение периода.",
            _ => null,
        });
        if (rejection is not null) throw new EquipmentPeriodWriteRejectedException(rejection);
        if (proof is not { Status: 0, ConfirmedEquipmentId: > 0 } || proof.ConfirmedEquipmentId != equipmentId)
            throw new InvalidOperationException("База не подтвердила точный ключ периода оборудования.");
        return equipmentId;
    }

    private sealed class PeriodWriteProof
    {
        /// <summary>Именованный статус операции с периодом.</summary>
        public int? Status { get; set; }
        /// <summary>Код оборудования подтверждённого периода.</summary>
        public int? ConfirmedEquipmentId { get; set; }
    }
}

internal sealed class EquipmentPeriodWriteRejectedException(string message) : InvalidOperationException(message);
