using newrouter.models;

namespace newrouter.services;

public interface IAuthService
{
    string GetCallerTokenOrThrow();
    Task<AuthLoginResponse> LoginNodeAsync(string nodeId, string secret, CancellationToken ct = default);
    Task<bool> CheckPermissionAsync(string callerToken, string targetNodeId, string action, CancellationToken ct = default);
    Task EnsurePermissionAsync(string targetNodeId, string action, CancellationToken ct = default);
    Task<IReadOnlyList<string>> FilterNodesAsync(string callerToken, IReadOnlyList<string> nodeIds, string action, CancellationToken ct = default);
    Task<RegisterAuthNodeResponse> RegisterNodeAsync(string? nodeId, CancellationToken ct = default);
    Task<IReadOnlyList<NodePermissionModel>> ListNodesAsync(CancellationToken ct = default);
    Task<NodePermissionModel?> GetNodeAsync(string nodeId, CancellationToken ct = default);
    Task<NodePermissionModel?> UpdateNodePermissionsAsync(string nodeId, IReadOnlyList<string>? addAllowedNodes, IReadOnlyList<string>? removeAllowedNodes, CancellationToken ct = default);
    Task<NodePermissionModel?> PromoteNodeAsync(string nodeId, CancellationToken ct = default);
    Task<bool> DeleteNodeAsync(string nodeId, CancellationToken ct = default);
    (string Token, DateTimeOffset ExpiresAt) IssueRouterNodeToken(string nodeId);
    bool ValidateRouterNodeToken(string token, string expectedNodeId);
}
