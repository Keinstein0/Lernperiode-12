"""Black-box conformance test of the auth node against uniskv2-interactions.md.

Plays the router: launches the real binary, talks to it over HTTP only, and
restarts it to test persistence and the bootstrap admin. Unlike the xUnit
suite (JIT, in-memory), this runs whatever you ship, e.g. the Native AOT build.
Standard library only.

usage:  python tests/blackbox/spec_test.py <AuthNode exe | AuthNode.dll> [port]

  # AOT binary (Linux, or Windows with the VS C++ workload)
  dotnet publish src/AuthNode -c Release -r linux-x64 -o out && python tests/blackbox/spec_test.py out/AuthNode
  # JIT build
  dotnet build src/AuthNode -c Release -o out && python tests/blackbox/spec_test.py out/AuthNode.dll
"""
import base64, json, os, shutil, subprocess, sys, tempfile, time, urllib.request, urllib.error
from concurrent.futures import ThreadPoolExecutor

sys.stdout.reconfigure(encoding="utf-8")  # Windows consoles default to cp1252
if len(sys.argv) < 2: sys.exit(__doc__)

TARGET = sys.argv[1]
PORT = int(sys.argv[2]) if len(sys.argv) > 2 else 18080
BASE = f"http://127.0.0.1:{PORT}"
KEY = "spec-router-key-" + "k" * 32
DBDIR = tempfile.mkdtemp(prefix="authspec-")
CMD = ["dotnet", TARGET] if TARGET.endswith(".dll") else [TARGET]
BASE_ENV = dict(os.environ, AUTH_DB_PATH=os.path.join(DBDIR, "auth.db"))
BASE_ENV.pop("AUTH_BOOTSTRAP_ADMIN_SECRET", None)  # the server refuses to start with it

results = []  # (section, name, ok, detail)
section = "?"

def check(name, cond, detail=""):
    results.append((section, name, bool(cond), detail))
    print(f"  {'PASS' if cond else 'FAIL'}  {name}" + (f"  -- {detail}" if detail and not cond else ""))

# ── process control ────────────────────────────────────────────────────────
proc = None
def start(lifetime=900, extra=None):
    global proc
    env = dict(BASE_ENV, AUTH_ROUTER_KEY=KEY, AUTH_TOKEN_LIFETIME_SECONDS=str(lifetime),
               ASPNETCORE_URLS=BASE, Logging__LogLevel__Default="Warning")
    env.update(extra or {})
    proc = subprocess.Popen(CMD, env=env, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
    for _ in range(100):
        try:
            if req("GET", "/healthz", key=None)[0] == 200: return
        except Exception: pass
        time.sleep(0.1)
    raise RuntimeError("auth node did not start: " + proc.stderr.read().decode(errors="replace")[-2000:])

def stop():
    proc.terminate(); proc.wait(10)

def bootstrap(*args):
    """Runs `AuthNode bootstrap-admin [args]`; returns (exit code, secret from stdout, stderr)."""
    p = subprocess.run(CMD + ["bootstrap-admin", *args], env=BASE_ENV, capture_output=True, text=True, timeout=60)
    return p.returncode, p.stdout.strip() or None, p.stderr

# ── http ───────────────────────────────────────────────────────────────────
def req(method, path, body=None, bearer=None, key=KEY, headers=None, raw=None, ctype="application/json"):
    data = raw if raw is not None else (json.dumps(body).encode() if body is not None else None)
    r = urllib.request.Request(BASE + path, data=data, method=method)
    if data is not None and ctype: r.add_header("Content-Type", ctype)
    if key is not None: r.add_header("X-Router-Key", key)
    if bearer: r.add_header("Authorization", "Bearer " + bearer)
    for k, v in (headers or {}).items(): r.add_header(k, v)
    try:
        with urllib.request.urlopen(r, timeout=10) as resp:
            txt = resp.read().decode()
            return resp.status, (json.loads(txt) if txt and txt[0] in "[{" else txt), dict(resp.headers)
    except urllib.error.HTTPError as e:
        txt = e.read().decode()
        try: j = json.loads(txt)
        except Exception: j = txt
        return e.code, j, dict(e.headers)

def register(node_id=None, **kw):
    body = {"node_id": node_id, **kw} if node_id else kw
    return req("POST", "/auth/nodes", body)

def login(cid, secret):
    s, j, _ = req("POST", "/auth/node/login", {"grant_type": "client_credentials", "client_id": cid, "client_secret": secret})
    return j.get("access_token") if s == 200 else None

def check_perm(tok, target, action):
    return req("POST", "/auth/permissions/check", {"target_node_id": target, "action": action}, bearer=tok)

def allowed(tok, target, action):
    s, j, _ = check_perm(tok, target, action)
    return j.get("allowed") if s == 200 else f"HTTP {s}"

def is_oauth_error(j, code):
    return isinstance(j, dict) and j.get("error") == code and "error_description" in j

# ═══════════════════════════════════════════════════════════════════════════
start()
try:
    # ── First admin: only via the one-time `bootstrap-admin` command
    section = "Bootstrap admin"
    print(f"\n[{section}]")
    check("no admin before bootstrap", login("admin", "sk_anything") is None)
    code, ADMIN_SECRET, err = bootstrap()
    check("bootstrap-admin → exit 0, secret alone on stdout", code == 0 and str(ADMIN_SECRET).startswith("sk_")
          and ADMIN_SECRET not in err, (code, err))
    check("bootstrap secret logs in as admin", login("admin", ADMIN_SECRET) is not None)
    code, out, err = bootstrap("other")
    check("second bootstrap refused (exit 1, no secret)", code == 1 and out is None, (code, err))
    p = subprocess.Popen(CMD, env=dict(BASE_ENV, AUTH_ROUTER_KEY=KEY, AUTH_BOOTSTRAP_ADMIN_SECRET="s" * 40,
                                       ASPNETCORE_URLS=f"http://127.0.0.1:{PORT + 1}"),
                         stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
    try: p.wait(30)
    except subprocess.TimeoutExpired: p.kill()
    check("server refuses to start with the old AUTH_BOOTSTRAP_ADMIN_SECRET", p.returncode not in (0, None), p.returncode)

    # ── Router ↔ Auth: "Router authenticates to Auth Node as trusted proxy/service"
    section = "Router trust"
    print(f"\n[{section}]")
    routes = [("POST", "/auth/node/login"), ("POST", "/auth/permissions/check"), ("POST", "/auth/permissions/filter"),
              ("POST", "/auth/nodes"), ("GET", "/auth/nodes"), ("GET", "/auth/nodes/x"), ("PATCH", "/auth/nodes/x"),
              ("POST", "/auth/nodes/x/promote"), ("DELETE", "/auth/nodes/x")]
    for m, p in routes:
        s1, j1, _ = req(m, p, {}, key=None)
        s2, j2, _ = req(m, p, {}, key="wrong" * 10)
        check(f"{m} {p} rejects missing+wrong router key",
              s1 == 401 and s2 == 401 and is_oauth_error(j1, "invalid_router_key"), f"{s1}/{s2}")
    check("/healthz open without key (for docker healthcheck)", req("GET", "/healthz", key=None)[0] == 200)

    # ── Registration: "creates identity with 0 permissions & one-time secret"
    section = "Registration"
    print(f"\n[{section}]")
    s, a4, h = register("a4", name="Orchestrator A4", host="10.0.0.4:9000")
    check("POST /auth/nodes → 201", s == 201, s)
    check("returns client_id + client_secret", a4.get("client_id") == "a4" and str(a4.get("client_secret", "")).startswith("sk_"))
    check("starts with 0 permissions, not admin", a4.get("allowed_nodes") == [] and a4.get("is_admin") is False)
    check("secret response is Cache-Control: no-store", "no-store" in h.get("Cache-Control", ""))
    s, gen, _ = register()
    check("node_id optional (router may omit it) → generated id", s == 201 and gen.get("node_id", "").startswith("node-"), gen)
    check("duplicate node_id → 409 node_exists", is_oauth_error(register("a4")[1], "node_exists"))
    for bad in ["../etc", "a b", "x" * 65, "-lead", "é"]:
        check(f"invalid node_id {bad[:10]!r} → 400", register(bad)[0] == 400)
    b2c = register("b2-coder", host="10.0.0.5:9000")[1]
    b2r = register("b2-research", host="10.0.0.6:9000")[1]
    s, adm_view, _ = req("GET", "/auth/nodes", bearer=login("admin", ADMIN_SECRET))
    check("secret is one-time: never appears in admin views", "client_secret" not in json.dumps(adm_view) and "sk_" not in json.dumps(adm_view))

    # ── Login: "OAuth 2.1 Client Credentials … short-lived Bearer token"
    section = "Login (OAuth 2.1 client credentials)"
    print(f"\n[{section}]")
    s, j, h = req("POST", "/auth/node/login", {"grant_type": "client_credentials", "client_id": "a4", "client_secret": a4["client_secret"]})
    check("JSON body → 200 token", s == 200 and j.get("token_type") == "Bearer" and j.get("access_token", "").startswith("at_"), j)
    check("expires_in present (short-lived, 900s default)", j.get("expires_in") == 900)
    check("token response Cache-Control: no-store (RFC 6749 §5.1)", "no-store" in h.get("Cache-Control", ""))
    form = f"grant_type=client_credentials&client_id=a4&client_secret={a4['client_secret']}".encode()
    check("form body (client_secret_post) → 200", req("POST", "/auth/node/login", raw=form, ctype="application/x-www-form-urlencoded")[0] == 200)
    basic = base64.b64encode(f"a4:{a4['client_secret']}".encode()).decode()
    s, _, _ = req("POST", "/auth/node/login", raw=b"grant_type=client_credentials", ctype="application/x-www-form-urlencoded",
                  headers={"Authorization": "Basic " + basic})
    check("HTTP Basic (client_secret_basic) → 200", s == 200, s)
    s, j, h = req("POST", "/auth/node/login", {"grant_type": "client_credentials", "client_id": "a4", "client_secret": "sk_wrong"})
    check("wrong secret → 401 invalid_client", s == 401 and is_oauth_error(j, "invalid_client"), (s, j))
    s, j, _ = req("POST", "/auth/node/login", {"grant_type": "client_credentials", "client_id": "ghost", "client_secret": "x"})
    check("unknown client → 401 invalid_client (same as wrong secret)", s == 401 and is_oauth_error(j, "invalid_client"))
    s, j, _ = req("POST", "/auth/node/login", {"grant_type": "password", "client_id": "a4", "client_secret": a4["client_secret"]})
    check("other grant_type → 400 unsupported_grant_type", s == 400 and is_oauth_error(j, "unsupported_grant_type"))
    s, j, h = req("POST", "/auth/node/login", raw=b"grant_type=client_credentials", ctype="application/x-www-form-urlencoded",
                  headers={"Authorization": "Basic " + base64.b64encode(b"a4:nope").decode()})
    check("Basic with bad secret → 401 + WWW-Authenticate (RFC 6749 §5.2 MUST)", s == 401 and "WWW-Authenticate" in h, f"{s}, headers={list(h)}")
    s, j, _ = req("POST", "/auth/node/login", {"client_id": "a4", "client_secret": a4["client_secret"]})
    check("missing grant_type → 400 invalid_request (RFC 6749 §4.4.2: REQUIRED)", s == 400, f"got {s}")
    t1, t2 = login("a4", a4["client_secret"]), login("a4", a4["client_secret"])
    check("each login issues a distinct token", t1 and t2 and t1 != t2)

    A4 = login("a4", a4["client_secret"])
    B2C = login("b2-coder", b2c["client_secret"])
    ADMIN = login("admin", ADMIN_SECRET)

    # ── Default deny: new identity "with 0 permissions"
    section = "Permissions: default deny"
    print(f"\n[{section}]")
    for tool, action in [("get_node", "read"), ("send_task", "invoke"), ("get_task", "read"), ("cancel_task", "invoke")]:
        check(f"{tool}: fresh a4 → b2-coder {action} denied", allowed(A4, "b2-coder", action) is False)
    s, j, _ = req("POST", "/auth/permissions/filter", {"node_ids": ["b2-coder", "b2-research"], "action": "read"}, bearer=A4)
    check("get_nodes: fresh a4 filter → []", s == 200 and j.get("allowed_node_ids") == [], j)

    # ── admin_update_node_permissions → PATCH
    section = "Admin: PATCH grants"
    print(f"\n[{section}]")
    s, j, _ = req("PATCH", "/auth/nodes/a4", {"add_allowed_nodes": ["b2-coder"]}, bearer=ADMIN)
    check("PATCH add → 200, allowed_nodes updated", s == 200 and j.get("allowed_nodes") == ["b2-coder"], j)
    for tool, action in [("get_node", "read"), ("send_task", "invoke"), ("send_task_stream", "invoke"),
                         ("get_task", "read"), ("subscribe_task", "read"), ("cancel_task", "invoke"),
                         ("get_extended_agent_card", "read")]:
        check(f"{tool}: a4 → b2-coder {action} allowed", allowed(A4, "b2-coder", action) is True)
    s, j, _ = check_perm(A4, "b2-coder", "invoke")
    check("check response names caller_node_id + reason", j.get("caller_node_id") == "a4" and j.get("reason") == "granted", j)
    check("still denied on non-granted b2-research", allowed(A4, "b2-research", "invoke") is False)
    check("grants are one-way: b2-coder → a4 denied", allowed(B2C, "a4", "invoke") is False)
    s, j, _ = req("POST", "/auth/permissions/filter",
                  {"node_ids": ["b2-research", "b2-coder", "ghost", "b2-coder", "a4"], "action": "read"}, bearer=A4)
    check("get_nodes filter: order kept, dupes+unknown dropped",
          s == 200 and j.get("allowed_node_ids") == ["b2-coder", "a4"], j)
    s, j, _ = req("POST", "/auth/permissions/filter", {"node_ids": ["b2-coder", "a4"], "action": "invoke"}, bearer=A4)
    check("filter invoke: self excluded (can't invoke self)", j.get("allowed_node_ids") == ["b2-coder"], j)
    s, j, _ = req("PATCH", "/auth/nodes/a4", {"add_allowed_nodes": ["ghost"]}, bearer=ADMIN)
    check("PATCH unknown target → 400 unknown_nodes", s == 400 and is_oauth_error(j, "unknown_nodes"), j)
    check("PATCH self-grant → 400", req("PATCH", "/auth/nodes/a4", {"add_allowed_nodes": ["a4"]}, bearer=ADMIN)[0] == 400)
    check("PATCH unknown node → 404", req("PATCH", "/auth/nodes/ghost", {"add_allowed_nodes": []}, bearer=ADMIN)[0] == 404)
    s, j, _ = req("PATCH", "/auth/nodes/a4", {"add_allowed_nodes": ["b2-research"], "remove_allowed_nodes": ["b2-coder"]}, bearer=ADMIN)
    check("PATCH add+remove in one call", j.get("allowed_nodes") == ["b2-research"], j)
    check("removal takes effect on existing token", allowed(A4, "b2-coder", "invoke") is False)
    req("PATCH", "/auth/nodes/a4", {"add_allowed_nodes": ["b2-coder"]}, bearer=ADMIN)

    # ── Admin role "verified on Auth Node"
    section = "Admin: role enforcement"
    print(f"\n[{section}]")
    for m, p, b in [("GET", "/auth/nodes", None), ("GET", "/auth/nodes/a4", None),
                    ("PATCH", "/auth/nodes/a4", {"add_allowed_nodes": ["b2-research"]}),
                    ("POST", "/auth/nodes/a4/promote", None), ("DELETE", "/auth/nodes/b2-coder", None)]:
        s1, j1, _ = req(m, p, b, bearer=A4)
        s2, j2, _ = req(m, p, b)
        check(f"{m} {p}: non-admin 403 insufficient_scope, no token 401",
              s1 == 403 and is_oauth_error(j1, "insufficient_scope") and s2 == 401, f"{s1}/{s2}")
    s, j, _ = req("GET", "/auth/nodes", bearer=ADMIN)
    ids = [n["node_id"] for n in j] if isinstance(j, list) else []
    check("admin_list_nodes: lists every node with is_admin + allowed_nodes",
          {"admin", "a4", "b2-coder", "b2-research"} <= set(ids) and all("allowed_nodes" in n and "is_admin" in n for n in j), ids)
    s, j, _ = req("GET", "/auth/nodes/a4", bearer=ADMIN)
    check("admin_get_node: one node", s == 200 and j.get("node_id") == "a4" and j.get("host") == "10.0.0.4:9000", j)
    check("admin_get_node unknown → 404 node_not_found", is_oauth_error(req("GET", "/auth/nodes/ghost", bearer=ADMIN)[1], "node_not_found"))
    check("admin may check anything (incl. unknown target denied)",
          allowed(ADMIN, "b2-research", "invoke") is True and allowed(ADMIN, "ghost", "read") is False)

    # ── admin_promote_node
    section = "Admin: promote"
    print(f"\n[{section}]")
    s, j, _ = req("POST", "/auth/nodes/b2-research/promote", bearer=ADMIN)
    check("promote → 200 is_admin true", s == 200 and j.get("is_admin") is True, j)
    B2R = login("b2-research", b2r["client_secret"])
    check("promoted node's token now passes admin routes", req("GET", "/auth/nodes", bearer=B2R)[0] == 200)
    check("promote unknown → 404", req("POST", "/auth/nodes/ghost/promote", bearer=ADMIN)[0] == 404)

    # ── admin_delete_node: "Revoke node identity and kill access immediately"
    section = "Admin: delete / revoke"
    print(f"\n[{section}]")
    check("before delete: b2-coder token works", allowed(B2C, "b2-coder", "read") is True)
    s, _, _ = req("DELETE", "/auth/nodes/b2-coder", bearer=ADMIN)
    check("DELETE → 204", s == 204, s)
    s, j, _ = check_perm(B2C, "a4", "read")
    check("deleted node's live token → 401 invalid_token on next call", s == 401 and is_oauth_error(j, "invalid_token"), s)
    check("deleted node can't log in again", login("b2-coder", b2c["client_secret"]) is None)
    s, j, _ = req("GET", "/auth/nodes/a4", bearer=ADMIN)
    check("grants TO deleted node removed from others", "b2-coder" not in j.get("allowed_nodes", []), j)
    check("a4 → b2-coder now denied (unknown target)", allowed(A4, "b2-coder", "invoke") is False)
    s, re, _ = register("b2-coder")
    check("id can be re-registered after delete, fresh 0 permissions", s == 201 and re.get("allowed_nodes") == [])
    check("…and old grants don't resurrect", allowed(A4, "b2-coder", "invoke") is False)
    # last admin: delete b2-research admin, then try to delete bootstrap admin
    req("DELETE", "/auth/nodes/b2-research", bearer=ADMIN)
    s, j, _ = req("DELETE", "/auth/nodes/admin", bearer=ADMIN)
    check("deleting the last admin → 409 last_admin", s == 409 and is_oauth_error(j, "last_admin"), (s, j))

    # ── Robustness of what the router forwards
    section = "Robustness"
    print(f"\n[{section}]")
    check("bad bearer → 401 invalid_token", req("POST", "/auth/permissions/check", {"target_node_id": "a4", "action": "read"}, bearer="at_nope")[0] == 401)
    check("non-Bearer scheme → 401", req("POST", "/auth/permissions/check", {"target_node_id": "a4", "action": "read"},
                                          headers={"Authorization": "Basic Zm9vOmJhcg=="})[0] == 401)
    check("action 'write' → 400", check_perm(A4, "a4", "write")[0] == 400)
    check("missing target_node_id → 400", req("POST", "/auth/permissions/check", {"action": "read"}, bearer=A4)[0] == 400)
    check("malformed JSON → 400", req("POST", "/auth/permissions/check", raw=b"{nope", bearer=A4)[0] == 400)
    check("wrong JSON type → 400", req("POST", "/auth/permissions/check", {"target_node_id": 5, "action": "read"}, bearer=A4)[0] == 400)
    check("text/plain body → 415", req("POST", "/auth/permissions/check", raw=b"{}", ctype="text/plain", bearer=A4)[0] == 415)
    s, j, _ = req("POST", "/auth/permissions/filter", {"node_ids": ["a4", None], "action": "read"}, bearer=A4)
    check("filter with null id → 4xx, not 500", 400 <= s < 500, f"got {s} {j}")
    s, j, _ = req("PATCH", "/auth/nodes/a4", {"add_allowed_nodes": [None]}, bearer=ADMIN)
    check("PATCH with null id → 4xx, not 500", 400 <= s < 500, f"got {s} {j}")
    s, j, _ = req("PATCH", "/auth/nodes/a4", {"remove_allowed_nodes": [None]}, bearer=ADMIN)
    check("PATCH remove null id → 4xx, not 500", 400 <= s < 500, f"got {s} {j}")
    check("filter > 1000 ids → 400", req("POST", "/auth/permissions/filter", {"node_ids": [f"n{i}" for i in range(1001)], "action": "read"}, bearer=A4)[0] == 400)
    check("SQL-ish id is just data", allowed(A4, "a4' OR '1'='1", "read") is False)

    # ── Concurrency: router fans out logins/checks in parallel
    section = "Concurrency (SQLite WAL)"
    print(f"\n[{section}]")
    with ThreadPoolExecutor(32) as ex:
        regs = list(ex.map(lambda i: register(f"par-{i}")[0], range(64)))
        checks = list(ex.map(lambda i: allowed(A4, "a4", "read"), range(200)))
    check("64 parallel registrations all 201", regs.count(201) == 64, {c: regs.count(c) for c in set(regs)})
    check("200 parallel checks all succeed", checks.count(True) == 200, {str(c): checks.count(c) for c in set(map(str, checks))})

    # ── Persistence & bootstrap across restart
    section = "Restart"
    print(f"\n[{section}]")
    s, j, _ = register("admin2")
    admin2_secret = j["client_secret"]
    req("POST", "/auth/nodes/admin2/promote", bearer=ADMIN)
    check("bootstrap admin deletable once another admin exists", req("DELETE", "/auth/nodes/admin", bearer=ADMIN)[0] == 204)
    stop(); start()
    A4b = login("a4", a4["client_secret"])
    check("identities survive restart (a4 can log in)", A4b is not None)
    check("old token survives restart (DB-backed)", allowed(A4, "a4", "read") is True)
    check("grants survive restart", allowed(A4b, "b2-research", "invoke") is not None)
    check("deleted bootstrap admin stays deleted after restart", login("admin", ADMIN_SECRET) is None)
    check("bootstrap-admin can't be re-run while admin2 exists", bootstrap()[0] == 1)
    check("admin2 still admin after restart", req("GET", "/auth/nodes", bearer=login("admin2", admin2_secret))[0] == 200)
    stop()

    # ── Token lifetime ("short-lived")
    section = "Token expiry"
    print(f"\n[{section}]")
    start(lifetime=1)
    t = login("a4", a4["client_secret"])
    check("token valid right after login", allowed(t, "a4", "read") is True)
    time.sleep(2.2)
    check("token rejected after lifetime → 401", check_perm(t, "a4", "read")[0] == 401)
finally:
    if proc and proc.poll() is None: stop()
    shutil.rmtree(DBDIR, ignore_errors=True)

fails = [r for r in results if not r[2]]
print(f"\n{len(results) - len(fails)}/{len(results)} passed")
for sec, name, _, d in fails: print(f"  FAIL [{sec}] {name}  -- {d}")
sys.exit(1 if fails else 0)
