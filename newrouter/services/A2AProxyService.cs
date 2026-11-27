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
        using var req = CreateAuthorizedRequest(HttpMethod.Get, $"{hostUrl.TrimEnd('/')}/agent-card/extended", nodeId);
        var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AgentCard>(ct))!;
    }

    public async Task<JsonElement> SendMessageAsync(string nodeId, string hostUrl, JsonElement payload, CancellationToken ct = default)
    {
        using var req = CreateAuthorizedRequest(HttpMethod.Post, $"{hostUrl.TrimEnd('/')}/a2a/message:send", nodeId);
        req.Content = JsonContent.Create(payload);
        var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
    }

    public async IAsyncEnumerable<string> SendStreamingMessageAsync(
        string nodeId,
        string hostUrl,
        JsonElement payload,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var req = CreateAuthorizedRequest(HttpMethod.Post, $"{hostUrl.TrimEnd('/')}/a2a/message:stream", nodeId);
        req.Content = JsonContent.Create(payload);
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

    public async Task<JsonElement> GetTaskAsync(string nodeId, string hostUrl, string taskId, int? historyLength, CancellationToken ct = default)
    {
        var url = $"{hostUrl.TrimEnd('/')}/a2a/tasks/{Uri.EscapeDataString(taskId)}";
        if (historyLength.HasValue)
        {
            url += $"?historyLength={historyLength.Value}";
        }

        using var req = CreateAuthorizedRequest(HttpMethod.Get, url, nodeId);
        var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
    }

    public async IAsyncEnumerable<string> SubscribeToTaskAsync(
        string nodeId,
        string hostUrl,
        string taskId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var url = $"{hostUrl.TrimEnd('/')}/a2a/tasks/{Uri.EscapeDataString(taskId)}:subscribe";
        using var req = CreateAuthorizedRequest(HttpMethod.Get, url, nodeId);
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

    public async Task<JsonElement> CancelTaskAsync(string nodeId, string hostUrl, string taskId, CancellationToken ct = default)
    {
        var url = $"{hostUrl.TrimEnd('/')}/a2a/tasks/{Uri.EscapeDataString(taskId)}:cancel";
        using var req = CreateAuthorizedRequest(HttpMethod.Post, url, nodeId);
        var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
    }

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string url, string nodeId)
    {
        var (token, _) = _authService.IssueRouterNodeToken(nodeId);
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }
}
