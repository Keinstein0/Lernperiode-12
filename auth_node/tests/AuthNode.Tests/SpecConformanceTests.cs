using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace AuthNode.Tests;

/// <summary>
/// Driven by uniskv2-interactions.md (repo root): the router's MCP tools and
/// which Auth Node call + action each one makes. If the spec table changes,
/// change the rows here.
/// </summary>
public sealed class SpecConformanceTests : IDisposable
{
    private readonly AuthApi _api = new();
    public void Dispose() => _api.Dispose();

    // Router table, "Auth / Authz" column: tool -> /auth/permissions/check action
    [Theory]
    [InlineData("get_node", "read")]
    [InlineData("get_extended_agent_card", "read")]
    [InlineData("send_task", "invoke")]
    [InlineData("send_task_stream", "invoke")]
    [InlineData("get_task", "read")]
    [InlineData("subscribe_task", "read")]
    [InlineData("cancel_task", "invoke")]
    public async Task Router_tool_check_is_denied_until_granted(string tool, string action)
    {
        var (a4, s) = await _api.Register("a4");
        await _api.Register("b2");
        var token = await _api.Login(a4, s);

        Assert.False(await _api.Allowed(token, "b2", action), $"{tool}: a fresh node must have 0 permissions");
        await _api.Grant(await _api.AdminToken(), "a4", "b2");
        Assert.True(await _api.Allowed(token, "b2", action), $"{tool}: granted node must pass");
    }

    // get_nodes: "Valid caller token + POST /auth/permissions/filter (action: "read")"
    [Fact]
    public async Task Get_nodes_filters_to_permitted_nodes()
    {
        var (a4, s) = await _api.Register("a4");
        foreach (var n in new[] { "b2-coder", "b2-research" }) await _api.Register(n);
        await _api.Grant(await _api.AdminToken(), "a4", "b2-research");
        var token = await _api.Login(a4, s);

        var r = await _api.Send(HttpMethod.Post, "/auth/permissions/filter",
            new { node_ids = new[] { "b2-coder", "b2-research" }, action = "read" }, token);
        var j = await AuthApi.Json(r);
        Assert.Equal(["b2-research"], j["allowed_node_ids"]!.AsArray().Select(n => n!.GetValue<string>()));
        Assert.Equal("a4", j["caller_node_id"]!.GetValue<string>());
    }

    // Lists the router forwards from MCP input must never crash the Auth Node.
    [Theory]
    [InlineData("POST", "/auth/permissions/filter", """{"node_ids":["a4",null],"action":"read"}""")]
    [InlineData("PATCH", "/auth/nodes/a4", """{"add_allowed_nodes":[null]}""")]
    [InlineData("PATCH", "/auth/nodes/a4", """{"remove_allowed_nodes":[null]}""")]
    public async Task Null_ids_are_a_400_not_a_500(string method, string url, string body)
    {
        await _api.Register("a4");
        var req = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _api.AdminToken());
        var r = await _api.Http.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("invalid_request", (await AuthApi.Json(r))["error"]!.GetValue<string>());
    }

    // login: "OAuth 2.1 Client Credentials" -> grant_type is REQUIRED (RFC 6749 §4.4.2)
    [Fact]
    public async Task Login_without_grant_type_is_rejected()
    {
        var (id, s) = await _api.Register("n1");
        var r = await _api.Send(HttpMethod.Post, "/auth/node/login", new { client_id = id, client_secret = s });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("invalid_request", (await AuthApi.Json(r))["error"]!.GetValue<string>());
    }

    // RFC 6749 §5.2: failed HTTP Basic client auth MUST include WWW-Authenticate
    [Fact]
    public async Task Failed_basic_login_sends_a_challenge()
    {
        await _api.Register("n1");
        var req = new HttpRequestMessage(HttpMethod.Post, "/auth/node/login")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String("n1:wrong"u8.ToArray()));
        var r = await _api.Http.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Equal("Basic", Assert.Single(r.Headers.WwwAuthenticate).Scheme);

        // JSON/form clients didn't use the header, so they get no challenge
        var json = await _api.Send(HttpMethod.Post, "/auth/node/login",
            new { grant_type = "client_credentials", client_id = "n1", client_secret = "wrong" });
        Assert.Empty(json.Headers.WwwAuthenticate);
    }
}
