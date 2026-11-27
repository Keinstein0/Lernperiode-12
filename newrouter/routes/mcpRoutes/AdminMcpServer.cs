using System.ComponentModel;
using ModelContextProtocol.Server;
using newrouter.models;
using newrouter.services;

namespace newrouter.routes.mcpRoutes;

[McpServerToolType]
public sealed class AdminMcpServer
{
    [McpServerTool(Name = "admin_list_nodes"), Description("Requires admin caller; lists all node identities and permission grants from Auth Node.")]
    public static Task<IReadOnlyList<NodePermissionModel>> ListNodesAsync(
        AuthService authService,
        CancellationToken ct = default) =>
        authService.ListNodesAsync(ct);

    [McpServerTool(Name = "admin_get_node"), Description("Requires admin caller; inspects one node's identity and permissions.")]
    public static Task<NodePermissionModel?> GetNodeAsync(
        AuthService authService,
        string target_node_id,
        CancellationToken ct = default) =>
        authService.GetNodeAsync(target_node_id, ct);

    [McpServerTool(Name = "admin_update_node_permissions"), Description("Requires admin caller; adds or removes permitted target nodes for target_node_id.")]
    public static Task<NodePermissionModel?> UpdateNodePermissionsAsync(
        AuthService authService,
        string target_node_id,
        IReadOnlyList<string>? add_allowed_nodes = null,
        IReadOnlyList<string>? remove_allowed_nodes = null,
        CancellationToken ct = default) =>
        authService.UpdateNodePermissionsAsync(target_node_id, add_allowed_nodes, remove_allowed_nodes, ct);

    [McpServerTool(Name = "admin_promote_node"), Description("Requires admin caller and confirm=true; grants admin privileges to target_node_id.")]
    public static Task<NodePermissionModel?> PromoteNodeAsync(
        AuthService authService,
        string target_node_id,
        bool confirm = false,
        CancellationToken ct = default)
    {
        if (!confirm)
        {
            throw new ArgumentException("Promotion requires confirm=true.", nameof(confirm));
        }

        return authService.PromoteNodeAsync(target_node_id, ct);
    }

    [McpServerTool(Name = "admin_delete_node"), Description("Requires admin caller and confirm=true; revokes node identity and evicts it from active routing.")]
    public static async Task<bool> DeleteNodeAsync(
        AuthService authService,
        NodeRegistryService registry,
        TaskRoutingService taskRouting,
        string target_node_id,
        bool confirm = false,
        CancellationToken ct = default)
    {
        if (!confirm)
        {
            throw new ArgumentException("Deletion requires confirm=true.", nameof(confirm));
        }

        var deleted = await authService.DeleteNodeAsync(target_node_id, ct);
        registry.EvictNode(target_node_id);
        taskRouting.RemoveRoutesForNode(target_node_id);
        return deleted;
    }
}
