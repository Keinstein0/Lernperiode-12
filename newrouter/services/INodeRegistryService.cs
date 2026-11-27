using newrouter.models;

namespace newrouter.services;

public interface INodeRegistryService
{
    NodeModel UpsertNode(NodeModel node);
    NodeModel? UpdateHeartbeat(string nodeId, NodeStatus? status);
    NodeModel? GetNode(string nodeId);
    IReadOnlyList<NodeModel> GetActiveNodes();
    bool EvictNode(string nodeId);
}
