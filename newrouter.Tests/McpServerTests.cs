using A2A;
using Moq;
using newrouter.models;
using newrouter.routes.mcpRoutes;
using newrouter.services;

namespace newrouter.Tests;

public class McpServerTests
{
    private static NodeModel MakeNode(string id, params string[] capabilities) =>
        new()
        {
            NodeId = id,
            HostUrl = $"http://{id}:8080",
            Capabilities = capabilities,
            AgentCard = new AgentCard { Name = id, Description = $"{id} agent" },
            Embedding = [1f, 0f]
        };

    [Fact]
    public async Task Discovery_GetNodes_FiltersByAuthAndCapability_AndRanks()
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.GetCallerTokenOrThrow()).Returns("caller-tok");
        auth.Setup(a => a.FilterNodesAsync("caller-tok", It.IsAny<IReadOnlyList<string>>(), "read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["b2-1"]);

        var registry = new Mock<INodeRegistryService>();
        registry.Setup(r => r.GetActiveNodes()).Returns([
            MakeNode("b2-1", "git"),
            MakeNode("b2-2", "git"),
            MakeNode("b2-3", "files")
        ]);

        var embed = new Mock<IEmbeddingService>();
        embed.Setup(e => e.ComputeTextEmbedding("find git")).Returns([1f, 0f]);
        embed.Setup(e => e.CosineSimilarity(It.IsAny<float[]>(), It.IsAny<float[]>())).Returns(0.9);

        var results = await DiscoveryMcpServer.GetNodesAsync(
            auth.Object, registry.Object, embed.Object, "find git", capability_filter: "git");

        Assert.Single(results);
        Assert.Equal("b2-1", results[0].Node.NodeId);
    }

    [Fact]
    public async Task Discovery_GetNode_Denied_ThrowsUnauthorized()
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.EnsurePermissionAsync("b2-1", "read", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("denied"));

        var registry = new Mock<INodeRegistryService>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            DiscoveryMcpServer.GetNodeAsync(auth.Object, registry.Object, "b2-1"));
    }

    [Fact]
    public async Task Task_SendTask_RecordsRoute_AndGetTaskResolvesIt()
    {
        var auth = new Mock<IAuthService>();
        var registry = new Mock<INodeRegistryService>();
        registry.Setup(r => r.GetNode("b2-1")).Returns(MakeNode("b2-1"));

        var routing = new TaskRoutingService();
        var proxy = new Mock<IA2AProxyService>();
        var sendResponse = System.Text.Json.JsonSerializer.SerializeToElement(new { id = "task-99", status = "SUBMITTED" });
        proxy.Setup(p => p.SendMessageAsync("b2-1", "http://b2-1:8080", It.IsAny<System.Text.Json.JsonElement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sendResponse);

        var msg = System.Text.Json.JsonSerializer.SerializeToElement(new { role = "user" });
        var result = await TaskMcpServer.SendTaskAsync(
            auth.Object, registry.Object, routing, proxy.Object, "b2-1", msg);

        Assert.Equal("task-99", result.GetProperty("id").GetString());
        Assert.Equal("b2-1", routing.ResolveNodeId("task-99"));
    }

    [Fact]
    public async Task Task_GetTask_UnknownTaskId_ThrowsKeyNotFound()
    {
        var auth = new Mock<IAuthService>();
        var registry = new Mock<INodeRegistryService>();
        var routing = new TaskRoutingService();
        var proxy = new Mock<IA2AProxyService>();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            TaskMcpServer.GetTaskAsync(auth.Object, registry.Object, routing, proxy.Object, "missing-task"));
    }

    [Fact]
    public async Task Admin_PromoteAndDelete_RequireConfirmTrue()
    {
        var auth = new Mock<IAuthService>();
        var registry = new Mock<INodeRegistryService>();
        var routing = new Mock<ITaskRoutingService>();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            AdminMcpServer.PromoteNodeAsync(auth.Object, "b2-1", confirm: false));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            AdminMcpServer.DeleteNodeAsync(auth.Object, registry.Object, routing.Object, "b2-1", confirm: false));

        auth.Verify(a => a.PromoteNodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        auth.Verify(a => a.DeleteNodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Admin_DeleteNode_Confirmed_EvictsNodeAndRoutes()
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.DeleteNodeAsync("b2-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var registry = new Mock<INodeRegistryService>();
        var routing = new Mock<ITaskRoutingService>();

        var deleted = await AdminMcpServer.DeleteNodeAsync(
            auth.Object, registry.Object, routing.Object, "b2-1", confirm: true);

        Assert.True(deleted);
        registry.Verify(r => r.EvictNode("b2-1"), Times.Once);
        routing.Verify(t => t.RemoveRoutesForNode("b2-1"), Times.Once);
    }
}
