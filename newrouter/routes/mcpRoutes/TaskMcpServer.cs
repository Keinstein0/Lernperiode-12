using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using newrouter.services;

namespace newrouter.routes.mcpRoutes;

[McpServerToolType]
public sealed class TaskMcpServer
{
    [McpServerTool(Name = "send_task"), Description("Checks invoke permission, proxies A2A SendMessage to node_id, and records taskId -> node_id.")]
    public static async Task<JsonElement> SendTaskAsync(
        IAuthService authService,
        INodeRegistryService registry,
        ITaskRoutingService taskRouting,
        IA2AProxyService a2aProxy,
        string node_id,
        JsonElement message,
        JsonElement? configuration = null,
        CancellationToken ct = default)
    {
        await authService.EnsurePermissionAsync(node_id, "invoke", ct);
        var node = registry.GetNode(node_id)
            ?? throw new KeyNotFoundException($"Node '{node_id}' not found in registry.");

        var payload = BuildSendPayload(message, configuration);
        var result = await a2aProxy.SendMessageAsync(node_id, node.HostUrl, payload, ct);

        if (TryExtractTaskId(result, out var taskId))
        {
            taskRouting.RecordTaskRoute(taskId!, node_id);
        }

        return result;
    }

    [McpServerTool(Name = "send_task_stream"), Description("Checks invoke permission, proxies A2A SendStreamingMessage to node_id, and streams SSE events.")]
    public static async Task<IReadOnlyList<string>> SendTaskStreamAsync(
        IAuthService authService,
        INodeRegistryService registry,
        ITaskRoutingService taskRouting,
        IA2AProxyService a2aProxy,
        string node_id,
        JsonElement message,
        JsonElement? configuration = null,
        CancellationToken ct = default)
    {
        await authService.EnsurePermissionAsync(node_id, "invoke", ct);
        var node = registry.GetNode(node_id)
            ?? throw new KeyNotFoundException($"Node '{node_id}' not found in registry.");

        var payload = BuildSendPayload(message, configuration);
        var events = new List<string>();
        await foreach (var line in a2aProxy.SendStreamingMessageAsync(node_id, node.HostUrl, payload, ct))
        {
            events.Add(line);
        }

        return events;
    }

    [McpServerTool(Name = "get_task"), Description("Resolves node_id for task_id, verifies read permission, and proxies A2A GetTask.")]
    public static async Task<JsonElement> GetTaskAsync(
        IAuthService authService,
        INodeRegistryService registry,
        ITaskRoutingService taskRouting,
        IA2AProxyService a2aProxy,
        string task_id,
        int? historyLength = null,
        CancellationToken ct = default)
    {
        var nodeId = taskRouting.ResolveNodeId(task_id)
            ?? throw new KeyNotFoundException($"No route recorded for task '{task_id}'.");
        await authService.EnsurePermissionAsync(nodeId, "read", ct);

        var node = registry.GetNode(nodeId)
            ?? throw new KeyNotFoundException($"Node '{nodeId}' not found in registry.");

        return await a2aProxy.GetTaskAsync(nodeId, node.HostUrl, task_id, historyLength, ct);
    }

    [McpServerTool(Name = "subscribe_task"), Description("Resolves node_id for task_id, verifies read permission, and proxies A2A SubscribeToTask.")]
    public static async Task<IReadOnlyList<string>> SubscribeTaskAsync(
        IAuthService authService,
        INodeRegistryService registry,
        ITaskRoutingService taskRouting,
        IA2AProxyService a2aProxy,
        string task_id,
        CancellationToken ct = default)
    {
        var nodeId = taskRouting.ResolveNodeId(task_id)
            ?? throw new KeyNotFoundException($"No route recorded for task '{task_id}'.");
        await authService.EnsurePermissionAsync(nodeId, "read", ct);

        var node = registry.GetNode(nodeId)
            ?? throw new KeyNotFoundException($"Node '{nodeId}' not found in registry.");

        var events = new List<string>();
        await foreach (var line in a2aProxy.SubscribeToTaskAsync(nodeId, node.HostUrl, task_id, ct))
        {
            events.Add(line);
        }

        return events;
    }

    [McpServerTool(Name = "cancel_task"), Description("Resolves node_id for task_id, verifies invoke permission, and proxies A2A CancelTask.")]
    public static async Task<JsonElement> CancelTaskAsync(
        IAuthService authService,
        INodeRegistryService registry,
        ITaskRoutingService taskRouting,
        IA2AProxyService a2aProxy,
        string task_id,
        CancellationToken ct = default)
    {
        var nodeId = taskRouting.ResolveNodeId(task_id)
            ?? throw new KeyNotFoundException($"No route recorded for task '{task_id}'.");
        await authService.EnsurePermissionAsync(nodeId, "invoke", ct);

        var node = registry.GetNode(nodeId)
            ?? throw new KeyNotFoundException($"Node '{nodeId}' not found in registry.");

        return await a2aProxy.CancelTaskAsync(nodeId, node.HostUrl, task_id, ct);
    }

    private static JsonElement BuildSendPayload(JsonElement message, JsonElement? configuration)
    {
        var dict = new Dictionary<string, JsonElement> { ["message"] = message };
        if (configuration.HasValue && configuration.Value.ValueKind != JsonValueKind.Undefined)
        {
            dict["configuration"] = configuration.Value;
        }

        return JsonSerializer.SerializeToElement(dict);
    }

    private static bool TryExtractTaskId(JsonElement result, out string? taskId)
    {
        taskId = null;
        if (result.ValueKind == JsonValueKind.Object &&
            result.TryGetProperty("id", out var idProp) &&
            idProp.ValueKind == JsonValueKind.String)
        {
            taskId = idProp.GetString();
            return !string.IsNullOrWhiteSpace(taskId);
        }

        return false;
    }
}
