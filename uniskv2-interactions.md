# UniSKv2 because we like our agents standardized

## What is UniSKv2? 
Well it's complicated but basically UniSK originally was basically a replica of the A2A protocol which made it entirely redundant. So we've pivoted to a more sensible solution which it to code an *implementation* of A2A instead of a competing standard. It consists of roughly 
4 components.


## Interactions:
### Auth Node:

Only ever in contact with the router, a traditional REST API, probably will be written in C# ASP.NET because i'm lazy and don't want to learn another framework.

| Group        | Endpoint                           | Purpose                                                                              |
|--------------|------------------------------------|--------------------------------------------------------------------------------------|
| Node Auth    | POST /auth/node/login              | Exchange node credentials for a short-lived token                                    |
| Permissions  | POST /auth/permissions/check       | Check if caller token can perform action on target_node_id                           |
| Permissions  | POST /auth/permissions/filter      | Filter a list of node_ids down to those allowed for caller                           |
| Registration | POST /auth/nodes                   | Router-triggered registration; creates identity with 0 permissions & one-time secret |
| Admin        | GET /auth/nodes                    | List all nodes and their permissions                                                 |
| Admin        | GET /auth/nodes/{node_id}          | Inspect one node's identity and permissions                                          |
| Admin        | PATCH /auth/nodes/{node_id}        | Add/remove permitted target nodes                                                    |
| Admin        | POST /auth/nodes/{node_id}/promote | Explicitly grant admin privileges                                                    |
| Admin        | DELETE /auth/nodes/{node_id}       | Revoke node identity and kill access immediately                                     |

### Router:

### A4 Node:
| Group                 | Endpoint                         | Purpose                          |                                                                                                                              |                                                                                                                                   |
|-----------------------|----------------------------------|----------------------------------|------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------|
| Category              | A4 Action / MCP Tool             | Underlying A2A Op                | Key Parameters                                                                                                               | What It Does                                                                                                                      |
| Discovery & Matching  | get_nodes                        | (Router / Registry)              | query (string), capability_filter (optional)                                                                                 | Filters nodes A4 is allowed to use and ranks them by embedding cosine similarity against query (AgentCard skills/description).    |
| Discovery & Matching  | get_node                         | GET /.well-known/agent-card.json | node_id (or agent URL)                                                                                                       | Fetches a single node's AgentCard, status, skills, and supported workspace modes (e.g., git, conngit, files).                     |
| Discovery & Matching  | get_extended_agent_card          | GetExtendedAgentCard             | node_id (optional tenant/auth context)                                                                                       | Retrieves extra authenticated card metadata/capabilities if the target node advertises extendedAgentCard.                         |
| Task Execution        | send_task                        | SendMessage                      | node_id, message (parts, optional taskId / contextId), configuration (returnImmediately, historyLength, acceptedOutputModes) | Starts a new task (or continues one in TASK_STATE_INPUT_REQUIRED / same contextId) and returns either a Task or a direct Message. |
| Task Execution        | send_task_stream                 | SendStreamingMessage             | Same as send_task (message, configuration)                                                                                   | Sends a message and opens an SSE stream yielding live TaskStatusUpdateEvent and TaskArtifactUpdateEvent chunks.                   |
| Monitoring & Recovery | get_task                         | GetTask                          | task_id (id), optional historyLength                                                                                         | Fetches current task status (TASK_STATE_*), artifacts, and recent message history.                                                |
| Monitoring & Recovery | subscribe_task                   | SubscribeToTask                  | task_id (id)                                                                                                                 | Re-attaches to an in-progress task's SSE event stream after a client/network disconnect without starting a new run.               |
| Monitoring & Recovery | get_tasks                        | ListTasks                        | Optional contextId, status, pageSize, pageToken, includeArtifacts                                                            | Lists logged/known tasks so A4 can audit history or recover an active taskId after reconnecting.                                  |
| Monitoring & Recovery | get_contexts                     | (A4 / Router Session Store)      | Optional node_id, active_only (bool)                                                                                         | Returns active/known contextIds (conversation threads) A4 has opened so follow-up work can stay grouped in the same context.      |
| Lifecycle & Auth      | cancel_task                      | CancelTask                       | task_id (id)                                                                                                                 | Requests cancellation of a running task (TASK_STATE_CANCELED); A4 handles cases where it already finished or cannot be canceled.  |
| Lifecycle & Auth      | login (Harness / Auth)           | (OAuth 2.1 Client Credentials)   | client_id, client_secret (or token endpoint)                                                                                 | Authenticates Node A4 with the Auth/Router layer to get a short-lived Bearer token automatically at startup/refresh.              |
| Lifecycle & Auth      | set_push_notification (Deferred) | CreateTaskPushNotificationConfig | task_id (id), webhook url, optional auth config                                                                              | Registers a callback webhook for long-running background tasks; safe to skip until you need async webhooks over polling/SSE.      |
