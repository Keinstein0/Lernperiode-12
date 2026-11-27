using System.Collections.Concurrent;
using System.ComponentModel;
using ModelContextProtocol.Server;
using newrouter;


[McpServerToolType]
public sealed class TaskMcpServer
{
    [McpServerTool, Description("[TODO]")]
    public static IReadOnlyCollection<NodeModel> SendTask(ItemService service)
    {
        throw new NotImplementedException();
    }

    [McpServerTool, Description("[TODO]")]
    public static IReadOnlyCollection<NodeModel> SendTaskStream(ItemService service)
    {
        throw new NotImplementedException();
    }

    [McpServerTool, Description("[TODO]")]
    public static NodeModel? GetTask(ItemService service, Guid id)
    {
        throw new NotImplementedException();
    }

    [McpServerTool, Description("[TODO]")]
    public static NodeModel? SubscribeToTask(ItemService service, Guid id)
    {
        throw new NotImplementedException();
    }

    [McpServerTool, Description("[TODO]")]
    public static NodeModel? CancelTask(ItemService service, Guid id)
    {
        throw new NotImplementedException();
    }
}