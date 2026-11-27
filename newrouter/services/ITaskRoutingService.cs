namespace newrouter.services;

public interface ITaskRoutingService
{
    void RecordTaskRoute(string taskId, string nodeId, string? contextId = null, string? callerNodeId = null);
    string? ResolveNodeId(string taskId);
    void RemoveRoutesForNode(string nodeId);
}
