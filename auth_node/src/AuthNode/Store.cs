using Microsoft.Data.Sqlite;

namespace AuthNode;

internal sealed record NodeRecord(
    string NodeId, string? Name, string? Host, bool IsAdmin, DateTimeOffset CreatedAt, List<string> AllowedNodes)
{
    public NodeView ToView() => new(NodeId, Name, Host, IsAdmin, AllowedNodes, CreatedAt);
}

/// <summary>
/// All persistence. Plain SQL with manual mapping, so nothing here needs
/// reflection (Native AOT safe). One short-lived connection per call; the
/// driver pools them.
/// </summary>
internal sealed class Store
{
    private readonly string _connectionString;

    public Store(string dbPath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(dbPath));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = true }.ToString();
        Init();
    }

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(_connectionString);
        c.Open();
        Exec(c, "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;");
        return c;
    }

    private static void Exec(SqliteConnection c, string sql, SqliteTransaction? tx = null)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private void Init()
    {
        using var c = Open();
        Exec(c, """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS nodes (
                node_id     TEXT PRIMARY KEY,
                name        TEXT,
                host        TEXT,
                secret_hash BLOB NOT NULL,
                is_admin    INTEGER NOT NULL DEFAULT 0,
                created_at  INTEGER NOT NULL
            );
            -- node_id may act on target_node_id. Deleting either node removes the grant.
            CREATE TABLE IF NOT EXISTS grants (
                node_id        TEXT NOT NULL REFERENCES nodes(node_id) ON DELETE CASCADE,
                target_node_id TEXT NOT NULL REFERENCES nodes(node_id) ON DELETE CASCADE,
                PRIMARY KEY (node_id, target_node_id)
            );
            CREATE TABLE IF NOT EXISTS tokens (
                token_hash BLOB PRIMARY KEY,
                node_id    TEXT NOT NULL REFERENCES nodes(node_id) ON DELETE CASCADE,
                expires_at INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS tokens_expiry ON tokens(expires_at);
            """);
    }

    // ── Nodes ───────────────────────────────────────────────────────────────

    /// <returns>false if the node id is already taken.</returns>
    public bool CreateNode(string nodeId, string? name, string? host, byte[] secretHash, bool isAdmin)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            INSERT INTO nodes (node_id, name, host, secret_hash, is_admin, created_at)
            VALUES ($id, $name, $host, $hash, $admin, $now)
            ON CONFLICT(node_id) DO NOTHING
            """;
        cmd.Parameters.AddWithValue("$id", nodeId);
        cmd.Parameters.AddWithValue("$name", (object?)name ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$host", (object?)host ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$hash", secretHash);
        cmd.Parameters.AddWithValue("$admin", isAdmin ? 1 : 0);
        cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return cmd.ExecuteNonQuery() == 1;
    }

    /// <summary>
    /// Creates an admin only if no admin exists yet. One statement, so two
    /// concurrent bootstrap runs can't both succeed.
    /// </summary>
    /// <returns>false if any admin already exists or the id is taken.</returns>
    public bool CreateFirstAdmin(string nodeId, byte[] secretHash)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            INSERT INTO nodes (node_id, name, host, secret_hash, is_admin, created_at)
            SELECT $id, 'bootstrap admin', NULL, $hash, 1, $now
            WHERE NOT EXISTS (SELECT 1 FROM nodes WHERE is_admin = 1)
            ON CONFLICT(node_id) DO NOTHING
            """;
        cmd.Parameters.AddWithValue("$id", nodeId);
        cmd.Parameters.AddWithValue("$hash", secretHash);
        cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return cmd.ExecuteNonQuery() == 1;
    }

    public NodeRecord? GetNode(string nodeId) => QueryNodes("WHERE n.node_id = $id", nodeId).FirstOrDefault();

    public List<NodeRecord> ListNodes() => QueryNodes("", null);

    private List<NodeRecord> QueryNodes(string where, string? id)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"""
            SELECT n.node_id, n.name, n.host, n.is_admin, n.created_at, g.target_node_id
            FROM nodes n LEFT JOIN grants g ON g.node_id = n.node_id
            {where}
            ORDER BY n.created_at, n.node_id, g.target_node_id
            """;
        if (id is not null) cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        var result = new List<NodeRecord>();
        NodeRecord? current = null;
        while (r.Read())
        {
            var nodeId = r.GetString(0);
            if (current?.NodeId != nodeId)
            {
                current = new NodeRecord(nodeId,
                    r.IsDBNull(1) ? null : r.GetString(1),
                    r.IsDBNull(2) ? null : r.GetString(2),
                    r.GetInt64(3) == 1,
                    DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(4)),
                    []);
                result.Add(current);
            }
            if (!r.IsDBNull(5)) current.AllowedNodes.Add(r.GetString(5));
        }
        return result;
    }

    public byte[]? GetSecretHash(string nodeId)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT secret_hash FROM nodes WHERE node_id = $id";
        cmd.Parameters.AddWithValue("$id", nodeId);
        return cmd.ExecuteScalar() as byte[];
    }

    /// <summary>Which of these ids exist.</summary>
    public HashSet<string> ExistingNodeIds(IEnumerable<string> ids)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM nodes WHERE node_id = $id";
        var p = cmd.Parameters.Add("$id", SqliteType.Text);
        foreach (var id in ids.Distinct())
        {
            p.Value = id;
            if (cmd.ExecuteScalar() is not null) found.Add(id);
        }
        return found;
    }

    public void UpdateGrants(string nodeId, IEnumerable<string> add, IEnumerable<string> remove)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        using (var ins = c.CreateCommand())
        {
            ins.Transaction = tx;
            ins.CommandText = "INSERT OR IGNORE INTO grants (node_id, target_node_id) VALUES ($n, $t)";
            ins.Parameters.AddWithValue("$n", nodeId);
            var t = ins.Parameters.Add("$t", SqliteType.Text);
            foreach (var target in add) { t.Value = target; ins.ExecuteNonQuery(); }
        }
        using (var del = c.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "DELETE FROM grants WHERE node_id = $n AND target_node_id = $t";
            del.Parameters.AddWithValue("$n", nodeId);
            var t = del.Parameters.Add("$t", SqliteType.Text);
            foreach (var target in remove) { t.Value = target; del.ExecuteNonQuery(); }
        }
        tx.Commit();
    }

    public HashSet<string> GetGrants(string nodeId)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT target_node_id FROM grants WHERE node_id = $id";
        cmd.Parameters.AddWithValue("$id", nodeId);
        using var r = cmd.ExecuteReader();
        var set = new HashSet<string>(StringComparer.Ordinal);
        while (r.Read()) set.Add(r.GetString(0));
        return set;
    }

    public bool SetAdmin(string nodeId)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE nodes SET is_admin = 1 WHERE node_id = $id";
        cmd.Parameters.AddWithValue("$id", nodeId);
        return cmd.ExecuteNonQuery() == 1;
    }

    public int CountAdmins()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM nodes WHERE is_admin = 1";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>Deletes the node; its tokens and every grant from or to it go with it.</summary>
    public bool DeleteNode(string nodeId)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM nodes WHERE node_id = $id";
        cmd.Parameters.AddWithValue("$id", nodeId);
        return cmd.ExecuteNonQuery() == 1;
    }

    // ── Tokens ──────────────────────────────────────────────────────────────

    public void SaveToken(byte[] tokenHash, string nodeId, DateTimeOffset expiresAt)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO tokens (token_hash, node_id, expires_at) VALUES ($h, $n, $e)";
        cmd.Parameters.AddWithValue("$h", tokenHash);
        cmd.Parameters.AddWithValue("$n", nodeId);
        cmd.Parameters.AddWithValue("$e", expiresAt.ToUnixTimeSeconds());
        cmd.ExecuteNonQuery();
    }

    /// <summary>The node a live token belongs to, or null if unknown/expired/revoked.</summary>
    public (string NodeId, bool IsAdmin)? ResolveToken(byte[] tokenHash)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT n.node_id, n.is_admin FROM tokens t JOIN nodes n ON n.node_id = t.node_id
            WHERE t.token_hash = $h AND t.expires_at > $now
            """;
        cmd.Parameters.AddWithValue("$h", tokenHash);
        cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        using var r = cmd.ExecuteReader();
        return r.Read() ? (r.GetString(0), r.GetInt64(1) == 1) : null;
    }

    public int DeleteExpiredTokens()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM tokens WHERE expires_at <= $now";
        cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return cmd.ExecuteNonQuery();
    }
}
