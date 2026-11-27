using System.Collections.Concurrent;
using System.ComponentModel;
using ModelContextProtocol.Server;
using newrouter;
using newrouter.services;


[McpServerToolType]
public sealed class AdminMcpServer
{
    [McpServerTool, Description("[TODO]")]
    public static IReadOnlyCollection<NodeModel> ListNodes(AuthService authService)
    {
        throw new NotImplementedException();
    }

    [McpServerTool, Description("[TODO]")]
    public static NodeModel? GetNode(AuthService authService, Guid id)
    {
        throw new NotImplementedException();
    }

    [McpServerTool, Description("[TODO]")]
    public static NodeModel? UpdateNodePermissions(AuthService authService, Guid id)
    {
        throw new NotImplementedException();
    }

    [McpServerTool, Description("[TODO]")]
    public static NodeModel? PromoteNode(AuthService authService, Guid id)
    {
        throw new NotImplementedException();
    }

    [McpServerTool, Description("[TODO]")]
    public static NodeModel? DeleteNode(AuthService authService, Guid id)
    {
        throw new NotImplementedException();
    }
}