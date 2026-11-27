using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace AuthNode.Tests;

public sealed class RouterKeyTests : IDisposable
{
    private readonly AuthApi _api = new();
    public void Dispose() => _api.Dispose();

    [Theory]
    [InlineData("POST", "/auth/node/login")]
    [InlineData("POST", "/auth/permissions/check")]
    [InlineData("POST", "/auth/permissions/filter")]
    [InlineData("POST", "/auth/nodes")]
    [InlineData("GET", "/auth/nodes")]
    [InlineData("GET", "/auth/nodes/admin")]
    [InlineData("PATCH", "/auth/nodes/admin")]
    [InlineData("POST", "/auth/nodes/admin/promote")]
    [InlineData("DELETE", "/auth/nodes/admin")]
    public async Task Every_auth_route_needs_the_router_key(string method, string url)
    {
        foreach (var key in new[] { null, "wrong-key" })
        {
            var req = new HttpRequestMessage(new HttpMethod(method), url);
            if (key is not null) req.Headers.TryAddWithoutValidation("X-Router-Key", key);
            var r = await SendWithoutDefaultKey(req);
            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
            Assert.Equal("invalid_router_key", (await AuthApi.Json(r))["error"]!.GetValue<string>());
        }
    }

    private async Task<HttpResponseMessage> SendWithoutDefaultKey(HttpRequestMessage req)
    {
        _api.Http.DefaultRequestHeaders.Remove("X-Router-Key");
        try { return await _api.Http.SendAsync(req); }
        finally { _api.Http.DefaultRequestHeaders.Add("X-Router-Key", AuthApi.RouterKey); }
    }

    [Fact]
    public async Task Health_needs_no_key()
    {
        _api.Http.DefaultRequestHeaders.Remove("X-Router-Key");
        var r = await _api.Http.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }
}

public sealed class RegistrationAndLoginTests : IDisposable
{
    private readonly AuthApi _api = new();
    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Registration_returns_a_one_time_secret_and_no_permissions()
    {
        var r = await _api.Send(HttpMethod.Post, "/auth/nodes", new { node_id = "b2-coder", name = "Coder", host = "10.0.0.5:9000" });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var j = await AuthApi.Json(r);
        Assert.Equal("b2-coder", j["client_id"]!.GetValue<string>());
        Assert.StartsWith("sk_", j["client_secret"]!.GetValue<string>());
        Assert.False(j["is_admin"]!.GetValue<bool>());
        Assert.Empty(j["allowed_nodes"]!.AsArray());

        // the secret is never shown again
        var admin = await _api.AdminToken();
        var view = await (await _api.Send(HttpMethod.Get, "/auth/nodes/b2-coder", bearer: admin)).Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret", view);
        Assert.Contains("10.0.0.5:9000", view);
    }

    [Fact]
    public async Task Registration_without_id_generates_one()
    {
        var (id, _) = await _api.Register();
        Assert.StartsWith("node-", id);
    }

    [Fact]
    public async Task Duplicate_and_invalid_ids_are_rejected()
    {
        await _api.Register("dup");
        Assert.Equal(HttpStatusCode.Conflict, (await _api.Send(HttpMethod.Post, "/auth/nodes", new { node_id = "dup" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _api.Send(HttpMethod.Post, "/auth/nodes", new { node_id = "../evil" })).StatusCode);
    }

    [Fact]
    public async Task Login_works_with_json_form_and_basic()
    {
        var (id, secret) = await _api.Register("n1");
        Assert.StartsWith("at_", await _api.Login(id, secret));

        var form = await _api.Http.PostAsync("/auth/node/login", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["grant_type"] = "client_credentials", ["client_id"] = id, ["client_secret"] = secret }));
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        var j = await AuthApi.Json(form);
        Assert.Equal("Bearer", j["token_type"]!.GetValue<string>());
        Assert.Equal(900, j["expires_in"]!.GetValue<int>());
        Assert.Equal("no-store", form.Headers.CacheControl!.ToString());

        var basic = new HttpRequestMessage(HttpMethod.Post, "/auth/node/login")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" })
        };
        basic.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{id}:{secret}")));
        Assert.Equal(HttpStatusCode.OK, (await _api.Http.SendAsync(basic)).StatusCode);
    }

    [Fact]
    public async Task Bad_credentials_get_oauth_errors()
    {
        var (id, _) = await _api.Register("n2");
        async Task<(HttpStatusCode, string)> Try(object body)
        {
            var r = await _api.Send(HttpMethod.Post, "/auth/node/login", body);
            return (r.StatusCode, (await AuthApi.Json(r))["error"]!.GetValue<string>());
        }
        const string cc = "client_credentials";
        Assert.Equal((HttpStatusCode.Unauthorized, "invalid_client"), await Try(new { grant_type = cc, client_id = id, client_secret = "sk_wrong" }));
        Assert.Equal((HttpStatusCode.Unauthorized, "invalid_client"), await Try(new { grant_type = cc, client_id = "ghost", client_secret = "x" }));
        Assert.Equal((HttpStatusCode.BadRequest, "invalid_request"), await Try(new { grant_type = cc, client_id = id }));
        Assert.Equal((HttpStatusCode.BadRequest, "unsupported_grant_type"),
            await Try(new { grant_type = "password", client_id = id, client_secret = "x" }));
    }

    [Fact]
    public async Task Malformed_json_is_a_clean_400()
    {
        var r = await _api.Http.PostAsync("/auth/nodes", new StringContent("{not json", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("invalid_request", (await AuthApi.Json(r))["error"]!.GetValue<string>());
    }
}

public sealed class PermissionTests : IDisposable
{
    private readonly AuthApi _api = new();
    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task New_nodes_can_do_nothing_until_an_admin_grants_access()
    {
        var (a4, a4Secret) = await _api.Register("a4");
        var (b2, b2Secret) = await _api.Register("b2");
        var a4Token = await _api.Login(a4, a4Secret);

        Assert.False(await _api.Allowed(a4Token, "b2", "read"));
        Assert.False(await _api.Allowed(a4Token, "b2", "invoke"));

        var admin = await _api.AdminToken();
        var granted = await AuthApi.Json(await _api.Grant(admin, "a4", "b2"));
        Assert.Equal("b2", granted["allowed_nodes"]![0]!.GetValue<string>());

        Assert.True(await _api.Allowed(a4Token, "b2", "read"));
        Assert.True(await _api.Allowed(a4Token, "b2", "invoke"));

        // grants are one-way: b2 still can't reach a4
        var b2Token = await _api.Login(b2, b2Secret);
        Assert.False(await _api.Allowed(b2Token, "a4", "invoke"));

        // revoke
        await _api.Send(HttpMethod.Patch, "/auth/nodes/a4", new { remove_allowed_nodes = new[] { "b2" } }, admin);
        Assert.False(await _api.Allowed(a4Token, "b2"));
    }

    [Fact]
    public async Task Self_read_admin_and_unknown_targets()
    {
        var (id, secret) = await _api.Register("solo");
        var token = await _api.Login(id, secret);
        Assert.True(await _api.Allowed(token, "solo", "read"));
        Assert.False(await _api.Allowed(token, "solo", "invoke"));

        var admin = await _api.AdminToken();
        Assert.True(await _api.Allowed(admin, "solo", "invoke"));
        Assert.False(await _api.Allowed(admin, "does-not-exist", "read"));
    }

    [Fact]
    public async Task Filter_keeps_order_and_drops_what_is_not_allowed()
    {
        var (a4, s) = await _api.Register("a4");
        foreach (var n in new[] { "x", "y", "z" }) await _api.Register(n);
        await _api.Grant(await _api.AdminToken(), "a4", "z", "x");
        var token = await _api.Login(a4, s);

        var r = await _api.Send(HttpMethod.Post, "/auth/permissions/filter",
            new { node_ids = new[] { "z", "y", "ghost", "x", "a4", "x" }, action = "invoke" }, token);
        var ids = (await AuthApi.Json(r))["allowed_node_ids"]!.AsArray().Select(n => n!.GetValue<string>());
        Assert.Equal(["z", "x"], ids);
    }

    [Fact]
    public async Task Bad_tokens_and_actions()
    {
        var r = await _api.Send(HttpMethod.Post, "/auth/permissions/check", new { target_node_id = "admin", action = "read" }, "at_forged");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Equal("invalid_token", (await AuthApi.Json(r))["error"]!.GetValue<string>());

        var admin = await _api.AdminToken();
        var bad = await _api.Send(HttpMethod.Post, "/auth/permissions/check", new { target_node_id = "admin", action = "delete" }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }
}

public sealed class TokenExpiryTests : IDisposable
{
    private readonly AuthApi _api = new(tokenLifetimeSeconds: 1);
    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Tokens_stop_working_when_they_expire()
    {
        var admin = await _api.AdminToken();
        Assert.True(await _api.Allowed(admin, "admin", "read"));
        await Task.Delay(2100);
        var r = await _api.Send(HttpMethod.Post, "/auth/permissions/check", new { target_node_id = "admin", action = "read" }, admin);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }
}

public sealed class AdminTests : IDisposable
{
    private readonly AuthApi _api = new();
    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Admin_routes_need_an_admin_token()
    {
        var (id, s) = await _api.Register("plain");
        var plain = await _api.Login(id, s);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Send(HttpMethod.Get, "/auth/nodes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.Send(HttpMethod.Get, "/auth/nodes", bearer: plain)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.Grant(plain, "plain", "admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.Send(HttpMethod.Post, "/auth/nodes/plain/promote", bearer: plain)).StatusCode);
    }

    [Fact]
    public async Task List_get_and_patch_validation()
    {
        var admin = await _api.AdminToken();
        await _api.Register("n1");
        var list = (await AuthApi.Json(await _api.Send(HttpMethod.Get, "/auth/nodes", bearer: admin))).AsArray();
        Assert.Equal(["admin", "n1"], list.Select(n => n!["node_id"]!.GetValue<string>()));
        Assert.True(list[0]!["is_admin"]!.GetValue<bool>());

        Assert.Equal(HttpStatusCode.NotFound, (await _api.Send(HttpMethod.Get, "/auth/nodes/ghost", bearer: admin)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.Grant(admin, "ghost", "n1")).StatusCode);
        var unknown = await _api.Grant(admin, "n1", "ghost");
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Contains("ghost", (await AuthApi.Json(unknown))["error_description"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, (await _api.Grant(admin, "n1", "n1")).StatusCode);
    }

    [Fact]
    public async Task Promote_makes_a_node_admin()
    {
        var admin = await _api.AdminToken();
        var (id, s) = await _api.Register("n1");
        await _api.Register("n2");
        var r = await AuthApi.Json(await _api.Send(HttpMethod.Post, "/auth/nodes/n1/promote", bearer: admin));
        Assert.True(r["is_admin"]!.GetValue<bool>());
        Assert.True(await _api.Allowed(await _api.Login(id, s), "n2", "invoke"));
    }

    [Fact]
    public async Task Delete_kills_access_immediately_and_removes_grants_both_ways()
    {
        var admin = await _api.AdminToken();
        var (a4, s) = await _api.Register("a4");
        await _api.Register("b2");
        await _api.Grant(admin, "a4", "b2");
        var b2Grant = await _api.Grant(admin, "b2", "a4");
        var a4Token = await _api.Login(a4, s);

        Assert.Equal(HttpStatusCode.NoContent, (await _api.Send(HttpMethod.Delete, "/auth/nodes/a4", bearer: admin)).StatusCode);

        // its token is dead right away
        var r = await _api.Send(HttpMethod.Post, "/auth/permissions/check", new { target_node_id = "b2", action = "read" }, a4Token);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        // it can't log in again
        var login = await _api.Send(HttpMethod.Post, "/auth/node/login", new { grant_type = "client_credentials", client_id = "a4", client_secret = s });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        // and b2's grant to it is gone
        var b2 = await AuthApi.Json(await _api.Send(HttpMethod.Get, "/auth/nodes/b2", bearer: admin));
        Assert.Empty(b2["allowed_nodes"]!.AsArray());
        // the id can be registered fresh
        await _api.Register("a4");
    }

    [Fact]
    public async Task The_last_admin_cannot_be_deleted()
    {
        var admin = await _api.AdminToken();
        Assert.Equal(HttpStatusCode.Conflict, (await _api.Send(HttpMethod.Delete, "/auth/nodes/admin", bearer: admin)).StatusCode);

        await _api.Register("second");
        await _api.Send(HttpMethod.Post, "/auth/nodes/second/promote", bearer: admin);
        Assert.Equal(HttpStatusCode.NoContent, (await _api.Send(HttpMethod.Delete, "/auth/nodes/admin", bearer: admin)).StatusCode);
    }
}

/// <summary>Each test runs the app twice on the same SQLite file, like a container restart.</summary>
public sealed class PersistenceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "authnode-persist-" + Guid.NewGuid().ToString("N"));

    // Runs after every AuthApi in the test is disposed, so Windows no longer holds auth.db open.
    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task Identities_and_grants_survive_a_restart()
    {
        string secret;
        using (var first = new AuthApi(dbDir: _dir))
        {
            (_, secret) = await first.Register("keeper");
            await first.Register("target");
            await first.Grant(await first.AdminToken(), "keeper", "target");
        }
        using var second = new AuthApi(dbDir: _dir);
        var token = await second.Login("keeper", secret);
        Assert.True(await second.Allowed(token, "target"));
    }

    [Fact]
    public async Task Deleted_bootstrap_admin_stays_deleted_after_a_restart()
    {
        // README: log in as admin, promote a real admin, then the bootstrap admin can be deleted.
        string bootSecret, realAdminSecret;
        using (var first = new AuthApi(dbDir: _dir))
        {
            bootSecret = first.AdminSecret!;
            var boot = await first.AdminToken();
            (_, realAdminSecret) = await first.Register("real-admin");
            await first.Send(HttpMethod.Post, "/auth/nodes/real-admin/promote", bearer: boot);
            Assert.Equal(HttpStatusCode.NoContent, (await first.Send(HttpMethod.Delete, "/auth/nodes/admin", bearer: boot)).StatusCode);
        }

        // Restart: nothing brings the bootstrap admin back, and bootstrap can't be re-run.
        using var second = new AuthApi(dbDir: _dir);
        Assert.Null(second.AdminSecret);
        var login = await second.Send(HttpMethod.Post, "/auth/node/login",
            new { grant_type = "client_credentials", client_id = "admin", client_secret = bootSecret });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        var real = await second.Login("real-admin", realAdminSecret);
        Assert.Equal(HttpStatusCode.OK, (await second.Send(HttpMethod.Get, "/auth/nodes", bearer: real)).StatusCode);
    }
}

/// <summary>`AuthNode bootstrap-admin`: the only way to create an admin without being one.</summary>
public sealed class BootstrapTests : IDisposable
{
    private readonly AuthApi _api = new(bootstrapAdmin: false);
    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Without_bootstrap_there_is_no_admin_at_all()
    {
        // The server never creates an admin by itself, so no secret can log in as one.
        var r = await _api.Send(HttpMethod.Post, "/auth/node/login",
            new { grant_type = "client_credentials", client_id = "admin", client_secret = "sk_anything" });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Bootstrap_prints_a_one_time_secret_that_logs_in_as_admin()
    {
        var (exit, secret, stderr) = AuthApi.RunBootstrap(_api.DbPath);
        Assert.Equal(0, exit);
        Assert.StartsWith("sk_", secret);
        Assert.DoesNotContain(secret!, stderr);   // stdout carries only the secret
        Assert.Contains("shown only once", stderr);

        var token = await _api.Login("admin", secret!);
        var me = await AuthApi.Json(await _api.Send(HttpMethod.Get, "/auth/nodes/admin", bearer: token));
        Assert.True(me["is_admin"]!.GetValue<bool>());
    }

    [Fact]
    public void Bootstrap_is_refused_once_any_admin_exists()
    {
        Assert.Equal(0, AuthApi.RunBootstrap(_api.DbPath, "first").Exit);
        var (exit, secret, stderr) = AuthApi.RunBootstrap(_api.DbPath, "second");
        Assert.Equal(1, exit);
        Assert.Null(secret);
        Assert.Contains("admin already exists", stderr);
    }

    [Fact]
    public async Task Bootstrap_is_refused_even_after_the_bootstrap_admin_was_deleted()
    {
        var token = await _api.Login("admin", AuthApi.RunBootstrap(_api.DbPath).Stdout!);
        await _api.Register("real-admin");
        await _api.Send(HttpMethod.Post, "/auth/nodes/real-admin/promote", bearer: token);
        await _api.Send(HttpMethod.Delete, "/auth/nodes/admin", bearer: token);

        Assert.Equal(1, AuthApi.RunBootstrap(_api.DbPath).Exit);
    }

    [Fact]
    public async Task Bootstrap_does_not_take_over_an_existing_non_admin_id()
    {
        await _api.Register("taken");
        var (exit, _, stderr) = AuthApi.RunBootstrap(_api.DbPath, "taken");
        Assert.Equal(1, exit);
        Assert.Contains("already taken", stderr);
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("a", "b")]
    public void Bad_usage_exits_2(params string[] args) =>
        Assert.Equal(2, AuthApi.RunBootstrap(_api.DbPath, args).Exit);

    [Fact]
    public void Concurrent_bootstraps_create_exactly_one_admin()
    {
        var exits = Enumerable.Range(0, 8).AsParallel()
            .Select(i => AuthApi.RunBootstrap(_api.DbPath, $"admin-{i}").Exit).ToList();
        Assert.Equal(1, exits.Count(e => e == 0));
        Assert.Equal(7, exits.Count(e => e == 1));
    }

    [Fact]
    public void The_old_env_var_stops_the_server_from_starting()
    {
        var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("AUTH_ROUTER_KEY", AuthApi.RouterKey);
            b.UseSetting("AUTH_DB_PATH", _api.DbPath);
            b.UseSetting("AUTH_BOOTSTRAP_ADMIN_SECRET", new string('s', 40));
        });
        using (factory)
        {
            var e = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
            Assert.Contains("AUTH_BOOTSTRAP_ADMIN_SECRET is no longer supported", e.ToString());
        }
    }
}
