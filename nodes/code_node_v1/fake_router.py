"""
Fake router — for testing a node without the real router.

It plays the router's side of every interaction, once, and prints each step:

  1. listens on :2048 and waits for the node to REGISTER
  2. sends a TICKET to the node
  3. (optional, --show-rejections) sends a second ticket  -> expects 409 busy
                                    sends a wrong identity -> expects 403
  4. POLLS GET /status every 2s until the node is done or error
  5. fetches the same report again by task-id from GET /reports/{task-id}
     (that's how a router gets a report it missed; there is no ack)

Credentials are read from this script's own environment (GIT_TOKEN,
ANTHROPIC_API_KEY) and passed in the ticket, just like the real router will.

Example:
  python fake_router.py --repo github.com/you/repo --desc "Add a README" --show-rejections
"""

import argparse
import asyncio
import json
import os
from datetime import datetime, timezone

import httpx
import uvicorn
from fastapi import FastAPI

registered: dict = {}
got_registration = asyncio.Event()
app = FastAPI()


@app.post("/")
async def register(body: dict):
    registered.update(body)
    got_registration.set()
    print(f"\n[1] REGISTER  <- {json.dumps(body)}\n    -> ok")
    return {"status": "ok"}


def show(step: str, r: httpx.Response) -> None:
    print(f"\n[{step}] {r.request.method} {r.request.url.path} -> {r.status_code}")
    print("    " + json.dumps(r.json(), indent=2).replace("\n", "\n    "))


async def drive(args) -> None:
    await got_registration.wait()
    node = f"http://{registered['host']}"
    identity = {k: registered[k] for k in ("name", "id", "host", "type")}

    ticket_req = {
        **identity,
        "task-id": args.task_id,
        "ticket": {
            "created-at": datetime.now(timezone.utc).isoformat(timespec="seconds"),
            "created-by": "fake-router",
            "git-repo-url": args.repo,
            "git-branch-id": args.branch,
            "files-of-interest": args.files,
            "description": args.desc,
        },
        "credentials": {
            "git-token": os.getenv("GIT_TOKEN"),
            "anthropic-api-key": os.getenv("ANTHROPIC_API_KEY"),
        },
    }

    async with httpx.AsyncClient(timeout=10) as c:
        show("2 TICKET", await c.post(f"{node}/ticket", json=ticket_req))

        if args.show_rejections:
            busy = {**ticket_req, "task-id": args.task_id + "-b"}
            show("3a SECOND TICKET (expect 409)", await c.post(f"{node}/ticket", json=busy))
            wrong = {**ticket_req, "id": "not-this-node"}
            show("3b WRONG IDENTITY (expect 403)", await c.post(f"{node}/ticket", json=wrong))

        print("\n[4] POLLING /status ...")
        while True:
            r = await c.get(f"{node}/status")
            s = r.json()
            print(f"    status={s['status']} task-id={s['task-id']}")
            if s["status"] in ("done", "error"):
                show("4 STATUS WITH REPORT", r)
                break
            await asyncio.sleep(2)

        print("\n[5] Report stored in client mailbox (pretend).")
        show("5 REPORT BY TASK-ID", await c.get(f"{node}/reports/{args.task_id}"))
        print("\nNode stays 'done' (showing this report) until it gets its next ticket.")


async def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument("--repo", required=True, help="e.g. github.com/user/repo or /path/to/repo")
    p.add_argument("--branch", default=None, help="working branch (omit: node creates one)")
    p.add_argument("--desc", required=True, help="ticket description")
    p.add_argument("--files", nargs="*", default=[], help="files of interest")
    p.add_argument("--task-id", default=datetime.now().strftime("%H%M%S"))
    p.add_argument("--port", type=int, default=2048)
    p.add_argument("--show-rejections", action="store_true")
    args = p.parse_args()

    server = uvicorn.Server(uvicorn.Config(app, host="0.0.0.0", port=args.port, log_level="warning"))
    serve = asyncio.create_task(server.serve())
    print(f"Fake router listening on :{args.port}, waiting for a node to register...")
    await drive(args)
    server.should_exit = True
    await serve


if __name__ == "__main__":
    asyncio.run(main())
