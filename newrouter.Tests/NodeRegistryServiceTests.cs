using A2A;
using Microsoft.Extensions.Configuration;
using newrouter.models;
using newrouter.services;

namespace newrouter.Tests;

public class NodeRegistryServiceTests
{
    private static NodeRegistryService CreateRegistry(int timeoutSeconds = 90)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Registry:HeartbeatTimeoutSeconds"] = timeoutSeconds.ToString()
            })
            .Build();
        return new NodeRegistryService(config);
    }

    private static NodeModel MakeNode(string id, NodeStatus status = NodeStatus.Free, DateTimeOffset? lastSeen = null) =>
        new()
        {
            NodeId = id,
            HostUrl = $"http://{id}:8080",
            AgentCard = new AgentCard { Name = id, Description = "test" },
            Status = status,
            LastSeen = lastSeen ?? DateTimeOffset.UtcNow
        };

    [Fact]
    public void UpsertAndGet_StoresAndRetrievesNode()
    {
        var reg = CreateRegistry();
        var node = MakeNode("b2-1");
        reg.UpsertNode(node);

        var fetched = reg.GetNode("b2-1");
        Assert.NotNull(fetched);
        Assert.Equal("b2-1", fetched.NodeId);
    }

    [Fact]
    public void UpdateHeartbeat_UnknownNode_ReturnsNull()
    {
        var reg = CreateRegistry();
        Assert.Null(reg.UpdateHeartbeat("missing", NodeStatus.Free));
    }

    [Fact]
    public void UpdateHeartbeat_UpdatesStatusAndLastSeen()
    {
        var reg = CreateRegistry();
        var oldTime = DateTimeOffset.UtcNow.AddMinutes(-1);
        reg.UpsertNode(MakeNode("b2-1", NodeStatus.Free, oldTime));

        var updated = reg.UpdateHeartbeat("b2-1", NodeStatus.Busy);
        Assert.NotNull(updated);
        Assert.Equal(NodeStatus.Busy, updated.Status);
        Assert.True(updated.LastSeen > oldTime);
    }

    [Fact]
    public void GetActiveNodes_ExcludesOfflineAndExpiredNodes()
    {
        var reg = CreateRegistry(timeoutSeconds: 30);
        reg.UpsertNode(MakeNode("active", NodeStatus.Free, DateTimeOffset.UtcNow));
        reg.UpsertNode(MakeNode("offline", NodeStatus.Offline, DateTimeOffset.UtcNow));
        reg.UpsertNode(MakeNode("expired", NodeStatus.Free, DateTimeOffset.UtcNow.AddSeconds(-60)));

        var active = reg.GetActiveNodes();
        Assert.Single(active);
        Assert.Equal("active", active[0].NodeId);
    }

    [Fact]
    public void EvictNode_RemovesNode()
    {
        var reg = CreateRegistry();
        reg.UpsertNode(MakeNode("b2-1"));

        Assert.True(reg.EvictNode("b2-1"));
        Assert.Null(reg.GetNode("b2-1"));
        Assert.False(reg.EvictNode("b2-1"));
    }
}
