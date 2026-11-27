using System.Text.Json;
using A2A;

namespace newrouter.services;

public interface IA2AProxyService
{
    Task<AgentCard> GetExtendedAgentCardAsync(string nodeId, string hostUrl, CancellationToken ct = default);
    Task<JsonElement> SendMessageAsync(string nodeId, string hostUrl, JsonElement payload, CancellationToken ct = default);
    IAsyncEnumerable<string> SendStreamingMessageAsync(string nodeId, string hostUrl, JsonElement payload, CancellationToken ct = default);
    Task<JsonElement> GetTaskAsync(string nodeId, string hostUrl, string taskId, int? historyLength, CancellationToken ct = default);
    IAsyncEnumerable<string> SubscribeToTaskAsync(string nodeId, string hostUrl, string taskId, CancellationToken ct = default);
    Task<JsonElement> CancelTaskAsync(string nodeId, string hostUrl, string taskId, CancellationToken ct = default);
}
