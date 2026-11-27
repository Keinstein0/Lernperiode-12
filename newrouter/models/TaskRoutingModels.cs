namespace newrouter.models;

public sealed record TaskRouteEntry(
    string TaskId,
    string NodeId,
    string? ContextId,
    string? CallerNodeId,
    DateTimeOffset CreatedAt
);
