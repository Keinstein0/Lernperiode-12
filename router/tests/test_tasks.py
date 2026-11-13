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
    assert task["node_id"] == registered_node.id
    assert task["status"] == "in progress"
    assert task["ticket"]["description"] == ticket_payload["description"]
    assert task["ticket"]["git_repo_url"] == ticket_payload["git_repo_url"]
    assert task["id"]  # a task id was generated


def test_task_is_assigned_to_mock_node_correctly(client, registered_node,
                                                 ticket_payload):
    """The ticket must actually arrive at the mock node's /ticket endpoint."""
    assert registered_node.received_tickets == []

    response = post_assignment(client, registered_node.id, ticket_payload)
    assert response.status_code == 201

    assert len(registered_node.received_tickets) == 1
    received = registered_node.received_tickets[0]
    assert received["task_id"] == response.get_json()["task"]["id"]
    assert received["ticket"]["description"] == ticket_payload["description"]
    assert received["ticket"]["git_branch_id"] == ticket_payload["git_branch_id"]


def test_get_all_tasks_returns_created_task(client, registered_node,
                                            ticket_payload):
    created = post_assignment(client, registered_node.id,
                              ticket_payload).get_json()["task"]

    response = client.get("/task/")

    assert response.status_code == 200
    tasks = response.get_json()
    assert len(tasks) == 1
    assert tasks[0]["id"] == created["id"]
    assert tasks[0]["node_id"] == registered_node.id
    assert tasks[0]["status"] == "in progress"
    assert tasks[0]["ticket"]["description"] == ticket_payload["description"]


def test_task_rejected_on_nonexisting_node(client, ticket_payload):
    response = post_assignment(client, "no-such-node", ticket_payload)

    assert response.status_code == 404
    assert "does not exist" in response.get_json()["error"]


def test_task_rejected_on_busy_node(client, registered_node, ticket_payload):
    registered_node.status = "in progress"

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
    response = client.post("/task/assign", json={"node_id": registered_node.id})
    assert response.status_code == 400
    assert response.get_json()["error"] == "Validation failed"
