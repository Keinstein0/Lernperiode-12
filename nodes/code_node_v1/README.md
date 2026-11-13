# UniSK Node: minimal Claude Code version

A working, minimal node: about 500 lines in one file, much of it comments. It speaks the UniSK protocol and uses Claude Code to do the work. This README explains every part of it and every interaction it has.

## What's in the box

| File | What it is |
|---|---|
| `node.py` | The node. Everything is in one file, in 8 numbered sections. |
| `fake_router.py` | A pretend router that runs through every interaction once and prints each step. Use it for testing. |
| `requirements.txt` | Python dependencies. |
| `UniSK-node.postman_collection.json` | Every node request, ready to import into Postman. |

---

## Quick start

```bash
pip install -r requirements.txt     # Python 3.11+ and git required
```

**1. Test the protocol without Claude (no API key needed).** Use two terminals:

```bash
# terminal 1: pretend router (it holds the keys and sends them with the ticket)
export GIT_TOKEN=ghp_xxx
python fake_router.py --repo github.com/YOU/REPO --desc "test" --show-rejections

# terminal 2: node with the fake worker (needs no keys of its own)
WORKER=fake python node.py
```

The fake worker writes a `NODE_WAS_HERE.md` file and pushes it to a new branch `node/<node-id>/<task-id>`.

**2. Real run with Claude Code.** Same as above, minus `WORKER=fake`, and with `ANTHROPIC_API_KEY` set in the router's terminal:

```bash
# terminal 1
export GIT_TOKEN=ghp_xxx ANTHROPIC_API_KEY=sk-ant-xxx
python fake_router.py --repo github.com/YOU/REPO --desc "Add a docstring to every function in main.py" --files main.py

# terminal 2
python node.py
```

**3. Test from Postman (Windows, PowerShell).** Import `UniSK-node.postman_collection.json`, then start the node:

```powershell
pip install -r requirements.txt
$env:NODE_ID = "postman-node"     # must match the collection's {{id}}
$env:WORKER  = "fake"             # drop this line to use Claude Code
python node.py
```

In Postman, set `repo` and `gitToken` in the collection's variables (and `anthropicKey` for the real worker). Then run requests 1 → 4: health, status, send ticket, then status again until it says done, then report by task-id.

There's no router running in this setup, so the node logs "Registration failed … retrying" every minute. That's harmless.

### Settings (environment variables)

| Variable | Default | Meaning |
|---|---|---|
| `NODE_NAME` | `my-node` | Display name |
| `NODE_ID` | generated once, saved in `data/node-id` | Unique id |
| `NODE_HOST` | `127.0.0.1:5556` | **The address the router uses to reach this node.** Set it to the LAN IP, e.g. `192.168.0.25:5556` |
| `NODE_TYPE` | `unisk:claude-coder-0.1` | Image-style type tag |
| `NODE_PORT` | `5556` | Port the node listens on |
| `ROUTER_URL` | `http://127.0.0.1:2048/` | Where to register |
| `GIT_TOKEN` | (none) | Git token used when a ticket brings no `credentials.git-token`. Needed to push, even to public repos |
| `WORKER` | `claude` | `claude` or `fake` |
| `CLAUDE_MODEL` | Claude Code's default | Model to use |
| `MAX_TURNS` | `50` | Maximum number of agent turns per ticket |
| `TASK_TIMEOUT` | `3600` | Seconds before a ticket is killed |
| `REREGISTER_AFTER` | `120` | Seconds without a poll before the node registers again |
| `DATA_DIR` / `WORKSPACE_DIR` | `./data` / `./work` | Where state and checkouts live |

---

## The big picture

```
            Router (LAN)
   ┌────────────┼──────────────────────────────┐
   │ register   │ GET /status   POST /ticket   │ GET /reports/{id}
   ▼            ▼                              ▼
┌─────────────────────────────────────────────────────┐
│ NODE SHELL (Python, FastAPI)                        │
│                                                     │
│  HTTP API ──► State slot (idle/busy/done/error)     │
│                   │                                 │
│                   ▼                                 │
│               Runner ──► Git: clone + branch        │
│                   │                                 │
│                   ├────► Worker: Claude Code edits  │
│                   │                                 │
│                   └────► Git: commit + push         │
│                                                     │
│  Background: registrar loop                         │
└─────────────────────────────────────────────────────┘
```

There are two layers:

- **The shell** is ordinary Python. It handles the protocol, the state, git and safety. It contains no AI.
- **The worker** is Claude Code. It only edits files inside a folder the shell prepared for it.

That split is the most important idea in the design. The router only ever talks to the shell, so you could swap Claude Code for Pi or a human and the router would never notice.

---

## The components (in the same order as `node.py`)

### 1. CONFIG

Reads environment variables and builds the node's **identity**: four values (`name`, `id`, `host`, `type`) that identify the node everywhere.

The `id` is generated once and saved to `data/node-id`. That way the node keeps the same id after a restart, and the router doesn't see a "new" node.

### 2. MODELS

Pydantic classes that describe the JSON the router sends: `Identity`, `Ticket`, `Credentials` and `TicketRequest`.

- **Why:** FastAPI uses them to check every incoming request automatically. A malformed ticket is rejected with `422` before any of our code runs.
- **The `Kebab` base class** translates between Python names (`git_repo_url`) and the spec's JSON names (`git-repo-url`).
- **`TASK_ID` pattern:** a task-id becomes a folder name and part of a branch name, so it may only contain letters, numbers, `-`, `_` and `.`. Without this, a task-id like `../../etc` could write outside the data folder.
- **`Credentials`** come with each ticket from the router. If a ticket has no git token, the node falls back to its own `GIT_TOKEN` environment variable.
- **`make_report()`** builds the report in exactly the spec's shape.

### 3. STATE (the `Slot` class)

The node can do one thing at a time, so its whole state is a single "slot":

```
status:   idle | busy | done | error
task_id:  which ticket
ticket:   the ticket (WITHOUT credentials)
report:   filled when done/error
```

**There is no ack.** A finished node (`done` or `error`) keeps showing its report on `/status` and accepts the next ticket whenever one arrives. Every report is also saved to `data/reports/<task-id>.json` the moment the task finishes, so nothing is lost when the next ticket replaces it.

Three rules make it reliable:

1. **One lock.** `slot.lock` makes sure two requests can never change the state at the same moment. Without it, two tickets arriving at once could both see `idle` and both be accepted.
2. **Saved on every change.** Each transition is written to `data/state.json`. It's written to a temp file first and then swapped in, so a crash can't leave a half-written file.
3. **Crash recovery.** On startup, if the saved state says `busy`, the node was killed mid-task. It doesn't pretend to still be working. It switches to `error` with a "node restarted" report, so the router finds out.

Two methods are the only ways state changes:
- `start()`: idle, done or error → busy
- `finish()`: busy → done or error, and saves the report to `data/reports/<task-id>.json`

`accepting` is the single rule for new tickets: anything except `busy`.

### 4. GIT

The node does all the git work itself. Claude only edits files. This keeps commits and pushes predictable.

- **`git_env()`:** gives git the token as a login header through environment variables (`GIT_CONFIG_*`), the same trick GitHub's own `actions/checkout` uses. The token is never in the repo URL, never written to `.git/config`, and no helper script is needed, so it works on Windows too.
- **`remove_dir()`:** deletes a finished checkout. Git marks some of its files read-only, which makes a plain delete fail on Windows, so this clears that flag first.
- **`normalize_url()`:** the spec writes `github.com/user/repo`, but git needs `https://github.com/user/repo`. Local paths are left alone (handy for testing).
- **`prepare_workspace()`:** clones into `work/<task-id>/`.
  - If the ticket has `git-branch-id`, the node clones that branch, which becomes the working branch. If the branch doesn't exist, that's an error.
  - Otherwise the node clones the default branch and creates `node/<node-id>/<task-id>`.
  - It records the starting commit (`base`), so it knows later what changed.
- **`commit_and_push()`:** stages everything, commits as `[<task-id>] <first line of summary>`, lists the files that changed since `base`, and pushes. If nothing changed, it doesn't push.

### 5. WORKERS

Both workers have the same signature: they take the ticket and the folder, and return `{success, summary, highlighted_files}`. The runner doesn't care which one it's calling.

**`claude_worker`** runs Claude Code through the Claude Agent SDK. These are the important settings:

| Setting | What it does |
|---|---|
| `cwd=path` | Claude works inside the task's checkout |
| `system_prompt` preset + `append` | Keeps Claude Code's normal coding instructions and adds the node rules (`NODE_RULES`) |
| `permission_mode="acceptEdits"` | Claude can edit files without asking (nobody is there to approve) |
| `allowed_tools` | Read, Write, Edit, Glob, Grep and Bash are approved automatically |
| `disallowed_tools` | Blocks `git push`, `commit`, `checkout` and so on, because the node owns git |
| `setting_sources=["project"]` | Follows the repo's `CLAUDE.md`, but ignores settings on the host machine |
| `env` | The API key from the ticket's credentials, for this run only |
| `output_format` | Makes Claude finish with JSON that matches `RESULT_SCHEMA`, so no text parsing is needed |

Every message Claude produces is logged to `data/transcripts/<task-id>.log`. That's where to look when a task goes wrong.

**`fake_worker`** waits a few seconds and writes one file. It lets you test the whole protocol and git flow without an API key.

### 6. RUNNER (`run_task`)

This runs one ticket from start to finish, as a background task, so `/ticket` can answer immediately. Its one promise: **whatever happens, the slot ends in `done` or `error`, never stuck in `busy`.**

```
try (with TASK_TIMEOUT):
    prepare workspace → run worker → commit & push
    build report → done (or error if worker said success=false)
except timeout   → error report "timed out"
except anything  → error report "Node error: ..."
finally          → delete the checkout folder
then             → slot.finish(status, report)
```

`files-of-interest` in the report uses the files Claude highlighted, but only those that actually changed. If none qualify, it falls back to all changed files (at most 20). `redact()` strips the token and API key from the report text, in case an error message contains them.

### 7. BACKGROUND LOOP

One loop starts with the node and runs forever.

**`registrar_loop`** registers with the router on startup. If the router isn't up yet, it retries after 1, 2, 4 … up to 60 seconds. After that, it watches `last_poll`: if the router hasn't polled for `REREGISTER_AFTER` seconds, it assumes the router restarted and lost its list, and registers again.

### 8. HTTP API

Four endpoints, each one short, because the real work happens in the sections above.

---

## Every interaction, step by step

### Registration (node → router)

```
Node ── POST ROUTER_URL {name,id,host,type} ──► Router
Node ◄─────────────── 200 ok ─────────────────  Router
```

This happens at startup and again whenever the router stops polling. The router should treat a repeat registration with the same `id` as an update.

### Status poll (router → node)

```
Router ── GET /status ──► Node
Router ◄── {identity…, status, task-id, report} ── Node
```

- `report` is only filled in when the status is `done` or `error`.
- The response includes the node's identity, so the router can check it reached the right node.
- Every poll also resets the node's re-register watchdog.

### Assigning a ticket (router → node)

```
Router ── POST /ticket {identity…, task-id, ticket, credentials} ──► Node
```

| Node's answer | When |
|---|---|
| `202 {"task-id", "status":"busy"}` | Accepted; work starts in the background |
| `403 identity mismatch` | The name, id, host or type doesn't match this node |
| `409 {"reason": "busy", "current-task-id"}` | The node is busy with another ticket. A node that's `done` or `error` accepts |
| `422` | The JSON is malformed, a field is missing, or the task-id is unsafe |

Inside the node: check identity → take the lock → confirm the slot isn't busy → save the ticket (without credentials) → start the runner → reply.

### Fetching a report by task-id (router → node)

```
Router ── GET /reports/{task-id} ──► Node
Router ◄── {"status", "report"} ─────  Node     (404 if unknown)
```

Every finished task's report lives here for good. Normally the router gets reports from `/status`. This endpoint is for when it missed one, for example because it sent the next ticket before reading the last report, or because it restarted.

**Why there's no ack:** an ack would only be needed if the node deleted reports. It doesn't. Reports stay on disk, and a finished node takes the next ticket, so nobody needs to be online to free a node.

### Health (anyone → node)

`GET /health` returns `{"ok": true}`. It's a plain "is the process alive" check.

---

## The life of one ticket

1. The router sends `POST /ticket`. The node checks it and replies `202`, and the state becomes **busy**.
2. The runner clones the repo into `work/<task-id>/` and gets onto the working branch.
3. Claude Code reads the ticket, explores the repo and edits files. It is not allowed to run git commands that change branches or history.
4. Claude finishes with `{success, summary, highlighted_files}`.
5. The node commits the changes and pushes the branch.
6. The node builds the report, and the state becomes **done**. If anything failed along the way, it becomes **error** with the reason.
7. The checkout folder is deleted.
8. The report is saved to `data/reports/<task-id>.json`. The router's next poll returns it, and the router stores it.
9. The node stays **done**, showing that report, and is ready for the next ticket.

---

## What was tested

These all ran against `fake_router.py` and a local git repo:

- Registration
- Ticket accepted (`202`)
- A second ticket while busy (`409`)
- Wrong identity (`403`)
- An unsafe task-id (`422`)
- Polling through busy to done, with a correct report
- A done node accepting the next ticket, with the old report still fetchable
- Report retrieval by task-id, and `404` for an unknown one
- A ticket naming an existing branch (`frontend`)
- A bad repo URL, which gives a clean error report
- `kill -9` mid-task, which comes back as `error` on restart
- Git login with a token, against a server that refuses clones without the right one (the token didn't end up in `.git/config`)
- Registration retrying while the router is down

The Claude worker was run with a fake API key. It started, failed authentication, returned a clean error report, and the key did not appear in it. **A real Claude run with a real key has not been tested yet.** That's your first test.

---

## Left out on purpose (vs. the architecture doc)

- **Push conflicts:** if the branch moved on the remote while Claude worked, the push fails and you get an error report. There's no rebase and no `-conflict` fallback branch yet.
- **Timeouts lose work:** when a ticket times out, partial changes are discarded rather than committed.
- **No identity check on `GET` requests:** `GET /status` and `GET /reports` don't check identity. The router checks the identity in the response instead.
- **No cancel endpoint, no progress reporting.**
- **Reports are never cleaned up.** They're small, but a "delete after 30 days" rule would be the next step.
- **No TLS:** credentials cross the LAN in plain HTTP. This is acceptable for the MVP.
- **No Dockerfile.** For safety, run each node in its own container or VM. Claude can run shell commands inside the checkout.
