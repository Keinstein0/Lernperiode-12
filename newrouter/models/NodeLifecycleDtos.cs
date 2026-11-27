using A2A;

namespace newrouter.models;

public sealed record RegisterNodeRequest(
    string? NodeId,
    string HostUrl,
    IReadOnlyList<string>? Capabilities,
    IReadOnlyList<string>? WorkspaceModes,
    AgentCard AgentCard
);

public sealed record RegisterNodeResponse(
    string NodeId,
    string Token,
    DateTimeOffset ExpiresAt
);

public sealed record HeartbeatRequest(
    string NodeId,
    NodeStatus? Status
);

public sealed record HeartbeatResponse(
    bool Ok,
    DateTimeOffset LastSeen
);
