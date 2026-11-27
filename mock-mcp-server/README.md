# UniSK Mock MCP Server

A fake version of the UniSKv2 **Router's MCP interface** (see `uniskv2-interactions.md`).
It keeps nodes, tasks and permissions in memory and simulates nodes working on tasks,
so the client/frontend can be built before the real router (`newrouter/`) is ready.

Nothing is persisted: restarting the server resets everything to the seed data.

## Run

```bash
cd mock-mcp-server
npm install
npm run dev        # restarts on file changes
# or: npm start
```

- MCP endpoint: `http://localhost:3333/mcp` (Streamable HTTP, stateless)
- Health check: `http://localhost:3333/health`

| Env var        | Default | Meaning                                    |
|----------------|---------|--------------------------------------------|
| `PORT`         | `3333`  | HTTP port                                  |
| `MOCK_STEP_MS` | `2000`  | Duration of one simulated task step in ms  |

PowerShell: `$env:MOCK_STEP_MS=200; npm run dev`

## Tools

| Group     | Tool                            | Notes                                                        |
|-----------|---------------------------------|--------------------------------------------------------------|
| Discovery | `get_nodes`                     | Ranks nodes by word-based cosine similarity to `query`       |
| Discovery | `get_node`                      | Full AgentCard + status                                      |
| Discovery | `get_extended_agent_card`       | Only `node-claude-code-1` supports it                        |
| Tasks     | `send_task`                     | Blocking by default; `configuration.returnImmediately: true` |
| Tasks     | `get_task`                      | Optional `historyLength`                                     |
| Tasks     | `get_tasks`                     | Filter by `contextId` / `status`, paging via `pageToken`     |
| Tasks     | `get_contexts`                  | Conversation threads, `active_only` filter                   |
| Tasks     | `cancel_task`                   | Fails for tasks that already ended                           |
| Admin     | `admin_list_nodes`              | Optional text `filter`                                       |
| Admin     | `admin_get_node`                |                                                              |
| Admin     | `admin_update_node_permissions` | `add_allowed_nodes` / `remove_allowed_nodes`                 |
| Admin     | `admin_promote_node`            | Needs `confirm: true`                                        |
| Admin     | `admin_delete_node`             | Needs `confirm: true`, cancels the node's running tasks      |

Not mocked yet: `send_task_stream`, `subscribe_task`, `set_push_notification`.

Errors come back as tool results with `isError: true` and a body like
`{ "error": { "code": "NODE_BUSY", "message": "..." } }`.

## Simulated behaviour

**Task lifecycle:** `SUBMITTED -> WORKING -> COMPLETED`, one step every `MOCK_STEP_MS`.
The text of the last user message changes the outcome:

| Message contains | Task ends in                | Continue with                                      |
|------------------|-----------------------------|----------------------------------------------------|
| `fail`           | `TASK_STATE_FAILED`         | (terminal)                                         |
| `?`              | `TASK_STATE_INPUT_REQUIRED` | `send_task` with `message.taskId` set to that task |
| anything else    | `TASK_STATE_COMPLETED`      | (terminal), result in `artifacts`                  |

A node is `busy` while it works and rejects new tasks with `NODE_BUSY`.
Offline nodes reject with `NODE_UNREACHABLE`.

**Seed data**

| Node                 | Status  | Notes                              |
|----------------------|---------|------------------------------------|
| `node-claude-code-1` | free    | streaming, extended card           |
| `node-research-1`    | busy    | stays busy (task-0002 is WORKING)  |
| `node-docs-1`        | free    | task-0003 waits for input          |
| `node-roblox-1`      | offline |                                    |

Tasks: `task-0001` (COMPLETED), `task-0002` (WORKING), `task-0003` (INPUT_REQUIRED).

**Fake auth (admin tools only)**

| `Authorization` header | Role                       |
|------------------------|----------------------------|
| _(none)_               | admin (local dev default)  |
| `Bearer mock-admin`    | admin                      |
| anything else          | user -> `FORBIDDEN`        |

## Use from the frontend

```bash
cd frontend
npm install @modelcontextprotocol/sdk
```

```ts
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StreamableHTTPClientTransport } from '@modelcontextprotocol/sdk/client/streamableHttp.js';

const client = new Client({ name: 'unisk-frontend', version: '0.1.0' });
await client.connect(new StreamableHTTPClientTransport(new URL('http://localhost:3333/mcp')));

const res = await client.callTool({ name: 'get_nodes', arguments: { query: 'fix a typescript bug' } });
if (res.isError) throw new Error(JSON.stringify(res.content));
console.log(res.structuredContent); // { query, nodes: [...] }
```

Every successful tool returns the same data twice: as JSON text in `content`
(for LLM clients) and as an object in `structuredContent` (use this one in code).

## Quick test without code

```bash
npx @modelcontextprotocol/inspector
```

Choose transport "Streamable HTTP", URL `http://localhost:3333/mcp`, then connect and call tools.
