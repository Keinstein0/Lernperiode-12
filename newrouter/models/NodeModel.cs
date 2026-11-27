using System.Text.Json.Serialization;
using A2A;

namespace newrouter.models;

public enum NodeStatus
{
    Free,
    Busy,
    Offline
}

public sealed record NodeModel
{
    public required string NodeId { get; init; }
    public required string HostUrl { get; init; }
    public IReadOnlyList<string> Capabilities { get; init; } = [];
    public IReadOnlyList<string> WorkspaceModes { get; init; } = [];
    public required AgentCard AgentCard { get; init; }
    public NodeStatus Status { get; init; } = NodeStatus.Free;
    public DateTimeOffset LastSeen { get; init; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public float[] Embedding { get; init; } = [];
}

public sealed record RankedNodeResult(
    NodeModel Node,
    double SimilarityScore
);
