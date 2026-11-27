using Microsoft.AspNetCore.Mvc;
using newrouter.models;
using newrouter.services;

namespace newrouter.routes.httpNodeLifecycle;

public static class NodeLifecycleRoutes
{
    public static IEndpointRouteBuilder MapNodeLifecycleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/nodes");

        group.MapPost("/register", RegisterAsync);
        group.MapPost("/heartbeat", HeartbeatAsync);

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        [FromBody] RegisterNodeRequest request,
        AuthService authService,
        NodeRegistryService registry,
        EmbeddingService embeddingService,
        CancellationToken ct)
    {
        var authNode = await authService.RegisterNodeAsync(request.NodeId, ct);
        var (routerToken, expiresAt) = authService.IssueRouterNodeToken(authNode.NodeId);
        var embedding = embeddingService.ComputeAgentCardEmbedding(request.AgentCard);

        var node = new NodeModel
        {
            NodeId = authNode.NodeId,
            HostUrl = request.HostUrl,
            Capabilities = request.Capabilities ?? [],
            WorkspaceModes = request.WorkspaceModes ?? [],
            AgentCard = request.AgentCard,
            Status = NodeStatus.Free,
            LastSeen = DateTimeOffset.UtcNow,
            Embedding = embedding
        };

        registry.UpsertNode(node);

        return TypedResults.Ok(new RegisterNodeResponse(authNode.NodeId, routerToken, expiresAt));
    }

    private static IResult HeartbeatAsync(
        HttpContext httpContext,
        [FromBody] HeartbeatRequest request,
        AuthService authService,
        NodeRegistryService registry)
    {
        var authHeader = httpContext.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.Unauthorized();
        }

        var token = authHeader["Bearer ".Length..].Trim();
        if (!authService.ValidateRouterNodeToken(token, request.NodeId))
        {
            return TypedResults.Unauthorized();
        }

        var updated = registry.UpdateHeartbeat(request.NodeId, request.Status);
        if (updated is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new HeartbeatResponse(true, updated.LastSeen));
    }
}
