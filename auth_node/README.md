# UniSKv2 Auth node

Keeps track of **who every node is** and **which nodes it may use**, and hands
out short-lived tokens. Only the router ever talks to it.

- ASP.NET Core 10 minimal API, compiled with **Native AOT** to a single native
  binary. Zero trim/AOT warnings, and the build fails if one ever appears.
- **SQLite** through plain ADO.NET (`Microsoft.Data.Sqlite`), with hand-written
  SQL and manual mapping, so no reflection. JSON uses source generation.
- About 39 MB as a Docker image and about 10 MB of memory at idle.

```
auth_node/
├── src/AuthNode/
│   ├── Program.cs     startup, bootstrap admin, expired-token cleanup
│   ├── Endpoints.cs   all routes and the permission rule (Decide)
│   ├── Store.cs       SQLite schema + queries
│   ├── Security.cs    secret/token generation, hashing, settings
│   └── Models.cs      request/response records + source-generated JSON
├── tests/AuthNode.Tests/   50 integration tests (real app, real SQLite)
├── tests/blackbox/         spec test against the real binary (stdlib Python)
└── Dockerfile
```

## Run it

```powershell
# locally
cd auth_node
$env:AUTH_ROUTER_KEY = "<random, 32+ chars>"
dotnet run --project src/AuthNode -- bootstrap-admin   # once: prints the first admin's secret
dotnet run --project src/AuthNode

# tests
dotnet test
dotnet build src/AuthNode -c Release -o out; python tests/blackbox/spec_test.py out/AuthNode.dll

# Docker (native AOT build inside)
docker build -t unisk-auth .
docker run --rm -v auth-data:/data unisk-auth bootstrap-admin   # once
docker run -p 8080:8080 -v auth-data:/data -e AUTH_ROUTER_KEY=... unisk-auth
```

Needs a .NET 10 SDK of 10.0.404 or newer. 10.0.402 ships an ILCompiler with a
known vulnerability, and with warnings treated as errors the restore fails.

To generate a random key in PowerShell 7:
`[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))`

| Variable | Default | Meaning |
|---|---|---|
| `AUTH_ROUTER_KEY` | **required** (32+ chars) | Shared secret the router sends in `X-Router-Key` |
| `AUTH_TOKEN_LIFETIME_SECONDS` | `900` | How long an access token lives |
| `AUTH_DB_PATH` | `data/auth.db` (`/data/auth.db` in Docker) | SQLite file |
| `ASPNETCORE_HTTP_PORTS` | `8080` in Docker | Listen port |

## How the router talks to it

Every `/auth/*` request carries two headers:

| Header | When | What |
|---|---|---|
| `X-Router-Key: <AUTH_ROUTER_KEY>` | always | Proves the request comes from the router. Without it: `401 invalid_router_key` |
| `Authorization: Bearer <token>` | check, filter, admin routes | The **caller's** token, which the router forwards. The Auth node works out which node it belongs to |

The JSON uses snake_case, as in [`uniskv2-interactions.md`](../uniskv2-interactions.md). Every error has the same
OAuth-style shape: `{"error": "invalid_token", "error_description": "..."}`.

## Endpoints

### Registration: `POST /auth/nodes`
The router calls this when a node registers. The new identity starts with **no permissions**.
```jsonc
// request (all fields optional; node_id is generated if omitted)
{ "node_id": "b2-coder", "name": "Code node", "host": "10.0.0.5:9000" }
// 201
{ "node_id": "b2-coder", "client_id": "b2-coder", "client_secret": "sk_...",
  "is_admin": false, "allowed_nodes": [] }
```
The `client_secret` is shown **only here**. The Auth node stores just its
SHA-256 hash, so a lost secret means deleting the node and registering it
again. A duplicate `node_id` gets `409 node_exists`.

### Login: `POST /auth/node/login`
OAuth 2.1 client credentials. The credentials can be sent as HTTP Basic, as a
form, or as JSON:
```jsonc
// request (JSON variant)
{ "grant_type": "client_credentials", "client_id": "b2-coder", "client_secret": "sk_..." }
// 200
{ "access_token": "at_...", "token_type": "Bearer", "expires_in": 900, "node_id": "b2-coder" }
```
`grant_type` is required (`400 invalid_request` without it), and any other
grant type gets `400 unsupported_grant_type`. A wrong or unknown client gets
`401 invalid_client`, plus a `WWW-Authenticate: Basic` challenge if it sent
its credentials as HTTP Basic.

### `POST /auth/permissions/check`
```jsonc
// Authorization: Bearer <caller token>
{ "target_node_id": "b2-coder", "action": "invoke" }   // action: "read" | "invoke"
// 200
{ "allowed": true, "caller_node_id": "a4", "reason": "granted" }
```
A dead token gets `401 invalid_token`. A valid token always gets `200`, with
`allowed` true or false.

### `POST /auth/permissions/filter`
```jsonc
{ "node_ids": ["b2-coder", "b2-research", "ghost"], "action": "read" }
// 200: only the allowed ones, in the order given, without duplicates
{ "allowed_node_ids": ["b2-coder"], "caller_node_id": "a4" }
```

### Admin routes (the caller token must belong to an admin)
| Route | Body | Result |
|---|---|---|
| `GET /auth/nodes` | — | All nodes with `is_admin` and `allowed_nodes` |
| `GET /auth/nodes/{node_id}` | — | One node |
| `PATCH /auth/nodes/{node_id}` | `{"add_allowed_nodes": ["b2-coder"], "remove_allowed_nodes": []}` | The updated node. Unknown targets get `400 unknown_nodes` |
| `POST /auth/nodes/{node_id}/promote` | — | The node, now `is_admin: true` |
| `DELETE /auth/nodes/{node_id}` | — | `204`. Its tokens die **immediately**, and every grant from or to it is removed |

A missing or dead token gets `401`, and a valid token from a non-admin gets
`403 insufficient_scope`. Deleting the last remaining admin is refused with
`409 last_admin`, so you can't lock yourself out.

`GET /healthz` returns `ok` and needs no key, which is useful for Docker health checks.

## The permission rule

All of it lives in `Endpoints.Decide`:

1. An unknown target is **denied**.
2. An admin is **allowed** everything.
3. A node may **read** itself, but not invoke itself.
4. A grant from `PATCH` allows **both** `read` and `invoke` on that target.
   Grants are one-way: A → B doesn't let B reach A.
5. Anything else is **denied**.

## Design choices

- **Why not MongoDB:** the MongoDB C# driver doesn't support Native AOT. Its
  BSON serialization uses reflection, and MongoDB closed the AOT ticket as
  "Won't Fix". SQLite through plain SQL has no reflection at all, and it's
  a single file, which suits a service with three small tables.
- **Tokens:** they're random strings that are looked up in the database, not
  JWTs. That's what makes `DELETE` take effect on the very next call; a JWT
  would stay valid until it expired. Only the router validates tokens, by
  calling `check` and `filter`, so the extra lookup costs nothing.
- **Hashing:** secrets and tokens are 256-bit random values stored as SHA-256
  hashes and compared in constant time. A copied database holds no usable
  credential.
- **The bootstrap admin:** every node starts with zero permissions, and only
  an admin can grant any, so the first admin is created out of band with
  `AuthNode bootstrap-admin [node_id]` (default `admin`). It only works on a
  database with **no admin at all**, generates the secret itself and prints it
  once (alone on stdout, so it can be captured), so it can't be re-run to take
  over a running system. The server itself never creates admins, and there is
  no admin secret in the environment that could be used over the network.
  Running the command needs access to the database volume, which already means
  full control. Log in with that secret, promote a real admin node, then delete
  the bootstrap admin. The old `AUTH_BOOTSTRAP_ADMIN_SECRET` variable makes the
  server refuse to start, so a leftover copy in `.env` gets noticed.

## Not built in (yet)

- **TLS:** the router↔auth link is plain HTTP, so keep the Auth node on the
  internal network (no published port in compose), or put a TLS proxy in front.
- **Per-action grants:** a grant currently covers both `read` and `invoke`.
  The API already takes `action`, so splitting them later only touches
  `Decide` and the `grants` table.
- **Rate limiting on login:** brute-forcing is pointless against 256-bit
  secrets, but a limit would cut down log noise from misbehaving clients.
