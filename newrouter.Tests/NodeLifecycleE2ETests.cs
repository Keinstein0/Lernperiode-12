using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using A2A;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using newrouter.models;
using newrouter.services;

namespace newrouter.Tests;

public class NodeLifecycleE2ETests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public NodeLifecycleE2ETests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegisterAndHeartbeat_HappyPath_AndSpoofedHeartbeatRejected()
    {
        var mockAuth = new Mock<IAuthService>();
        mockAuth.Setup(a => a.RegisterNodeAsync("b2-e2e", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegisterAuthNodeResponse("b2-e2e", "one-time-secret"));
        mockAuth.Setup(a => a.IssueRouterNodeToken("b2-e2e"))
            .Returns(("valid-router-token", DateTimeOffset.UtcNow.AddHours(1)));
        mockAuth.Setup(a => a.ValidateRouterNodeToken("valid-router-token", "b2-e2e"))
            .Returns(true);
        mockAuth.Setup(a => a.ValidateRouterNodeToken("valid-router-token", "b2-other"))
            .Returns(false);

        using var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAuthService>();
                services.AddSingleton(mockAuth.Object);
            });
        }).CreateClient();

        // 1. Register (with registration token)
        client.DefaultRequestHeaders.Add("X-Registration-Token", "dev-registration-token");
        var regReq = new RegisterNodeRequest(
            "b2-e2e",
            "http://b2-e2e:8080",
            ["git"],
            ["files"],
            new AgentCard { Name = "B2 E2E", Description = "E2E worker" });

        var regResp = await client.PostAsJsonAsync("/nodes/register", regReq);
        Assert.Equal(HttpStatusCode.OK, regResp.StatusCode);

        // Duplicate NodeId -> 409 Conflict
        var dupResp = await client.PostAsJsonAsync("/nodes/register", regReq);
        Assert.Equal(HttpStatusCode.Conflict, dupResp.StatusCode);

        var regBody = await regResp.Content.ReadFromJsonAsync<RegisterNodeResponse>();
        Assert.NotNull(regBody);
        Assert.Equal("b2-e2e", regBody.NodeId);
        Assert.Equal("valid-router-token", regBody.Token);

        // 2. Heartbeat with valid token
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", regBody.Token);
        var hbResp = await client.PostAsJsonAsync("/nodes/heartbeat", new HeartbeatRequest("b2-e2e", NodeStatus.Busy));
        Assert.Equal(HttpStatusCode.OK, hbResp.StatusCode);

        // 3. Spoofed heartbeat for another node_id -> 401 Unauthorized
        var spoofResp = await client.PostAsJsonAsync("/nodes/heartbeat", new HeartbeatRequest("b2-other", NodeStatus.Free));
        Assert.Equal(HttpStatusCode.Unauthorized, spoofResp.StatusCode);
    }

    [Fact]
    public async Task AuthNodeDown_HeartbeatStillSucceeds_ForRegisteredNode()
    {
        var mockAuth = new Mock<IAuthService>();
        mockAuth.Setup(a => a.RegisterNodeAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Auth node unreachable", null, HttpStatusCode.ServiceUnavailable));
        mockAuth.Setup(a => a.ValidateRouterNodeToken("valid-tok", "b2-existing"))
            .Returns(true);

        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAuthService>();
                services.AddSingleton(mockAuth.Object);
            });
        });
        using var client = customFactory.CreateClient();

        // Pre-seed registry with an already-registered node
        var registry = customFactory.Services.GetRequiredService<INodeRegistryService>();
        registry.UpsertNode(new NodeModel
        {
            NodeId = "b2-existing",
            HostUrl = "http://b2-existing:8080",
            AgentCard = new AgentCard { Name = "Existing", Description = "Already registered" }
        });

        // Registration fails when Auth Node is down
        client.DefaultRequestHeaders.Add("X-Registration-Token", "dev-registration-token");
        var regReq = new RegisterNodeRequest("b2-new", "http://b2-new:8080", null, null, new AgentCard { Name = "New", Description = "New" });
        var regResp = await client.PostAsJsonAsync("/nodes/register", regReq);
        Assert.Equal(HttpStatusCode.InternalServerError, regResp.StatusCode);

        // Heartbeat for existing node still succeeds
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "valid-tok");
        var hbResp = await client.PostAsJsonAsync("/nodes/heartbeat", new HeartbeatRequest("b2-existing", NodeStatus.Free));
        Assert.Equal(HttpStatusCode.OK, hbResp.StatusCode);
    }

    [Fact]
    public async Task Register_MissingOrInvalidRegistrationToken_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        var regReq = new RegisterNodeRequest("b2-unauth", "http://b2-unauth:8080", null, null, new AgentCard { Name = "Unauth", Description = "Unauth" });

        var missingResp = await client.PostAsJsonAsync("/nodes/register", regReq);
        Assert.Equal(HttpStatusCode.Unauthorized, missingResp.StatusCode);

        client.DefaultRequestHeaders.Add("X-Registration-Token", "wrong-token");
        var wrongResp = await client.PostAsJsonAsync("/nodes/register", regReq);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongResp.StatusCode);
    }
}
