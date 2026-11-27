using AuthNode;

// One-off admin command: `AuthNode bootstrap-admin [node_id]` (see Bootstrap.Run).
if (args is ["bootstrap-admin", .. var rest])
{
    var env = new ConfigurationBuilder().AddEnvironmentVariables().Build();
    return Bootstrap.Run(rest, AuthOptions.DbPathFrom(env), Console.Out, Console.Error);
}

// Auth node: identities, short-lived tokens and node-to-node permissions.
// Only the router talks to it (every /auth route needs X-Router-Key).
var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.TypeInfoResolverChain.Insert(0, AuthJsonContext.Default));
builder.Services.AddSingleton(sp => AuthOptions.FromConfiguration(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton(sp => new Store(sp.GetRequiredService<AuthOptions>().DbPath));
builder.Services.AddHostedService<TokenCleanup>();

var app = builder.Build();

// Fail fast on bad settings.
app.Services.GetRequiredService<AuthOptions>();
Bootstrap.WarnIfNoAdmin(app.Services.GetRequiredService<Store>(), app.Logger);

Endpoints.Map(app);
app.Run();
return 0;

/// <summary>Visible to the integration tests.</summary>
public partial class Program;

/// <summary>
/// Nodes start with no permissions and only an admin can grant any, so the
/// first admin has to be created out of band. The server itself never creates
/// admins: there is no standing secret in the environment that could be used
/// over the network. Running this command needs access to the database
/// (host or volume), which already means full control over it.
/// </summary>
internal static class Bootstrap
{
    private const string Usage = "usage: AuthNode bootstrap-admin [node_id]   (default node_id: admin)";

    /// <summary>
    /// Creates the first admin, but only if the database has no admin at all.
    /// The generated secret goes to stdout (alone, so it can be captured);
    /// everything else goes to stderr.
    /// </summary>
    /// <returns>0 created, 1 refused, 2 bad usage.</returns>
    public static int Run(string[] args, string dbPath, TextWriter stdout, TextWriter stderr)
    {
        if (args is ["-h" or "--help"]) { stderr.WriteLine(Usage); return 0; }
        if (args.Length > 1) { stderr.WriteLine(Usage); return 2; }
        var id = args is [var given] ? given : "admin";
        if (!Security.IsValidNodeId(id))
        {
            stderr.WriteLine("node_id may contain letters, digits, '.', '_' and '-' (max 64).");
            return 2;
        }

        var store = new Store(dbPath);
        var secret = Security.NewClientSecret();
        if (!store.CreateFirstAdmin(id, Security.Hash(secret)))
        {
            stderr.WriteLine(store.CountAdmins() > 0
                ? "Refused: an admin already exists. bootstrap-admin only works on a database without admins; " +
                  "use POST /auth/nodes/{node_id}/promote as an existing admin instead."
                : $"Refused: node id '{id}' is already taken. Choose another: AuthNode bootstrap-admin <node_id>");
            return 1;
        }

        stdout.WriteLine(secret);
        stderr.WriteLine($"Created admin '{id}'. The client_secret above is shown only once; " +
                         $"log in with POST /auth/node/login as client_id '{id}'.");
        return 0;
    }

    public static void WarnIfNoAdmin(Store store, ILogger log)
    {
        if (store.CountAdmins() == 0)
            log.LogWarning("No admin exists, so permissions can't be granted yet. " +
                           "Create the first one once with: AuthNode bootstrap-admin");
    }
}

/// <summary>Deletes expired tokens every 5 minutes (they're already rejected; this just keeps the table small).</summary>
internal sealed class TokenCleanup(Store store, ILogger<TokenCleanup> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(stop))
        {
            var n = store.DeleteExpiredTokens();
            if (n > 0) log.LogDebug("Removed {Count} expired tokens", n);
        }
    }
}
