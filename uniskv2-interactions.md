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

