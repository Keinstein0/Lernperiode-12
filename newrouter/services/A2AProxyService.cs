using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using A2A;

namespace newrouter.services;

public sealed class A2AProxyService : IA2AProxyService
{
    private readonly HttpClient _http;
    private readonly IAuthService _authService;

    public A2AProxyService(HttpClient http, IAuthService authService)
    {
        _http = http;
        _authService = authService;
    }

    public async Task<AgentCard> GetExtendedAgentCardAsync(string nodeId, string hostUrl, CancellationToken ct = default)
    {
        var result = await SendJsonRpcAsync(nodeId, hostUrl, "agent/authenticatedExtendedCard", new { }, ct);
        return result.Deserialize<AgentCard>()!;
    }

    public Task<JsonElement> SendMessageAsync(string nodeId, string hostUrl, JsonElement payload, CancellationToken ct = default) =>
        SendJsonRpcAsync(nodeId, hostUrl, "message/send", payload, ct);

    public IAsyncEnumerable<string> SendStreamingMessageAsync(
        string nodeId,
        string hostUrl,
        JsonElement payload,
        CancellationToken ct = default) =>
        StreamJsonRpcAsync(nodeId, hostUrl, "message/stream", payload, ct);

    public Task<JsonElement> GetTaskAsync(string nodeId, string hostUrl, string taskId, int? historyLength, CancellationToken ct = default)
    {
        object @params = historyLength.HasValue
            ? new { id = taskId, historyLength = historyLength.Value }
            : new { id = taskId };
        return SendJsonRpcAsync(nodeId, hostUrl, "tasks/get", @params, ct);
    }

    public IAsyncEnumerable<string> SubscribeToTaskAsync(
        string nodeId,
        string hostUrl,
        string taskId,
        CancellationToken ct = default) =>
        StreamJsonRpcAsync(nodeId, hostUrl, "tasks/resubscribe", new { id = taskId }, ct);

    public Task<JsonElement> CancelTaskAsync(string nodeId, string hostUrl, string taskId, CancellationToken ct = default) =>
        SendJsonRpcAsync(nodeId, hostUrl, "tasks/cancel", new { id = taskId }, ct);

    private async Task<JsonElement> SendJsonRpcAsync(string nodeId, string hostUrl, string method, object @params, CancellationToken ct)
    {
        using var req = CreateJsonRpcRequest(nodeId, hostUrl, method, @params);
        var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
        return doc.TryGetProperty("result", out var result) ? result : doc;
    }

    private async IAsyncEnumerable<string> StreamJsonRpcAsync(
        string nodeId,
        string hostUrl,
        string method,
        object @params,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var req = CreateJsonRpcRequest(nodeId, hostUrl, method, @params);
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (!string.IsNullOrWhiteSpace(line))
            {
                yield return line;
            }
        }
    }

    private HttpRequestMessage CreateJsonRpcRequest(string nodeId, string hostUrl, string method, object @params)
    {
        var (token, _) = _authService.IssueRouterNodeToken(nodeId);
        var req = new HttpRequestMessage(HttpMethod.Post, hostUrl.TrimEnd('/'));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = JsonContent.Create(new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString("N"),
            method,
            @params
        });
        return req;
    }
}
