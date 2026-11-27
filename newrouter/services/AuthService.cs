using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using newrouter.models;

namespace newrouter.services;

public sealed class AuthService : IAuthService
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;

    public AuthService(
        HttpClient http,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration)
    {
        _http = http;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;

        var baseUrl = _configuration["AuthNode:BaseUrl"] ?? "http://localhost:5100";
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");

        var serviceToken = _configuration["AuthNode:ServiceToken"];
        if (!string.IsNullOrWhiteSpace(serviceToken))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken);
        }
    }

    public string GetCallerTokenOrThrow()
    {
        var authHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Missing or invalid caller Bearer token.");
        }

        return authHeader["Bearer ".Length..].Trim();
    }

    public async Task<AuthLoginResponse> LoginNodeAsync(string nodeId, string secret, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("auth/node/login", new AuthLoginRequest(nodeId, secret), ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AuthLoginResponse>(ct))!;
    }

    public async Task<bool> CheckPermissionAsync(string callerToken, string targetNodeId, string action, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync(
            "auth/permissions/check",
            new PermissionCheckRequest(callerToken, targetNodeId, action),
            ct);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<PermissionCheckResponse>(ct);
        return result?.Allowed ?? false;
    }

    public async Task EnsurePermissionAsync(string targetNodeId, string action, CancellationToken ct = default)
    {
        var callerToken = GetCallerTokenOrThrow();
        var allowed = await CheckPermissionAsync(callerToken, targetNodeId, action, ct);
        if (!allowed)
        {
            throw new UnauthorizedAccessException($"Caller is not permitted to perform '{action}' on node '{targetNodeId}'.");
        }
    }

    public async Task<IReadOnlyList<string>> FilterNodesAsync(string callerToken, IReadOnlyList<string> nodeIds, string action, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync(
            "auth/permissions/filter",
            new PermissionFilterRequest(callerToken, nodeIds, action),
            ct);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<PermissionFilterResponse>(ct);
        return result?.AllowedNodeIds ?? [];
    }

    public async Task<RegisterAuthNodeResponse> RegisterNodeAsync(string? nodeId, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("auth/nodes", new RegisterAuthNodeRequest(nodeId), ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<RegisterAuthNodeResponse>(ct))!;
    }

    public async Task<IReadOnlyList<NodePermissionModel>> ListNodesAsync(CancellationToken ct = default)
    {
        using var req = CreateCallerRequest(HttpMethod.Get, "auth/nodes");
        var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<IReadOnlyList<NodePermissionModel>>(ct) ?? [];
    }

    public async Task<NodePermissionModel?> GetNodeAsync(string nodeId, CancellationToken ct = default)
    {
        using var req = CreateCallerRequest(HttpMethod.Get, $"auth/nodes/{Uri.EscapeDataString(nodeId)}");
        var resp = await _http.SendAsync(req, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<NodePermissionModel>(ct);
    }

    public async Task<NodePermissionModel?> UpdateNodePermissionsAsync(
        string nodeId,
        IReadOnlyList<string>? addAllowedNodes,
        IReadOnlyList<string>? removeAllowedNodes,
        CancellationToken ct = default)
    {
        using var req = CreateCallerRequest(HttpMethod.Patch, $"auth/nodes/{Uri.EscapeDataString(nodeId)}");
        req.Content = JsonContent.Create(new UpdatePermissionsRequest(addAllowedNodes, removeAllowedNodes));
        var resp = await _http.SendAsync(req, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<NodePermissionModel>(ct);
    }

    public async Task<NodePermissionModel?> PromoteNodeAsync(string nodeId, CancellationToken ct = default)
    {
        using var req = CreateCallerRequest(HttpMethod.Post, $"auth/nodes/{Uri.EscapeDataString(nodeId)}/promote");
        var resp = await _http.SendAsync(req, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<NodePermissionModel>(ct);
    }

    public async Task<bool> DeleteNodeAsync(string nodeId, CancellationToken ct = default)
    {
        using var req = CreateCallerRequest(HttpMethod.Delete, $"auth/nodes/{Uri.EscapeDataString(nodeId)}");
        var resp = await _http.SendAsync(req, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        resp.EnsureSuccessStatusCode();
        return true;
    }

    public (string Token, DateTimeOffset ExpiresAt) IssueRouterNodeToken(string nodeId)
    {
        var lifetimeMinutes = _configuration.GetValue<int?>("RouterAuth:TokenLifetimeMinutes") ?? 60;
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(lifetimeMinutes);
        var signingKey = _configuration["RouterAuth:SigningKey"] ?? "dev-router-signing-key-change-in-production-32chars!";
        var payload = $"{nodeId}:{expiresAt.ToUnixTimeSeconds()}";
        var signature = ComputeHmac(payload, signingKey);
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{payload}:{signature}"));
        return (token, expiresAt);
    }

    public bool ValidateRouterNodeToken(string token, string expectedNodeId)
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts = decoded.Split(':', 3);
            if (parts.Length != 3) return false;

            if (!string.Equals(parts[0], expectedNodeId, StringComparison.Ordinal)) return false;
            if (!long.TryParse(parts[1], out var expUnix)) return false;
            if (DateTimeOffset.FromUnixTimeSeconds(expUnix) < DateTimeOffset.UtcNow) return false;

            var signingKey = _configuration["RouterAuth:SigningKey"] ?? "dev-router-signing-key-change-in-production-32chars!";
            var expectedSig = ComputeHmac($"{parts[0]}:{parts[1]}", signingKey);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(parts[2]),
                Encoding.UTF8.GetBytes(expectedSig));
        }
        catch
        {
            return false;
        }
    }

    private HttpRequestMessage CreateCallerRequest(HttpMethod method, string relativeUrl)
    {
        var callerToken = GetCallerTokenOrThrow();
        var req = new HttpRequestMessage(method, relativeUrl);
        req.Headers.Add("X-Caller-Token", callerToken);
        return req;
    }

    private static string ComputeHmac(string payload, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }
}
