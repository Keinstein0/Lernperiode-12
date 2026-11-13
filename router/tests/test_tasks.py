"""Scenarios:
- "post tasks to node"
  -> get all tasks returns (created) task
  -> task assigned to mock node correctly
  -> task rejected on nonexisting node
  -> task rejected on busy/unavailable node
"""

from conftest import post_assignment


def test_post_task_creates_and_assigns_task(client, registered_node,
                                            ticket_payload):
    response = post_assignment(client, registered_node.id, ticket_payload)

    assert response.status_code == 201
    body = response.get_json()
    assert body["status"] == "assigned"

    task = body["task"]
    assert task["node-id"] == registered_node.id
    assert task["status"] == "busy"
    assert task["ticket"]["description"] == ticket_payload["description"]
    assert task["ticket"]["git-repo-url"] == ticket_payload["git-repo-url"]
    assert task["id"]  # a task id was generated


def test_task_is_assigned_to_mock_node_correctly(client, registered_node,
                                                 ticket_payload):
    """The ticket must actually arrive at the mock node's /ticket endpoint."""
    assert registered_node.received_tickets == []

    response = post_assignment(client, registered_node.id, ticket_payload)
    assert response.status_code == 201

    assert len(registered_node.received_tickets) == 1
    received = registered_node.received_tickets[0]
    assert received["task-id"] == response.get_json()["task"]["id"]
    assert received["ticket"]["description"] == ticket_payload["description"]
    assert received["ticket"]["git-branch-id"] == ticket_payload["git-branch-id"]
    # identity block, so the node can check the ticket is meant for it
    assert received["id"] == registered_node.id
    assert received["host"] == registered_node.host


def test_get_all_tasks_returns_created_task(client, registered_node,
                                            ticket_payload):
    created = post_assignment(client, registered_node.id,
                              ticket_payload).get_json()["task"]

    response = client.get("/task/")

    assert response.status_code == 200
    tasks = response.get_json()
    assert len(tasks) == 1
    assert tasks[0]["id"] == created["id"]
    assert tasks[0]["node-id"] == registered_node.id
    assert tasks[0]["status"] == "busy"
    assert tasks[0]["ticket"]["description"] == ticket_payload["description"]


def test_task_rejected_on_nonexisting_node(client, ticket_payload):
    response = post_assignment(client, "no-such-node", ticket_payload)

    assert response.status_code == 404
    assert "does not exist" in response.get_json()["error"]


def test_task_rejected_on_busy_node(client, registered_node, ticket_payload):
    registered_node.status = "busy"

    response = post_assignment(client, registered_node.id, ticket_payload)

    assert response.status_code == 409
    assert "busy" in response.get_json()["error"]


def test_task_rejected_on_unavailable_node(client, registered_node,
                                           ticket_payload):
    registered_node.responsive = False

    response = post_assignment(client, registered_node.id, ticket_payload)

    assert response.status_code == 503


def test_task_rejected_on_nonexisting_node_creates_no_task(
        client, ticket_payload):
    post_assignment(client, "no-such-node", ticket_payload)

    response = client.get("/task/")
    assert response.get_json() == []


def test_task_assignment_with_invalid_payload_is_rejected(
        client, registered_node):
    response = client.post("/task/assign", json={"node-id": registered_node.id})
    assert response.status_code == 400
    assert response.get_json()["error"] == "Validation failed"


def test_ticket_without_branch_is_accepted(client, registered_node, ticket_payload):
    """git-branch-id is optional: the node then creates its own branch."""
    ticket = {k: v for k, v in ticket_payload.items() if k != "git-branch-id"}
    response = post_assignment(client, registered_node.id, ticket)
    assert response.status_code == 201
    assert registered_node.received_tickets[0]["ticket"]["git-branch-id"] is None


def test_finished_task_returns_report(client, registered_node, ticket_payload):
    """A done node's kebab-case report is parsed and handed to the client."""
    task_id = post_assignment(client, registered_node.id,
                              ticket_payload).get_json()["task"]["id"]
    registered_node.status = "done"
    registered_node.current_task_id = task_id
    registered_node.report = {
        "finished-at": "2026-11-13T09:22:42+00:00",
        "finished-by-node-id": registered_node.id,
        "git-repo-url": ticket_payload["git-repo-url"],
        "git-branch-id": ticket_payload["git-branch-id"],
        "files-of-interest": ["main.py"],
        "description": "did the thing",
    }

    response = client.get(f"/task/{task_id}")

    assert response.status_code == 200
    task = response.get_json()
    assert task["status"] == "done"
    assert task["report"]["finished-by-node-id"] == registered_node.id
    assert task["report"]["files-of-interest"] == ["main.py"]
    assert task["report"]["description"] == "did the thing"
