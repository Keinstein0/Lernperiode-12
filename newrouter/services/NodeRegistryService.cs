using System.Collections.Concurrent;
using newrouter.models;

namespace newrouter.services;

public sealed class NodeRegistryService : INodeRegistryService
{
    private readonly ConcurrentDictionary<string, NodeModel> _nodes = new(StringComparer.Ordinal);
    private readonly TimeSpan _heartbeatTimeout;

    public NodeRegistryService(IConfiguration configuration)
    {
        var timeoutSeconds = configuration.GetValue<int?>("Registry:HeartbeatTimeoutSeconds") ?? 90;
        _heartbeatTimeout = TimeSpan.FromSeconds(timeoutSeconds);
    }

    public NodeModel UpsertNode(NodeModel node)
    {
        _nodes[node.NodeId] = node;
        return node;
    }

    public NodeModel? UpdateHeartbeat(string nodeId, NodeStatus? status)
    {
        if (!_nodes.TryGetValue(nodeId, out var existing))
        {
            return null;
        }

        var updated = existing with
        {
            Status = status ?? existing.Status,
            LastSeen = DateTimeOffset.UtcNow
        };
        _nodes[nodeId] = updated;
        return updated;
    }

    public NodeModel? GetNode(string nodeId) =>
        _nodes.TryGetValue(nodeId, out var node) ? node : null;

    public IReadOnlyList<NodeModel> GetActiveNodes()
    {
        var cutoff = DateTimeOffset.UtcNow - _heartbeatTimeout;
        return _nodes.Values
            .Where(n => n.LastSeen >= cutoff && n.Status != NodeStatus.Offline)
            .ToArray();
    }

    public bool EvictNode(string nodeId) => _nodes.TryRemove(nodeId, out _);
}
