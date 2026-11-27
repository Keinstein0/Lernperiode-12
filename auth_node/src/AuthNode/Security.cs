using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AuthNode;

/// <summary>
/// Secrets and tokens are 256-bit random values. Only their SHA-256 hash is
/// stored: for high-entropy random values a fast hash is enough (no password
/// hashing needed), and a leaked database reveals no usable credential.
/// </summary>
internal static partial class Security
{
    public static string NewClientSecret() => "sk_" + Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string NewAccessToken() => "at_" + Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string NewNodeId() => "node-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));

    public static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    /// <summary>Compares in constant time, so timing reveals nothing about the stored value.</summary>
    public static bool Matches(string candidate, byte[] storedHash) =>
        CryptographicOperations.FixedTimeEquals(Hash(candidate), storedHash);

    public static bool EqualsConstantTime(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Hash(a), Hash(b));

    /// <summary>Node ids end up in URLs and logs: letters, digits, . _ - ; max 64.</summary>
    public static bool IsValidNodeId(string id) => NodeIdPattern().IsMatch(id);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")]
    private static partial Regex NodeIdPattern();

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>Settings, read once from environment variables.</summary>
internal sealed record AuthOptions(string RouterKey, string DbPath, TimeSpan TokenLifetime)
{
    public static AuthOptions FromConfiguration(IConfiguration cfg)
    {
        // Removed: a standing admin secret in the environment is an admin login anyone
        // with access to .env / compose / `docker inspect` can use over the network.
        if (!string.IsNullOrWhiteSpace(cfg["AUTH_BOOTSTRAP_ADMIN_SECRET"]))
            throw new InvalidOperationException(
                "AUTH_BOOTSTRAP_ADMIN_SECRET is no longer supported; remove it from the environment. " +
                "Create the first admin once with: AuthNode bootstrap-admin");
        var routerKey = cfg["AUTH_ROUTER_KEY"];
        if (string.IsNullOrWhiteSpace(routerKey) || routerKey.Length < 32)
            throw new InvalidOperationException(
                "AUTH_ROUTER_KEY must be set to a random string of at least 32 characters " +
                "(the router sends it in the X-Router-Key header).");
        var lifetime = int.TryParse(cfg["AUTH_TOKEN_LIFETIME_SECONDS"], out var s) && s > 0 ? s : 900;
        return new AuthOptions(routerKey, DbPathFrom(cfg), TimeSpan.FromSeconds(lifetime));
    }

    /// <summary>Shared with the bootstrap-admin command, which needs the database but no router key.</summary>
    public static string DbPathFrom(IConfiguration cfg) => cfg["AUTH_DB_PATH"] ?? "data/auth.db";
}
