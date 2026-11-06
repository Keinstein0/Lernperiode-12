"""
UniSK Node — minimal Claude Code version
========================================

One file, eight sections. Read top to bottom:

  1. CONFIG      who am I, where is the router, limits
  2. MODELS      the JSON shapes the router sends and receives
  3. STATE       the single task slot (idle/busy/done/error), saved to disk
  4. GIT         clone, branch, commit, push
  5. WORKERS     Claude Code (real) and a fake worker (for testing)
  6. RUNNER      runs one ticket from start to finish, never leaves us stuck
  7. BACKGROUND  registration + re-registration, ack timeout
  8. HTTP API    /health /status /ticket /ack /reports/{task-id}

Run:  python node.py          (see README.md for env vars)
"""

from __future__ import annotations

import asyncio
import json
import logging
import os
import shutil
import time
import uuid
from contextlib import asynccontextmanager
from datetime import datetime, timezone
from pathlib import Path

import httpx
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, ConfigDict, Field

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
logging.getLogger("httpx").setLevel(logging.WARNING)  # quiet per-request logs
log = logging.getLogger("node")


# ─────────────────────────────────────────────────────────────────────────────
# 1. CONFIG
# ─────────────────────────────────────────────────────────────────────────────

DATA_DIR = Path(os.getenv("DATA_DIR", "./data"))         # state, reports, logs
WORK_DIR = Path(os.getenv("WORKSPACE_DIR", "./work"))    # git checkouts
for d in (DATA_DIR, WORK_DIR, DATA_DIR / "reports", DATA_DIR / "transcripts"):
    d.mkdir(parents=True, exist_ok=True)


def _load_node_id() -> str:
    """The id must stay the same across restarts, so generate it once and keep it."""
    if os.getenv("NODE_ID"):
        return os.environ["NODE_ID"]
    f = DATA_DIR / "node-id"
    if not f.exists():
        f.write_text(uuid.uuid4().hex[:18])
    return f.read_text().strip()


NODE_PORT = int(os.getenv("NODE_PORT", "5556"))

# The identity block. Sent on registration, returned on /status, and every
# request from the router must carry exactly these four values.
IDENTITY = {
    "name": os.getenv("NODE_NAME", "my-node"),
    "id": _load_node_id(),
    "host": os.getenv("NODE_HOST", f"127.0.0.1:{NODE_PORT}"),
    "type": os.getenv("NODE_TYPE", "unisk:claude-coder-0.1"),
}

ROUTER_URL = os.getenv("ROUTER_URL", "http://127.0.0.1:2048/")
WORKER = os.getenv("WORKER", "claude")                    # "claude" or "fake"
CLAUDE_MODEL = os.getenv("CLAUDE_MODEL") or None          # None = CLI default
MAX_TURNS = int(os.getenv("MAX_TURNS", "50"))
TASK_TIMEOUT = float(os.getenv("TASK_TIMEOUT", "3600"))   # seconds per ticket
ACK_TIMEOUT = float(os.getenv("ACK_TIMEOUT", "1800"))     # self-release after done
REREGISTER_AFTER = float(os.getenv("REREGISTER_AFTER", "120"))


# ─────────────────────────────────────────────────────────────────────────────
# 2. MODELS  (Pydantic validates incoming JSON; kebab-case like the spec)
# ─────────────────────────────────────────────────────────────────────────────

class Kebab(BaseModel):
    """Python uses git_repo_url, the JSON uses git-repo-url. This maps them."""
    model_config = ConfigDict(
        alias_generator=lambda s: s.replace("_", "-"), populate_by_name=True
    )


# task-ids end up in file paths and branch names, so only allow safe characters
TASK_ID = Field(pattern=r"^[A-Za-z0-9_-][A-Za-z0-9._-]{0,63}$")


class Identity(Kebab):
    name: str
    id: str
    host: str
    type: str


class Ticket(Kebab):
    created_at: str | None = None
    created_by: str | None = None
    git_repo_url: str
    git_branch_id: str | None = None
    files_of_interest: list[str] = []
    description: str


class Credentials(Kebab):
    """Passed by the router with each ticket. Kept in memory only."""
    anthropic_api_key: str | None = None
    git_token: str | None = None
    git_username: str = "x-access-token"


class TicketRequest(Identity):
    task_id: str = TASK_ID
    ticket: Ticket
    credentials: Credentials = Field(default_factory=Credentials)


class AckRequest(Identity):
    task_id: str = TASK_ID


def make_report(repo_url: str, branch: str, files: list[str], description: str) -> dict:
    return {
        "finished-at": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "finished-by": IDENTITY["id"],
        "git-repo-url": repo_url,
        "git-branch-id": branch,
        "files-of-interest": files,
        "description": description,
    }


# ─────────────────────────────────────────────────────────────────────────────
# 3. STATE  (one slot; every change is written to disk immediately)
# ─────────────────────────────────────────────────────────────────────────────

class Slot:
    def __init__(self) -> None:
        self.file = DATA_DIR / "state.json"
        self.lock = asyncio.Lock()     # only one request may change state at a time
        self.status = "idle"           # idle | busy | done | error
        self.task_id: str | None = None
        self.ticket: dict | None = None    # stored WITHOUT credentials
        self.report: dict | None = None
        self.finished_at = 0.0
        self._load()

    def _load(self) -> None:
        if not self.file.exists():
            return
        d = json.loads(self.file.read_text())
        self.status, self.task_id = d["status"], d["task_id"]
        self.ticket, self.report = d["ticket"], d["report"]
        self.finished_at = d["finished_at"]
        if self.status == "busy":
            # We crashed or were restarted mid-task. Don't pretend we're still
            # working; turn it into an error the router can collect.
            log.warning("Restarted while busy with %s; marking as error", self.task_id)
            branch = self.ticket.get("git-branch-id") or f"node/{IDENTITY['id']}/{self.task_id}"
            self.finish("error", make_report(
                self.ticket["git-repo-url"], branch, [],
                "Node restarted during the task. Work was not completed or pushed."))

    def _save(self) -> None:
        tmp = self.file.with_suffix(".tmp")
        tmp.write_text(json.dumps({
            "status": self.status, "task_id": self.task_id, "ticket": self.ticket,
            "report": self.report, "finished_at": self.finished_at}, indent=2))
        tmp.replace(self.file)   # atomic: never leaves a half-written file

    def start(self, task_id: str, ticket: dict) -> None:
        self.status, self.task_id, self.ticket, self.report = "busy", task_id, ticket, None
        self._save()

    def finish(self, status: str, report: dict) -> None:
        self.status, self.report, self.finished_at = status, report, time.time()
        self._save()

    def release(self) -> None:
        """Ack received (or ack timeout): archive the report, go back to idle."""
        if self.report:
            (DATA_DIR / "reports" / f"{self.task_id}.json").write_text(
                json.dumps({"status": self.status, "report": self.report}, indent=2))
        self.status, self.task_id, self.ticket, self.report = "idle", None, None, None
        self._save()


slot = Slot()


# ─────────────────────────────────────────────────────────────────────────────
# 4. GIT  (the node owns git; Claude only edits files)
# ─────────────────────────────────────────────────────────────────────────────

# Git asks this tiny script for username/password. It reads them from env vars,
# so the token never gets written into .git/config or appears in a URL.
ASKPASS = DATA_DIR / "askpass.sh"
ASKPASS.write_text(
    '#!/bin/sh\ncase "$1" in\n  Username*) echo "$GIT_USERNAME" ;;\n  *) echo "$GIT_TOKEN" ;;\nesac\n')
ASKPASS.chmod(0o700)


class GitError(Exception):
    pass


def git_env(creds: Credentials) -> dict:
    env = dict(os.environ, GIT_TERMINAL_PROMPT="0", GIT_ASKPASS=str(ASKPASS.resolve()),
               GIT_AUTHOR_NAME=IDENTITY["name"], GIT_COMMITTER_NAME=IDENTITY["name"],
               GIT_AUTHOR_EMAIL=f"{IDENTITY['id']}@unisk.local",
               GIT_COMMITTER_EMAIL=f"{IDENTITY['id']}@unisk.local")
    if creds.git_token:
        env.update(GIT_USERNAME=creds.git_username, GIT_TOKEN=creds.git_token)
    return env


async def git(*args: str, cwd: Path | None = None, env: dict | None = None) -> str:
    proc = await asyncio.create_subprocess_exec(
        "git", *args, cwd=cwd, env=env,
        stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.PIPE)
    out, err = await proc.communicate()
    if proc.returncode != 0:
        raise GitError(f"git {args[0]} failed: {err.decode().strip()[-400:]}")
    return out.decode()


def normalize_url(url: str) -> str:
    """The spec writes 'github.com/user/repo'; git needs 'https://...'."""
    if "://" in url or url.startswith(("/", ".", "git@")):
        return url
    return "https://" + url


async def prepare_workspace(task_id: str, ticket: Ticket, branch: str, env: dict) -> tuple[Path, str]:
    """Clone the repo and get onto the working branch. Returns (path, base commit)."""
    path = WORK_DIR / task_id
    shutil.rmtree(path, ignore_errors=True)
    url = normalize_url(ticket.git_repo_url)
    if ticket.git_branch_id:
        # Ticket names a branch: that's our working branch (fails if it doesn't exist)
        await git("clone", "--branch", branch, url, str(path), env=env)
    else:
        # No branch given: clone the default branch and make our own
        await git("clone", url, str(path), env=env)
        await git("checkout", "-b", branch, cwd=path, env=env)
    base = (await git("rev-parse", "HEAD", cwd=path, env=env)).strip()
    return path, base


async def commit_and_push(path: Path, branch: str, base: str, task_id: str,
                          summary: str, env: dict) -> list[str]:
    """Commit whatever the worker changed, push it, return the changed files."""
    await git("add", "-A", cwd=path, env=env)
    if (await git("diff", "--cached", "--name-only", cwd=path, env=env)).strip():
        title = (summary.strip().splitlines() or ["node work"])[0][:72]
        await git("commit", "-m", f"[{task_id}] {title}", cwd=path, env=env)
    changed = (await git("diff", "--name-only", base, "HEAD", cwd=path, env=env)).splitlines()
    if changed:
        await git("push", "origin", branch, cwd=path, env=env)
    return changed


# ─────────────────────────────────────────────────────────────────────────────
# 5. WORKERS  (same signature, so they're interchangeable)
#    in:  task_id, ticket, workspace path, branch, credentials
#    out: {"success": bool, "summary": str, "highlighted_files": [str]}
# ─────────────────────────────────────────────────────────────────────────────

NODE_RULES = """
You are running as a UniSK node worker. You are completing exactly one ticket,
unattended; nobody can answer questions, so make reasonable decisions yourself.
- Work only inside the current repository.
- Do NOT run git commit, push, checkout, switch, reset, rebase or branch. The node
  commits and pushes your changes for you after you finish.
- Start with the "files of interest" if the ticket lists any.
- When finished, return success=false if you could not complete the ticket, and
  explain why in the summary.
"""

RESULT_SCHEMA = {
    "type": "object",
    "properties": {
        "success": {"type": "boolean"},
        "summary": {"type": "string", "description": "What you changed and why, 1-5 sentences"},
        "highlighted_files": {"type": "array", "items": {"type": "string"}},
    },
    "required": ["success", "summary", "highlighted_files"],
}


def build_prompt(task_id: str, ticket: Ticket, branch: str) -> str:
    files = "\n".join(f"- {f}" for f in ticket.files_of_interest) or "(none)"
    return (f"Ticket {task_id} from {ticket.created_by or 'unknown'}\n"
            f"Repository: {ticket.git_repo_url}  (branch: {branch})\n\n"
            f"Task:\n{ticket.description}\n\nFiles of interest:\n{files}\n")


async def claude_worker(task_id: str, ticket: Ticket, path: Path, branch: str,
                        creds: Credentials) -> dict:
    # Imported here so the fake worker works without the SDK installed
    from claude_agent_sdk import ClaudeAgentOptions, ResultMessage, query

    options = ClaudeAgentOptions(
        cwd=str(path),
        # Keep Claude Code's normal coding prompt, add our rules on top
        system_prompt={"type": "preset", "preset": "claude_code", "append": NODE_RULES},
        permission_mode="acceptEdits",          # file edits without asking
        allowed_tools=["Read", "Write", "Edit", "Glob", "Grep", "Bash"],  # auto-approved
        disallowed_tools=[f"Bash(git {c}:*)" for c in
                          ("push", "commit", "checkout", "switch", "reset", "rebase", "branch")],
        setting_sources=["project"],            # respect the repo's CLAUDE.md, ignore host settings
        max_turns=MAX_TURNS,
        model=CLAUDE_MODEL,
        env={"ANTHROPIC_API_KEY": creds.anthropic_api_key} if creds.anthropic_api_key else {},
        output_format={"type": "json_schema", "schema": RESULT_SCHEMA},
    )

    result = None
    transcript = DATA_DIR / "transcripts" / f"{task_id}.log"
    with transcript.open("w") as f:
        async for msg in query(prompt=build_prompt(task_id, ticket, branch), options=options):
            f.write(f"{type(msg).__name__}: {msg!r}\n")   # full log for debugging
            if isinstance(msg, ResultMessage):
                result = msg

    if result is None:
        raise RuntimeError("Claude Code ended without a result")
    if result.is_error:
        raise RuntimeError(f"Claude Code stopped ({result.subtype}): {result.result or result.errors}")
    if isinstance(result.structured_output, dict):
        return result.structured_output
    # Fallback if structured output was missing: use the final text
    return {"success": True, "summary": result.result or "Done.", "highlighted_files": []}


async def fake_worker(task_id: str, ticket: Ticket, path: Path, branch: str,
                      creds: Credentials) -> dict:
    """Pretends to work: waits, then writes one file. For testing the protocol."""
    await asyncio.sleep(float(os.getenv("FAKE_DELAY", "3")))
    (path / "NODE_WAS_HERE.md").write_text(f"# Task {task_id}\n\n{ticket.description}\n")
    return {"success": True,
            "summary": f"Fake worker wrote NODE_WAS_HERE.md for: {ticket.description}",
            "highlighted_files": ["NODE_WAS_HERE.md"]}


WORKERS = {"claude": claude_worker, "fake": fake_worker}


# ─────────────────────────────────────────────────────────────────────────────
# 6. RUNNER  (whatever happens, the slot ends in done or error)
# ─────────────────────────────────────────────────────────────────────────────

def redact(text: str, creds: Credentials) -> str:
    for secret in (creds.git_token, creds.anthropic_api_key):
        if secret:
            text = text.replace(secret, "***")
    return text


async def run_task(task_id: str, ticket: Ticket, creds: Credentials) -> None:
    branch = ticket.git_branch_id or f"node/{IDENTITY['id']}/{task_id}"
    env = git_env(creds)
    try:
        async with asyncio.timeout(TASK_TIMEOUT):
            path, base = await prepare_workspace(task_id, ticket, branch, env)
            out = await WORKERS[WORKER](task_id, ticket, path, branch, creds)
            changed = await commit_and_push(path, branch, base, task_id, out["summary"], env)
        # Prefer the files Claude highlighted, but only ones that really changed
        files = [f for f in out.get("highlighted_files", []) if f in changed] or changed[:20]
        summary = out["summary"] if changed or not out["success"] else f"No changes needed. {out['summary']}"
        status = "done" if out["success"] else "error"
        report = make_report(ticket.git_repo_url, branch, files, redact(summary, creds))
    except TimeoutError:
        status = "error"
        report = make_report(ticket.git_repo_url, branch, [],
                             f"Task timed out after {TASK_TIMEOUT:.0f}s; nothing was pushed.")
    except Exception as e:  # noqa: BLE001 — every failure must become a report
        log.exception("Task %s failed", task_id)
        status = "error"
        report = make_report(ticket.git_repo_url, branch, [], redact(f"Node error: {e}", creds))
    finally:
        shutil.rmtree(WORK_DIR / task_id, ignore_errors=True)

    async with slot.lock:
        slot.finish(status, report)
    log.info("Task %s finished: %s", task_id, status)


# ─────────────────────────────────────────────────────────────────────────────
# 7. BACKGROUND LOOPS
# ─────────────────────────────────────────────────────────────────────────────

last_poll = time.time()   # updated on every GET /status


async def registrar_loop() -> None:
    """Register on startup (retrying with backoff). If the router stops
    polling us, assume it restarted and forgot us, and register again."""
    global last_poll
    registered, delay = False, 1.0
    async with httpx.AsyncClient(timeout=5) as client:
        while True:
            if registered and time.time() - last_poll > REREGISTER_AFTER:
                log.warning("No status poll for %ss; re-registering", int(REREGISTER_AFTER))
                registered = False
            if not registered:
                try:
                    r = await client.post(ROUTER_URL, json=IDENTITY)
                    r.raise_for_status()
                    registered, delay, last_poll = True, 1.0, time.time()
                    log.info("Registered with router at %s", ROUTER_URL)
                except Exception as e:  # noqa: BLE001
                    log.warning("Registration failed (%s); retrying in %.0fs", e, delay)
                    await asyncio.sleep(delay)
                    delay = min(delay * 2, 60)
                    continue
            await asyncio.sleep(5)


async def ack_timeout_loop() -> None:
    """Safety net: if nobody acks a finished task, release ourselves anyway."""
    while True:
        await asyncio.sleep(10)
        async with slot.lock:
            if slot.status in ("done", "error") and time.time() - slot.finished_at > ACK_TIMEOUT:
                log.warning("No ack for %s after %ss; self-releasing", slot.task_id, int(ACK_TIMEOUT))
                slot.release()


# ─────────────────────────────────────────────────────────────────────────────
# 8. HTTP API
# ─────────────────────────────────────────────────────────────────────────────

running: set[asyncio.Task] = set()   # keep references so tasks aren't garbage-collected


@asynccontextmanager
async def lifespan(app: FastAPI):
    loops = [asyncio.create_task(registrar_loop()), asyncio.create_task(ack_timeout_loop())]
    log.info("Node %s (%s) up on port %s, worker=%s", IDENTITY["name"], IDENTITY["id"], NODE_PORT, WORKER)
    yield
    for t in loops:
        t.cancel()


app = FastAPI(title="UniSK Node", lifespan=lifespan)


def check_identity(req: Identity) -> None:
    if req.model_dump(include={"name", "id", "host", "type"}) != IDENTITY:
        raise HTTPException(403, "identity mismatch")


@app.get("/health")
async def health():
    return {"ok": True}


@app.get("/status")
async def status():
    global last_poll
    last_poll = time.time()
    return {**IDENTITY, "status": slot.status, "task-id": slot.task_id,
            "report": slot.report if slot.status in ("done", "error") else None}


@app.post("/ticket", status_code=202)
async def ticket(req: TicketRequest):
    check_identity(req)
    async with slot.lock:
        if slot.status != "idle":
            raise HTTPException(409, {"reason": slot.status, "current-task-id": slot.task_id})
        slot.start(req.task_id, req.ticket.model_dump(by_alias=True))
    t = asyncio.create_task(run_task(req.task_id, req.ticket, req.credentials))
    running.add(t)
    t.add_done_callback(running.discard)
    log.info("Accepted task %s", req.task_id)
    return {"task-id
