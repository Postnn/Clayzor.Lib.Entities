using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Text;
using Clayzor.Lib.DALC;
using Dapper;

namespace Clayzor.Lib.Entities.Inventory;

/// <summary>Ссылка на оборудование, найденная по исходному токену текстового поля.</summary>
public sealed record NetEquipmentLink(string Token, int EquipmentId);

/// <summary>Независимые списки ссылок для полей «Подключено» и «Примечание».</summary>
public sealed class NetEquipmentLinkResolution
{
    /// <summary>Ссылки под полем «Подключено».</summary>
    public IReadOnlyList<NetEquipmentLink> Connected { get; init; } = [];

    /// <summary>Ссылки под полем «Примечание».</summary>
    public IReadOnlyList<NetEquipmentLink> Description { get; init; } = [];
}

/// <summary>Минимальная проекция результата пакетного сопоставления токена с оборудованием.</summary>
public sealed class NetEquipmentLinkMatch
{
    /// <summary>Исходный токен после legacy-преобразования.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Идентификатор найденного оборудования.</summary>
    [Column(EquipmentColumns.КодОборудования)]
    public int EquipmentId { get; set; }
}

/// <summary>Пакетно сопоставляет токены двух текстовых полей с оборудованием по пяти условиям legacy.</summary>
public static class NetEquipmentLinkResolver
{
    private const int BatchSize = 500;

    /// <summary>Формирует ссылки, не изменяя исходные тексты и не смешивая результаты двух полей.</summary>
    public static async Task<NetEquipmentLinkResolution> ResolveAsync(
        DbManager db,
        string? connected,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var connectedTokens = NetEquipmentLinkExtractor.Extract(connected);
        var descriptionTokens = NetEquipmentLinkExtractor.Extract(description);
        var tokens = connectedTokens.Concat(descriptionTokens)
            .Select(token => token.Trim())
            .Where(token => token.Length > 0)
            .Distinct()
            .ToList();
        var matches = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var offset = 0; offset < tokens.Count; offset += BatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = tokens.Skip(offset).Take(BatchSize).ToList();
            var parameters = new DynamicParameters();
            var sources = new StringBuilder();
            for (var index = 0; index < batch.Count; index++)
            {
                var name = $"@Token{offset + index}";
                parameters.Add(name, batch[index], DbType.String, size: 50);
                if (sources.Length > 0)
                    sources.Append(" UNION ALL ");
                sources.Append("SELECT ").Append(name).Append(" AS Token");
            }

            var sql = string.Format(SQLQueries.SELECT_Оборудование_СсылкиПоТокенам, sources.ToString());
            var rows = await db.QueryRequiredAsync<NetEquipmentLinkMatch>(sql, parameters, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var row in rows)
                if (row.EquipmentId > 0 && !matches.ContainsKey(row.Token))
                    matches.Add(row.Token, row.EquipmentId);
        }

        return new NetEquipmentLinkResolution
        {
            Connected = MapTokens(connectedTokens, matches),
            Description = MapTokens(descriptionTokens, matches),
        };
    }

    private static IReadOnlyList<NetEquipmentLink> MapTokens(
        IEnumerable<string> tokens,
        IReadOnlyDictionary<string, int> matches) => tokens
        .Where(token => matches.ContainsKey(token.Trim()))
        .Select(token => new NetEquipmentLink(token, matches[token.Trim()]))
        .ToList();
}
