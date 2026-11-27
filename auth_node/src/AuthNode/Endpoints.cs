using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace AuthNode;

internal static class Endpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/healthz", () => TypedResults.Text("ok"));

        // Everything under /auth is only for the router: it must send X-Router-Key.
        var auth = app.MapGroup("/auth").AddEndpointFilter(RequireRouterKey);

        auth.MapPost("/node/login", Login);
        auth.MapPost("/permissions/check", Check);
        auth.MapPost("/permissions/filter", Filter);
        auth.MapPost("/nodes", Register);
        auth.MapGet("/nodes", ListNodes);
        auth.MapGet("/nodes/{node_id}", GetNode);
        auth.MapPatch("/nodes/{node_id}", UpdatePermissions);
        auth.MapPost("/nodes/{node_id}/promote", Promote);
        auth.MapDelete("/nodes/{node_id}", DeleteNode);
    }

    // ── Node Auth ───────────────────────────────────────────────────────────

    /// <summary>
    /// OAuth 2.1 client credentials. Accepts the credentials as HTTP Basic
    /// (client_secret_basic), as a form (client_secret_post) or as JSON.
    /// </summary>
    private static async Task<IResult> Login(HttpRequest req, Store store, AuthOptions opt)
    {
        string? grantType = null, clientId = null, clientSecret = null;
        var viaBasic = false;

        if (AuthenticationHeaderValue.TryParse(req.Headers.Authorization, out var h)
            && "Basic".Equals(h.Scheme, StringComparison.OrdinalIgnoreCase) && h.Parameter is not null)
        {
            viaBasic = true;
            string decoded;
            try { decoded = Encoding.UTF8.GetString(Convert.FromBase64String(h.Parameter)); }
            catch (FormatException) { return Error(400, "invalid_request", "Malformed Basic credentials"); }
            var colon = decoded.IndexOf(':');
            if (colon < 0) return Error(400, "invalid_request", "Malformed Basic credentials");
            // RFC 6749 §2.3.1: both parts are form-urlencoded inside Basic
            clientId = Uri.UnescapeDataString(decoded[..colon].Replace('+', ' '));
            clientSecret = Uri.UnescapeDataString(decoded[(colon + 1)..].Replace('+', ' '));
        }

        if (req.HasFormContentType)
        {
            var form = await req.ReadFormAsync();
            grantType = form["grant_type"].ToString();
            clientId ??= NullIfEmpty(form["client_id"].ToString());
            clientSecret ??= NullIfEmpty(form["client_secret"].ToString());
        }
        else if (req.HasJsonContentType())
        {
            var (body, error) = await ReadJson(req, AuthJsonContext.Default.LoginRequest);
            if (error is not null) return error;
            grantType = body!.GrantType;
            clientId ??= body.ClientId;
            clientSecret ??= body.ClientSecret;
        }

        // RFC 6749 §4.4.2: grant_type is REQUIRED
        if (string.IsNullOrEmpty(grantType))
            return Error(400, "invalid_request", "grant_type is required (use \"client_credentials\")");
        if (grantType != "client_credentials")
            return Error(400, "unsupported_grant_type", "Only client_credentials is supported");
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            return Error(400, "invalid_request", "client_id and client_secret are required");

        var stored = store.GetSecretHash(clientId);
        if (stored is null || !Security.Matches(clientSecret, stored))
        {
            // RFC 6749 §5.2: a client that authenticated via the Authorization header MUST get a challenge back
            if (viaBasic) req.HttpContext.Response.Headers.WWWAuthenticate = "Basic realm=\"auth-node\"";
            return Error(401, "invalid_client", "Unknown client or wrong secret");
        }

        var token = Security.NewAccessToken();
        store.SaveToken(Security.Hash(token), clientId, DateTimeOffset.UtcNow + opt.TokenLifetime);
        req.HttpContext.Response.Headers.CacheControl = "no-store";   // RFC 6749 §5.1
        return TypedResults.Json(
            new TokenResponse(token, "Bearer", (int)opt.TokenLifetime.TotalSeconds, clientId),
            AuthJsonContext.Default.TokenResponse);
    }

    // ── Permissions ─────────────────────────────────────────────────────────

    private static async Task<IResult> Check(HttpRequest req, Store store)
    {
        var caller = ResolveCaller(req, store);
        if (caller is null) return InvalidToken();
        var (body, error) = await ReadJson(req, AuthJsonContext.Default.CheckRequest);
        if (error is not null) return error;
        if (string.IsNullOrEmpty(body!.TargetNodeId)) return Error(400, "invalid_request", "target_node_id is required");
        if (!IsAction(body.Action)) return Error(400, "invalid_request", "action must be \"read\" or \"invoke\"");

        var (allowed, reason) = Decide(caller.Value, store.GetGrants(caller.Value.NodeId),
            store.ExistingNodeIds([body.TargetNodeId]).Count == 1, body.TargetNodeId, body.Action!);
        return TypedResults.Json(new CheckResponse(allowed, caller.Value.NodeId, reason),
            AuthJsonContext.Default.CheckResponse);
    }

    private static async Task<IResult> Filter(HttpRequest req, Store store)
    {
        var caller = ResolveCaller(req, store);
        if (caller is null) return InvalidToken();
        var (body, error) = await ReadJson(req, AuthJsonContext.Default.FilterRequest);
        if (error is not null) return error;
        if (body!.NodeIds is null) return Error(400, "invalid_request", "node_ids is required");
        if (ContainsNull(body.NodeIds)) return Error(400, "invalid_request", "node_ids must not contain null");
        if (body.NodeIds.Count > 1000) return Error(400, "invalid_request", "At most 1000 node_ids per call");
        if (!IsAction(body.Action)) return Error(400, "invalid_request", "action must be \"read\" or \"invoke\"");

        var grants = store.GetGrants(caller.Value.NodeId);
        var existing = store.ExistingNodeIds(body.NodeIds);
        var allowed = body.NodeIds.Distinct()
            .Where(id => Decide(caller.Value, grants, existing.Contains(id), id, body.Action!).Allowed)
            .ToList();
        return TypedResults.Json(new FilterResponse(allowed, caller.Value.NodeId),
            AuthJsonContext.Default.FilterResponse);
    }

    /// <summary>
    /// The access rule. Grants cover both actions; admins may do anything;
    /// a node may read (not invoke) itself; unknown targets are always denied.
    /// </summary>
    internal static (bool Allowed, string Reason) Decide(
        (string NodeId, bool IsAdmin) caller, HashSet<string> grants, bool targetExists, string target, string action)
    {
        if (!targetExists) return (false, "unknown target node");
        if (caller.IsAdmin) return (true, "admin");
        if (target == caller.NodeId) return action == "read" ? (true, "self") : (false, "a node cannot invoke itself");
        return grants.Contains(target) ? (true, "granted") : (false, "no permission for this target");
    }

    // ── Registration ────────────────────────────────────────────────────────

    /// <summary>
    /// Called by the router when a node registers. Creates the identity with
    /// no permissions and returns its one-time secret.
    /// </summary>
    private static async Task<IResult> Register(HttpRequest req, Store store)
    {
        var body = new RegisterRequest(null, null, null);
        if (req.ContentLength is > 0 || req.HasJsonContentType())
        {
            var (parsed, error) = await ReadJson(req, AuthJsonContext.Default.RegisterRequest);
            if (error is not null) return error;
            body = parsed!;
        }
        if (body.NodeId is not null && !Security.IsValidNodeId(body.NodeId))
            return Error(400, "invalid_request", "node_id may contain letters, digits, '.', '_' and '-' (max 64)");
        if (body.Name is { Length: > 256 } || body.Host is { Length: > 512 })
            return Error(400, "invalid_request", "name (max 256) or host (max 512) too long");

        var secret = Security.NewClientSecret();
        var nodeId = body.NodeId;
        if (nodeId is null)
        {
            // generated id; retry on the (astronomically unlikely) collision
            do nodeId = Security.NewNodeId();
            while (!store.CreateNode(nodeId, body.Name, body.Host, Security.Hash(secret), isAdmin: false));
        }
        else if (!store.CreateNode(nodeId, body.Name, body.Host, Security.Hash(secret), isAdmin: false))
        {
            return Error(409, "node_exists", $"Node '{nodeId}' is already registered");
        }

        req.HttpContext.Response.Headers.CacheControl = "no-store";
        return TypedResults.Json(new RegisterResponse(nodeId, nodeId, secret, false, []),
            AuthJsonContext.Default.RegisterResponse, statusCode: 201);
    }

    // ── Admin ───────────────────────────────────────────────────────────────

    private static IResult ListNodes(HttpRequest req, Store store)
    {
        if (RequireAdmin(req, store) is { } denied) return denied;
        return TypedResults.Json(store.ListNodes().Select(n => n.ToView()).ToList(),
            AuthJsonContext.Default.ListNodeView);
    }

    private static IResult GetNode(HttpRequest req, Store store, [FromRoute(Name = "node_id")] string nodeId)
    {
        if (RequireAdmin(req, store) is { } denied) return denied;
        return store.GetNode(nodeId) is { } node ? NodeResult(node) : NotFound(nodeId);
    }

    private static async Task<IResult> UpdatePermissions(HttpRequest req, Store store,
        [FromRoute(Name = "node_id")] string nodeId)
    {
        if (RequireAdmin(req, store) is { } denied) return denied;
        var (body, error) = await ReadJson(req, AuthJsonContext.Default.UpdatePermissionsRequest);
        if (error is not null) return error;
        if (store.GetNode(nodeId) is null) return NotFound(nodeId);

        var add = body!.AddAllowedNodes ?? [];
        var remove = body.RemoveAllowedNodes ?? [];
        if (ContainsNull(add) || ContainsNull(remove))
            return Error(400, "invalid_request", "add_allowed_nodes / remove_allowed_nodes must not contain null");
        if (add.Contains(nodeId)) return Error(400, "invalid_request", "A node can't be granted access to itself");
        var unknown = add.Except(store.ExistingNodeIds(add)).ToList();
        if (unknown.Count > 0) return Error(400, "unknown_nodes", "Unknown node ids: " + string.Join(", ", unknown));

        store.UpdateGrants(nodeId, add, remove);
        return NodeResult(store.GetNode(nodeId)!);
    }

    private static IResult Promote(HttpRequest req, Store store, [FromRoute(Name = "node_id")] string nodeId)
    {
        if (RequireAdmin(req, store) is { } denied) return denied;
        return store.SetAdmin(nodeId) ? NodeResult(store.GetNode(nodeId)!) : NotFound(nodeId);
    }

    /// <summary>Revokes the identity: its tokens stop working on the very next call.</summary>
    private static IResult DeleteNode(HttpRequest req, Store store, [FromRoute(Name = "node_id")] string nodeId)
    {
        if (RequireAdmin(req, store) is { } denied) return denied;
        var node = store.GetNode(nodeId);
        if (node is null) return NotFound(nodeId);
        if (node.IsAdmin && store.CountAdmins() == 1)
            return Error(409, "last_admin", "Refusing to delete the last admin; promote another node first");
        store.DeleteNode(nodeId);
        return TypedResults.NoContent();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static ValueTask<object?> RequireRouterKey(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var opt = ctx.HttpContext.RequestServices.GetRequiredService<AuthOptions>();
        var key = ctx.HttpContext.Request.Headers["X-Router-Key"].ToString();
        if (key.Length == 0 || !Security.EqualsConstantTime(key, opt.RouterKey))
            return ValueTask.FromResult<object?>(Error(401, "invalid_router_key", "Missing or wrong X-Router-Key"));
        return next(ctx);
    }

    /// <summary>The node behind "Authorization: Bearer &lt;token&gt;", if the token is live.</summary>
    private static (string NodeId, bool IsAdmin)? ResolveCaller(HttpRequest req, Store store)
    {
        if (!AuthenticationHeaderValue.TryParse(req.Headers.Authorization, out var h)
            || !"Bearer".Equals(h.Scheme, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrEmpty(h.Parameter))
            return null;
        return store.ResolveToken(Security.Hash(h.Parameter));
    }

    private static IResult? RequireAdmin(HttpRequest req, Store store)
    {
        var caller = ResolveCaller(req, store);
        if (caller is null) return InvalidToken();
        return caller.Value.IsAdmin ? null : Error(403, "insufficient_scope", "Admin rights required");
    }

    private static async Task<(T? Body, IResult? Error)> ReadJson<T>(HttpRequest req, JsonTypeInfo<T> type)
        where T : class
    {
        if (!req.HasJsonContentType())
            return (null, Error(415, "invalid_request", "Content-Type must be application/json"));
        try
        {
            var body = await JsonSerializer.DeserializeAsync(req.Body, type, req.HttpContext.RequestAborted);
            return body is null ? (null, Error(400, "invalid_request", "Empty JSON body")) : (body, null);
        }
        catch (JsonException e)
        {
            return (null, Error(400, "invalid_request", "Malformed JSON: " + e.Message));
        }
    }

    private static bool IsAction(string? action) => action is "read" or "invoke";

    /// <summary>JSON can put null into a List&lt;string&gt; despite the type; SQLite would then throw (500).</summary>
    private static bool ContainsNull(List<string> ids) => ids.Exists(id => id is null);

    private static string? NullIfEmpty(string s) => s.Length == 0 ? null : s;

    private static IResult NodeResult(NodeRecord node) =>
        TypedResults.Json(node.ToView(), AuthJsonContext.Default.NodeView);

    private static IResult NotFound(string nodeId) => Error(404, "node_not_found", $"No node '{nodeId}'");

    private static IResult InvalidToken() =>
        Error(401, "invalid_token", "Missing, unknown, expired or revoked bearer token");

    private static IResult Error(int status, string error, string description) =>
        TypedResults.Json(new ErrorResponse(error, description), AuthJsonContext.Default.ErrorResponse,
            statusCode: status);
}
