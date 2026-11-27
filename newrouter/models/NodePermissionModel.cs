namespace newrouter.models;

public sealed record NodePermissionModel
{
    public required string NodeId { get; init; }
    public bool IsAdmin { get; init; }
    public IReadOnlyList<string> AllowedTargetNodes { get; init; } = [];
}

public sealed record AuthLoginRequest(string NodeId, string Secret);

public sealed record AuthLoginResponse(string Token, DateTimeOffset ExpiresAt);

public sealed record PermissionCheckRequest(
    string CallerToken,
    string TargetNodeId,
    string Action
);

public sealed record PermissionCheckResponse(bool Allowed, string? Reason = null);

public sealed record PermissionFilterRequest(
    string CallerToken,
    IReadOnlyList<string> NodeIds,
    string Action
);

public sealed record PermissionFilterResponse(IReadOnlyList<string> AllowedNodeIds);

public sealed record RegisterAuthNodeRequest(string? NodeId);

public sealed record RegisterAuthNodeResponse(string NodeId, string OneTimeSecret);

public sealed record UpdatePermissionsRequest(
    IReadOnlyList<string>? AddAllowedNodes,
    IReadOnlyList<string>? RemoveAllowedNodes
);
