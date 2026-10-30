# Interactions

## Router to Node

#### registration
```
Node -- register --> Router

Node <- ok --------- Router
```

#### status (polled in an interval)
```
Node <- get-status ------- Router

Node -- status(+report) -> Router
```

#### assign ticket
```
Node <- ticket------ Router

Node -- id --------> Router
```

## Router to Node requests

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
  "name": "my-node-12", //metadata for validation
  "id": "alwjehflksja3a342y",
  "host": "192.168.0.25:5556",
  "type": "keinstein0:research-agent-1.3"

  "status": "busy" // [busy, idle, done, error]
  "task-id": "12345678"
  // only on error, or done not null
  "report": // see report
}
```

### ticket:

POST [node-ip]:5556

```json
{
  "name": "my-node-12", //metadata for validation, if they don't match the request is rejected
  "id": "alwjehflksja3a342y",
  "host": "192.168.0.25:5556",
  "type": "keinstein0:research-agent-1.3"

  "task-id": "12345678"
  "ticket": //see ticket definition
}
```

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


#### get all nodes status
```
Client -- /status--> Router

Client <- status --- Router
```

#### get one nodes status
```
Client /status/id-> Router

Client <-status --- Router
```

#### get one nodes status
```
Client /status/id-> Router

Client <-status --- Router
```

#### get task
```
Client --/task/id-> Router

Client <-task------ Router
```
Gets a task (node its running on, report if finished, ticket), only shows your tasks


#### assign ticket
```
Client ---/assign-> Router

Client <-ok+id----- Router
```
Returns the routers ID for the task. 



## Data Structures
### ticket structure (ticket = do something)

```json
{
  // Metadata
  "created-at": "datetime"
  "created-by": "keinstein0"

  // Usage parameters
  "git-repo-url": "github.com/Keinstein0/Lernperiode-12"
  "git-branch-id": "frontend"

  // wonky parameters
  "files-of-interest": ["main.py"] // autoadded to context, interpretation varies between node

  // Ticket description
  "description": "change the color of the continue button to a darker shade of blue"
}
```

### report structure (report = did something)
```json
{
  // Metadata
  "finished-at": "datetime"
  "finished-by": "alwjehflksja3a342y" // node id

  // Usage parameters
  "git-repo-url": "github.com/Keinstein0/Lernperiode-12"
  "git-branch-id": "frontend"

  // wonky parameters
  "files-of-interest": ["main.py"] // what was changed/added that is especially relevant

  // Action description
  "description": "changed the color of the continue button to a darker shade of blue"
}
```
