# Interactions

## Router to Node

Node -- register --> Router
Node <- ok --------- Router

Node <- get-status - Router
Node -- status ----- Router

### register:

POST [router-ip]:2048

```json
{
  "name": "my-node-12",
  "id": "alwjehflksja3a342y",
  "host": "192.168.0.25:5556",
  "type": "keinstein0:research-agent-1.3"
}
```

### get-status:

GET [node-ip]:5556

response:

```json
{
  "name": "my-node-12",
  "id": "alwjehflksja3a342y",
  "host": "192.168.0.25:5556",
  "type": "keinstein0:research-agent-1.3"

  "status": "busy" // [busy, idle, done, error]
  "task-id": "12345678"
  // only on error, or done not null
  "message": "i have encountered an error when trying to do task XY, give me further instructions"
}
```
