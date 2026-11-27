using System.Collections.Concurrent;
using System.ComponentModel;
using ModelContextProtocol.Server;
using newrouter;


[McpServerToolType]
public sealed class DiscoveryMcpServer
{
    [McpServerTool, Description("[TODO]")]
    public static IReadOnlyCollection<NodeModel> GetAllNodes(ItemService service)
    {
        throw new NotImplementedException();
    }

    [McpServerTool, Description("[TODO]")]
    public static NodeModel? GetSingleNode(ItemService service, Guid id)
    {
        throw new NotImplementedException();
    }
}