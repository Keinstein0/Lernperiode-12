"""Scenarios:
- "client can query for all nodes"  -> node status correct
- "client can query for a single node"
  -> node status correct
  -> node does not exist
  -> node irresponsive
"""

from conftest import MockNode, register_node


def test_get_all_nodes_returns_registered_nodes_with_status(client, mock_node):
    second_node = MockNode(name="second-node", node_id="mock-node-2")
    try:
        register_node(client, mock_node)
        register_node(client, second_node)

        response = client.get("/node/")

        assert response.status_code == 200
        nodes = response.get_json()
        assert len(nodes) == 2

        by_id = {n["id"]: n for n in nodes}
        assert by_id[mock_node.id]["status"] == "idle"
        assert by_id[mock_node.id]["name"] == mock_node.name
        assert by_id[mock_node.id]["type"] == mock_node.type
        assert "host" not in by_id[mock_node.id]  # not exposed to the client
        assert by_id[second_node.id]["status"] == "idle"
    finally:
        second_node.stop()


def test_get_single_node_returns_correct_status(client, registered_node):
    response = client.get(f"/node/{registered_node.id}")

    assert response.status_code == 200
    body = response.get_json()
    assert body["id"] == registered_node.id
    assert body["name"] == registered_node.name
    assert body["status"] == "idle"
    assert body["task_id"] is None


def test_get_single_node_reflects_live_status_change(client, registered_node):
    """When the node picks up work, the queried status must follow."""
    registered_node.status = "in progress"
    registered_node.current_task_id = "task-42"

    response = client.get(f"/node/{registered_node.id}")

    assert response.status_code == 200
    body = response.get_json()
    assert body["status"] == "in progress"
    assert body["task_id"] == "task-42"


def test_get_nonexisting_node_returns_404(client):
    response = client.get("/node/does-not-exist")
    assert response.status_code == 404


def test_irresponsive_node_is_reported_unreachable(client, registered_node):
    registered_node.responsive = False

    response = client.get(f"/node/{registered_node.id}")

    assert response.status_code == 200
    assert response.get_json()["status"] == "unreachable"


def test_irresponsive_node_in_overview_is_reported_unreachable(
        client, registered_node):
    registered_node.responsive = False

    response = client.get("/node/")

    assert response.status_code == 200
    nodes = response.get_json()
    assert len(nodes) == 1
    assert nodes[0]["status"] == "unreachable"
