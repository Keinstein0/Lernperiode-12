# Interactions

## Conventions

- **JSON keys are kebab-case** on the wire (`task-id`, `git-repo-url`). Python code uses snake_case internally and maps it with Pydantic aliases (`alias_generator` + `model_dump(by_alias=True)`).
- **Timestamps** are ISO 8601 in UTC, e.g. `2026-11-13T09:22:42+00:00`.
- **Node status values:** `idle`, `busy`, `done`, `error`. The router adds `unreachable` in its client-facing node structure when it can't get a status from a node.
- **No ack.** A node in `done` or `error` keeps showing its last report on `/status` and accepts the next ticket. Every report is also kept on the node and can be fetched by task-id.
- `//` comments in the JSON examples are notes, not part of the payload.

## Router to Node

#### registration
```
Node -- register --> Router
                     Router -- get-status --> Node   (router checks the node is reachable and the metadata matches)
Node <- ok --------- Router
```
The node registers on startup, and again if the router hasn't polled it for 120 s (router restarted and forgot it).

#### status (polled in an interval)
```
Node <- get-status ------- Router

Node -- status(+report) -> Router
```

#### assign ticket
```
Node <- ticket------ Router

Node -- task-id ---> Router
```

#### fetch an old report
```
Node <- get-report(task-id) -- Router

Node -- report --------------> Router
```
For reports the router missed, e.g. after the node already took its next ticket.

## Router to Node requests

### register:

POST [router-host]/register/  (port 5000 in the compose setup; note the trailing slash)

```json
{
  "name": "my-node-12",
  "id": "alwjehflksja3a342y",
  "host": "192.168.0.25:5556",
  "type": "keinstein0:research-agent-1.3"
}
```

| Response | When |
|---|---|
| `201` + the registered node | New node |
| `200` | Node id already registered |
| `400` | Invalid body |
| `403` | Metadata doesn't match what the node's `/status` returns |
| `503` | Router can't reach the node's `/status` |

### get-status:

GET [node-host]/status

response:

```jsonc
{
  "name": "my-node-12", // metadata for validation
  "id": "alwjehflksja3a342y",
  "host": "192.168.0.25:5556",
  "type": "keinstein0:research-agent-1.3",

  "status": "busy",      // idle | busy | done | error
  "task-id": "12345678", // null when idle
  "report": null         // report structure when done or error, otherwise null
}
```

### ticket:

POST [node-host]/ticket

```jsonc
{
  "name": "my-node-12", // metadata for validation, if they don't match the request is rejected
  "id": "alwjehflksja3a342y",
  "host": "192.168.0.25:5556",
  "type": "keinstein0:research-agent-1.3",

  "task-id": "12345678",
  "ticket": { /* see ticket structure */ },
  "credentials": { /* optional, see credentials structure */ }
}
```

| Response | When |
|---|---|
| `202` `{"task-id": "12345678", "status": "busy"}` | Accepted, work starts |
| `403` | Identity block doesn't match the node |
| `409` `{"reason": "busy", "current-task-id": "..."}` | Node is busy with another ticket (`done`/`error` nodes accept) |
| `422` | Invalid body, or unsafe task-id (allowed: letters, digits, `-`, `_`, `.`; max 64) |

### get-report:

GET [node-host]/reports/{task-id}

response (`404` if the node never finished that task):

```jsonc
{
  "status": "done", // done | error
  "report": { /* see report structure */ }
}
```

### health:

GET [node-host]/health → `{"ok": true}`. Liveness only, no metadata.

## Client2Router

#### login:
```
Client -- login -----> Router -- redirect --> Github
Client                 Router <-----code----- Github
Client                 Router ---get token--> Github
Client                 Router <--token------- Github
Client                 Router ---get user---> Github
Client                 Router <--user-------- Github
Client <--401/JWT----- Router                 Github
```
Every JWT is valid only on the specific router and sorting them is the job of the client

#### get all nodes
```
Client -- /nodes----> Router

Client <- nodes ----- Router
```
Returns `node` structure


#### get all nodes status
```
Client -- /status--> Router

Client <- status --- Router
```
Returns list of `node` structures

#### get task
```
Client --/task/id-> Router

Client <-task------ Router
```
Gets a task (node its running on, report if finished, ticket), only shows your tasks

Returns `task` structure


#### assign ticket
```
Client ---/assign-> Router

Client <-ok+id----- Router
```
Requires a node id and a ticket, builds a task from it
Returns the task ID for the task.

```jsonc
{
  "node-id": "code-node-1",
  "ticket": { /* see ticket structure */ }
}
```


## Data Structures
### ticket structure (ticket = do something)

```jsonc
{
  // Metadata
  "created-at": "2026-11-13T09:00:00+00:00",
  "created-by": "keinstein0",

  // Usage parameters
  "git-repo-url": "github.com/Keinstein0/Lernperiode-12",
  "git-branch-id": "frontend", // optional: if null, the node creates node/<node-id>/<task-id>

  // wonky parameters
  "files-of-interest": ["main.py"], // optional, autoadded to context, interpretation varies between node

  // Ticket description
  "description": "change the color of the continue button to a darker shade of blue"
}
```

### report structure (report = did something)
```jsonc
{
  // Metadata
  "finished-at": "2026-11-13T09:22:42+00:00",
  "finished-by-node-id": "alwjehflksja3a342y",

  // Usage parameters
  "git-repo-url": "github.com/Keinstein0/Lernperiode-12",
  "git-branch-id": "frontend", // the branch the work was actually pushed to

  // wonky parameters
  "files-of-interest": ["main.py"], // what was changed/added that is especially relevant

  // Action description (on error: what went wrong)
  "description": "changed the color of the continue button to a darker shade of blue"
}
```

### credentials structure (router to node only, optional)
```jsonc
{
  "git-token": "github_pat_...",      // needed to push, even to public repos
  "git-username": "x-access-token",   // optional, this is the default
  "anthropic-api-key": "sk-ant-..."   // optional
}
```
Sent next to the ticket, never inside it, so tickets can be stored and logged without secrets. The node keeps credentials in memory for one task only. Until the router sends them, the node falls back to its own `GIT_TOKEN` and `ANTHROPIC_API_KEY` environment variables.

### node structure
```jsonc
{
  "name": "my-node-12", // metadata
  "id": "alwjehflksja3a342y",
  "type": "keinstein0:research-agent-1.3",

  "status": "busy",     // idle | busy | done | error | unreachable
  "task-id": "12345678" // or null
}
```

### task structure
```jsonc
{
  "id": "alwjehflksja3a342y",      // self id
  "node-id": "alwjehflksja3a342y", // node that is doing the task
  "ticket": { /* ticket */ },
  "status": "busy",                // busy | done | error
  "report": null                   // report, only on done/error
}
```
