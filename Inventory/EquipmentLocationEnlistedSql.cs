using System.Data;
using System.Data.Common;
using System.Text;
using System.Xml.Linq;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

internal static class EquipmentLocationEnlistedSql
{
    internal static void AppendPlan(StringBuilder sql, DynamicParameters parameters, IReadOnlyList<int> simIds)
    {
        var plan = new XElement("sim", simIds.Select(id => new XElement("id", id)));
        parameters.Add("@ExpectedSimXml", plan.ToString(SaveOptions.DisableFormatting), DbType.Xml);
        sql.AppendLine("""
            DECLARE @ExpectedSim TABLE (EquipmentId int NOT NULL PRIMARY KEY);
            INSERT @ExpectedSim (EquipmentId)
            SELECT sim.Item.value('(text())[1]', 'int')
            FROM @ExpectedSimXml.nodes('/sim/id') AS sim(Item);
            """);
    }

    internal static async Task<int[]> ReadPlanAsync(DbConnection connection, DbTransaction? transaction,
        EquipmentLocationPeriod location, CancellationToken ct)
    {
        var rows = (await connection.QueryAsync<PropagationTarget>(new CommandDefinition(
            SQLQueries.SELECT_ОборудованиеРасположениеПереносSim,
            new { КодОборудования = location.EquipmentId, От = location.FromUtc, КодРасположения = location.LocationId },
            transaction, commandType: CommandType.Text, cancellationToken: ct))).ToArray();
        if (rows.Any(row => row.EquipmentId <= 0 || row.EquipmentId == location.EquipmentId || row.SourceRowCount != 1) ||
            rows.Select(row => row.EquipmentId).Distinct().Count() != rows.Length)
            throw new EquipmentPeriodWriteRejectedException("Не удалось однозначно определить связанные SIM для перемещения.");
        return rows.Select(row => row.EquipmentId).Order().ToArray();
    }

    private sealed class PropagationTarget
    {
        /// <summary>Оборудование связанной SIM из актуального плана.</summary>
        public int EquipmentId { get; set; }
        /// <summary>Число исходных строк текущего расположения этой SIM.</summary>
        public long SourceRowCount { get; set; }
    }
}
