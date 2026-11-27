using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AuthNode.Tests;

/// <summary>
/// Runs the real app in memory with its own SQLite file, and plays the router:
/// every helper sends the router key, like the router will.
/// </summary>
public sealed class AuthApi : IDisposable
{
    public const string RouterKey = "test-router-key-0123456789abcdef0123456789";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "authnode-tests-" + Guid.NewGuid().ToString("N"));
    public HttpClient Http { get; }
    public string DbPath { get; }

    /// <summary>
    /// Secret of the "admin" node, created through the real `bootstrap-admin`
    /// code path. Null if the database already had an admin (e.g. a restart
    /// on an existing dbDir) or <paramref name="bootstrapAdmin"/> was false.
    /// </summary>
    public string? AdminSecret { get; }

    public AuthApi(int tokenLifetimeSeconds = 900, string? dbDir = null, bool bootstrapAdmin = true)
    {
        DbPath = Path.Combine(dbDir ?? _dir, "auth.db");
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("AUTH_ROUTER_KEY", RouterKey);
            b.UseSetting("AUTH_DB_PATH", DbPath);
            b.UseSetting("AUTH_TOKEN_LIFETIME_SECONDS", tokenLifetimeSeconds.ToString());
        });
        Http = _factory.CreateClient();   // starts the app, which creates the schema
        Http.DefaultRequestHeaders.Add("X-Router-Key", RouterKey);
        if (bootstrapAdmin) AdminSecret = RunBootstrap(DbPath).Stdout;
    }

    /// <summary>Runs `AuthNode bootstrap-admin [args]` against a database.</summary>
    public static (int Exit, string? Stdout, string Stderr) RunBootstrap(string dbPath, params string[] args)
    {
        using StringWriter stdout = new(), stderr = new();
        var exit = Bootstrap.Run(args, dbPath, stdout, stderr);
        var secret = stdout.ToString().Trim();
        return (exit, secret.Length == 0 ? null : secret, stderr.ToString());
    }

    public async Task<HttpResponseMessage> Send(HttpMethod method, string url, object? json = null, string? bearer = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (json is not null)
            req.Content = new StringContent(JsonSerializer.Serialize(json), Encoding.UTF8, "application/json");
        if (bearer is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return await Http.SendAsync(req);
    }

    public static async Task<JsonNode> Json(HttpResponseMessage r) =>
        JsonNode.Parse(await r.Content.ReadAsStringAsync()) ?? throw new InvalidOperationException("empty body");

    /// <returns>(node_id, client_secret)</returns>
    public async Task<(string Id, string Secret)> Register(string? nodeId = null)
    {
        var r = await Send(HttpMethod.Post, "/auth/nodes", nodeId is null ? new { } : new { node_id = nodeId, host = "10.0.0.9:9000" });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var j = await Json(r);
        return (j["node_id"]!.GetValue<string>(), j["client_secret"]!.GetValue<string>());
    }

    public async Task<string> Login(string id, string secret)
    {
        var r = await Send(HttpMethod.Post, "/auth/node/login",
            new { grant_type = "client_credentials", client_id = id, client_secret = secret });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return (await Json(r))["access_token"]!.GetValue<string>();
    }

    public Task<string> AdminToken() =>
        Login("admin", AdminSecret ?? throw new InvalidOperationException("This instance didn't bootstrap an admin"));

    public async Task<bool> Allowed(string token, string target, string action = "invoke")
    {
        var r = await Send(HttpMethod.Post, "/auth/permissions/check", new { target_node_id = target, action }, token);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return (await Json(r))["allowed"]!.GetValue<bool>();
    }

    public Task<HttpResponseMessage> Grant(string adminToken, string node, params string[] targets) =>
        Send(HttpMethod.Patch, $"/auth/nodes/{node}", new { add_allowed_nodes = targets }, adminToken);

    public void Dispose()
    {
        Http.Dispose();
        _factory.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }
}
