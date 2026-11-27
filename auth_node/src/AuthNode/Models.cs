using System.Text.Json.Serialization;

namespace AuthNode;

// ── Requests ────────────────────────────────────────────────────────────────

/// <summary>POST /auth/node/login (JSON variant; form and HTTP Basic also work).</summary>
public sealed record LoginRequest(string? GrantType, string? ClientId, string? ClientSecret);

/// <summary>POST /auth/permissions/check</summary>
public sealed record CheckRequest(string? TargetNodeId, string? Action);

/// <summary>POST /auth/permissions/filter</summary>
public sealed record FilterRequest(List<string>? NodeIds, string? Action);

/// <summary>POST /auth/nodes (router-triggered registration)</summary>
public sealed record RegisterRequest(string? NodeId, string? Name, string? Host);

/// <summary>PATCH /auth/nodes/{node_id}</summary>
public sealed record UpdatePermissionsRequest(List<string>? AddAllowedNodes, List<string>? RemoveAllowedNodes);

// ── Responses ───────────────────────────────────────────────────────────────

/// <summary>OAuth 2.1 token response (RFC 6749 §5.1).</summary>
public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn, string NodeId);

public sealed record CheckResponse(bool Allowed, string CallerNodeId, string Reason);

public sealed record FilterResponse(List<string> AllowedNodeIds, string CallerNodeId);

/// <summary>Returned once, at registration. The secret is never shown again.</summary>
public sealed record RegisterResponse(string NodeId, string ClientId, string ClientSecret, bool IsAdmin, List<string> AllowedNodes);

public sealed record NodeView(string NodeId, string? Name, string? Host, bool IsAdmin, List<string> AllowedNodes, DateTimeOffset CreatedAt);

/// <summary>Error body; OAuth-style (RFC 6749 §5.2) on every endpoint.</summary>
public sealed record ErrorResponse(string Error, string? ErrorDescription = null);

// ── Source-generated JSON (no reflection) ───────────────────────────────────

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(CheckRequest))]
[JsonSerializable(typeof(FilterRequest))]
[JsonSerializable(typeof(RegisterRequest))]
[JsonSerializable(typeof(UpdatePermissionsRequest))]
[JsonSerializable(typeof(TokenResponse))]
[JsonSerializable(typeof(CheckResponse))]
[JsonSerializable(typeof(FilterResponse))]
[JsonSerializable(typeof(RegisterResponse))]
[JsonSerializable(typeof(NodeView))]
[JsonSerializable(typeof(List<NodeView>))]
[JsonSerializable(typeof(ErrorResponse))]
internal sealed partial class AuthJsonContext : JsonSerializerContext;
