# UniSK integration demo

This is a **local demo**, not a production deployment. The frontend sends real
HTTP requests to the Flask router, which communicates with the included demo
node. The demo node **does not run an LLM, modify code, push to Git, or use
external credentials**. It returns simulated reports for assigned tickets.

## Prerequisites

- Docker Desktop or Docker Engine with the Compose plugin
- Internet access for Docker image and dependency downloads on the first run

## Run

From the repository root:

```bash
docker compose -f docker-compose.demo.yml up --build
```

Open **http://localhost:5173** and choose **Enter demo workspace**. The demo
account is stored locally in your browser; no GitHub OAuth is performed.

1. Go to **Agents**. You should see **Demo agent** (idle). It registers after
   the router starts; refresh the page if it is not immediately visible.
2. Open **Projects → Website redesign → Tasks → New task**. Select Demo agent,
   enter a description, and choose **Assign ticket**.
3. The frontend sends `POST /api/task/assign` through the Vite proxy to the
   router's `POST /task/assign` endpoint. The router forwards the ticket to the
   demo node at `POST /ticket`.
4. Watch status change from **busy** to **done** after roughly seven seconds.
   Open task details to see the reported result.
5. In **Conversation**, send `task: Review the documentation` to create a
   ticket from chat. Other messages get local simulated chat replies.

Stop with Ctrl+C, or:

```bash
docker compose -f docker-compose.demo.yml down
```

## Architecture and limitations

```
Browser :5173
  | /api/* proxied by Vite
  v
Flask router :5000   <--- node registration
  | /status, /ticket, /reports/{task-id}
  v
Demo agent :5556 (internal-only)
```

The router endpoints currently implemented are `GET /node/`, `GET /task/`,
`GET /task/{id}` and `POST /task/assign`. They differ from the earlier
short-form names in `interactions.md`. JSON keys use kebab-case.

**Backend-backed:** Node directory/status, ticket assignment, task status, and
reports. **Local browser demo state:** Project creation/editing/deletion,
repository associations, conversation history, simulated chat replies, and
session. Task-to-project associations are stored in localStorage; linked
repository URLs provide a fallback for tasks visible in another session.

- Router data is in memory and is lost when the router restarts; it runs as
  **one Gunicorn worker** so in-memory stores are consistent.
- Project and chat data remain local to each browser; these operations have
  no backend contract yet.
- GitHub OAuth is not completed in the router. This demo intentionally uses
  local sign-in rather than claiming a real GitHub login.
- Live API errors are shown in the frontend. The router and demo agent do not
  need any API credentials; do **not** expose demo ports publicly.
- Run `docker compose -f docker-compose.demo.yml down` to stop services.

## Frontend-only mode

For an entirely offline frontend with mock agents:

```bash
cd frontend
npm ci
npm run dev
```

The default `VITE_API_MODE` is `mock`; the Compose demo sets it to `router`.
Never put API tokens into `VITE_` variables: they are exposed in the browser.
