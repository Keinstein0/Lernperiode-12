using System.Collections.Concurrent;
using newrouter.models;

namespace newrouter.services;

public sealed class TaskRoutingService : ITaskRoutingService
{
    private readonly ConcurrentDictionary<string, TaskRouteEntry> _routes = new(StringComparer.Ordinal);

    public void RecordTaskRoute(string taskId, string nodeId, string? contextId = null, string? callerNodeId = null)
    {
        _routes[taskId] = new TaskRouteEntry(
            taskId,
            nodeId,
            contextId,
            callerNodeId,
            DateTimeOffset.UtcNow);
    }

    public string? ResolveNodeId(string taskId) =>
        _routes.TryGetValue(taskId, out var entry) ? entry.NodeId : null;

    public void RemoveRoutesForNode(string nodeId)
    {
        foreach (var kvp in _routes)
        {
            if (string.Equals(kvp.Value.NodeId, nodeId, StringComparison.Ordinal))
            {
                _routes.TryRemove(kvp.Key, out _);
            }
        }
    }
}
