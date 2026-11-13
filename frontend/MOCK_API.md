# Mock client/router integration

The frontend imports `api` from `src/lib/api/index.ts`. The exported client implements
`ApiClient` from `types.ts` through `mock.ts`. **No HTTP requests are made.**
All methods return Promises and return cloned data to simulate request/response boundaries.
The mock persists projects, tasks, messages and the simulated login session in browser `localStorage` (`unisk-mock-state-v2`). Data is browser-local and not shared across devices. Clear that key to reset fixtures.

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

`getSession`, `logout`, `deleteProject`, `listProjects`, `getProject`, `createProject`, `updateProject`, `listProjectTasks`, `listMessages`, `sendMessage`, `getAgentReply`, `advanceTask`, and
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
chat auto-replies are mock-only features. Newly assigned tickets complete automatically after roughly 15 seconds, when the next status request is made. The task detail manual simulation remains available. Agent directories and project task lists poll every four seconds while mounted. Chat commands of the form `task: <description>` assign a ticket to an available node. Project deletion removes associated tasks and conversation messages.

When integrating the real router, implement `ApiClient` in a new adapter and change
only `src/lib/api/index.ts` to export it. Authentication/storage/authorization rules
will need to be designed before using real tokens. The app shell redirects visitors without a mock session to `/login` (client-side UI gating only). The mock persists a non-sensitive fake session token for UI testing only; never use it for real authentication or authorization.
