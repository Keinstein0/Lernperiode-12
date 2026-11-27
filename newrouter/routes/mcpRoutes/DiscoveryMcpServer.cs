using System.ComponentModel;
using A2A;
using ModelContextProtocol.Server;
using newrouter.models;
using newrouter.services;

namespace newrouter.routes.mcpRoutes;

[McpServerToolType]
public sealed class DiscoveryMcpServer
{
    [McpServerTool(Name = "get_nodes"), Description("Filters nodes the caller is allowed to read and ranks them by embedding cosine similarity against query.")]
    public static async Task<IReadOnlyList<RankedNodeResult>> GetNodesAsync(
        IAuthService authService,
        INodeRegistryService registry,
        IEmbeddingService embeddingService,
        string query,
        string? capability_filter = null,
        CancellationToken ct = default)
    {
        var callerToken = authService.GetCallerTokenOrThrow();
        var activeNodes = registry.GetActiveNodes();

        if (!string.IsNullOrWhiteSpace(capability_filter))
        {
            activeNodes = activeNodes
                .Where(n => n.Capabilities.Contains(capability_filter, StringComparer.OrdinalIgnoreCase))
                .ToArray();
        }

        if (activeNodes.Count == 0)
        {
            return [];
        }

        var allowedIds = await authService.FilterNodesAsync(
            callerToken,
            activeNodes.Select(n => n.NodeId).ToArray(),
            "read",
            ct);

        var allowedSet = allowedIds.ToHashSet(StringComparer.Ordinal);
        var queryEmbedding = embeddingService.ComputeTextEmbedding(query);

        return activeNodes
            .Where(n => allowedSet.Contains(n.NodeId))
            .Select(n => new RankedNodeResult(n, embeddingService.CosineSimilarity(queryEmbedding, n.Embedding)))
            .OrderByDescending(r => r.SimilarityScore)
            .ToArray();
    }

    [McpServerTool(Name = "get_node"), Description("Checks read permission on node_id and returns its AgentCard, status, capabilities, and workspace modes.")]
    public static async Task<NodeModel?> GetNodeAsync(
        IAuthService authService,
        INodeRegistryService registry,
        string node_id,
        CancellationToken ct = default)
    {
        await authService.EnsurePermissionAsync(node_id, "read", ct);
        return registry.GetNode(node_id);
    }

    [McpServerTool(Name = "get_extended_agent_card"), Description("Checks read permission and proxies A2A GetExtendedAgentCard to node_id.")]
    public static async Task<AgentCard> GetExtendedAgentCardAsync(
        IAuthService authService,
        INodeRegistryService registry,
        IA2AProxyService a2aProxy,
        string node_id,
        CancellationToken ct = default)
    {
        await authService.EnsurePermissionAsync(node_id, "read", ct);
        var node = registry.GetNode(node_id)
            ?? throw new KeyNotFoundException($"Node '{node_id}' not found in registry.");

        return await a2aProxy.GetExtendedAgentCardAsync(node_id, node.HostUrl, ct);
    }
}
