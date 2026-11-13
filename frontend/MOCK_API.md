# Mock client/router integration

The frontend imports `api` from `src/lib/api/index.ts`. The exported client implements
`ApiClient` from `types.ts` through `mock.ts`. **No HTTP requests are made.**
All methods return Promises and return cloned data to simulate request/response boundaries.
The mock stores ticket assignments, project edits, and conversation messages in memory; a full page reload resets them.

## Covered client-to-router operations (interactions.md)

| Client method | Intended endpoint | Current behavior |
| --- | --- | --- |
| `login()` | GitHub OAuth through router | Resolves a sample session; no OAuth or JWT verification |
| `listNodes()` | `GET /nodes` | Returns sample nodes |
| `getNodeStatuses()` | `GET /status` | Returns node status snapshots |
| `getTask(id)` | `GET /task/:id` | Returns a task or rejects with an error |
| `assignTicket({ nodeId, ticket })` | `POST /assign` | Validates node and ticket, creates a task ID, sets node busy |

The shape of `Node`, `Task`, `Ticket`, and `Report` retains the hyphenated field names
specified in `interactions.md`. The mock status names are exactly `busy`, `idle`,
`done`, and `error` for nodes, and `busy`, `done`, and `error` for tasks.

`listProjects`, `getProject`, `createProject`, `updateProject`, `listProjectTasks`, `listMessages`, `sendMessage`, `getAgentReply`, `advanceTask`, and
`listTasks` are **mock-only additions**: there is no defined router endpoint for them
in `interactions.md` yet. The project list, chat, global agent directory, task creation,
and task detail view all access the adapter instead of importing static fixture arrays.

The router-to-node registration, status polling, and ticket dispatch workflows belong
on the backend and are deliberately not implemented in the frontend. Ticket creation
on the frontend represents **only** the client-to-router `/assign` contract.

For local UI testing, `assignTicket` also accepts an optional `projectSlug` to associate
a ticket with a project that does not yet have a repository. This field is **not** part
of the documented `/assign` body and must be removed when wiring real HTTP requests.

The project editor, task search/status filter, task completion simulation, and
chat auto-replies are mock-only features. Simulated completion intentionally runs
only after clicking **Simulate status update** in the task detail view.

When integrating the real router, implement `ApiClient` in a new adapter and change
only `src/lib/api/index.ts` to export it. Authentication/storage/authorization rules
will need to be designed before using real tokens. This stage does not persist tokens.
