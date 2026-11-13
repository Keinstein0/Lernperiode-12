"""Shared fixtures for the router test suite.

Provides a real mock node (a tiny HTTP server on 127.0.0.1) so that the
router's two-way communication with nodes (status probing, ticket
assignment) is exercised over actual HTTP, as described in tests.md.
"""

import json
import os
import sys
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import pytest

# Make the `flaskr` package importable regardless of where pytest is invoked.
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from flaskr import create_app  # noqa: E402


class MockNode:
    """A stand-in compute node speaking the router/node HTTP protocol.

    Endpoints:
        GET  /status  -> current node status report
        POST /ticket  -> receives assigned tickets

    Test hooks:
        responsive        if False, every request fails with a 500
        status            node status string ("idle", "in progress", ...)
        current_task_id   task id reported alongside the status
        received_tickets  list of ticket payloads posted to /ticket
        status_requests   number of GET /status requests received
    """

    def __init__(self, name="mock-node", node_id="mock-node-1", node_type="worker"):
        self.name = name
        self.id = node_id
        self.type = node_type
        self.status = "idle"
        self.current_task_id = None
        self.responsive = True
        self.received_tickets = []
        self.status_requests = 0

        self._server = ThreadingHTTPServer(("127.0.0.1", 0), self._make_handler())
        self.host = f"127.0.0.1:{self._server.server_address[1]}"
        self._thread = threading.Thread(target=self._server.serve_forever, daemon=True)
        self._thread.start()

    # -- protocol ---------------------------------------------------------

    def _status_report(self):
        return {
            "name": self.name,
            "id": self.id,
            "host": self.host,
            "type": self.type,
            "status": self.status,
            "task_id": self.current_task_id,
        }

    def _make_handler(self):
        node = self

        class Handler(BaseHTTPRequestHandler):
            def log_message(self, *args):  # silence request logging
                pass

            def _send(self, code, payload):
                body = json.dumps(payload).encode()
                self.send_response(code)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)

            def do_GET(self):
                if self.path == "/status":
                    node.status_requests += 1
                    if not node.responsive:
                        self._send(500, {"error": "node is on fire"})
                        return
                    self._send(200, node._status_report())
                else:
                    self._send(404, {"error": "not found"})

            def do_POST(self):
                if self.path == "/ticket":
                    length = int(self.headers.get("Content-Length", 0))
                    payload = json.loads(self.rfile.read(length) or b"{}")
                    if not node.responsive:
                        self._send(500, {"error": "node is on fire"})
                        return
                    node.received_tickets.append(payload)
                    self._send(200, {"status": "accepted"})
                else:
                    self._send(404, {"error": "not found"})

        return Handler

    # -- lifecycle --------------------------------------------------------

    def stop(self):
        self._server.shutdown()
        self._server.server_close()
        self._thread.join(timeout=5)


@pytest.fixture()
def app():
    app = create_app(test_config={"TESTING": True})
    yield app


@pytest.fixture()
def client(app):
    return app.test_client()


@pytest.fixture()
def mock_node():
    node = MockNode()
    yield node
    node.stop()


def post_registration(client, node: MockNode):
    """POSTs the registration payload for `node` to the router."""
    return client.post("/register/", json={
        "name": node.name,
        "id": node.id,
        "host": node.host,
        "type": node.type,
    })


def register_node(client, node: MockNode):
    """Registers `node` with the router and asserts it succeeded."""
    response = post_registration(client, node)
    assert response.status_code == 201, response.get_json()
    return response


def post_assignment(client, node_id: str, ticket: dict):
    """POSTs a task assignment for `node_id` to the router."""
    return client.post("/task/assign", json={
        "node_id": node_id,
        "ticket": ticket,
    })


@pytest.fixture()
def registered_node(client, mock_node):
    """A mock node that is already registered with the router."""
    register_node(client, mock_node)
    return mock_node


@pytest.fixture()
def ticket_payload():
    """A valid ticket body (TicketModel without the auto-generated timestamp)."""
    return {
        "created_by_github_user_id": "octocat-42",
        "git_repo_url": "https://github.com/example/repo",
        "git_branch_id": "feature-branch",
        "description": "Refactor the widget spinner",
    }
