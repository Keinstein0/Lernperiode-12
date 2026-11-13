"""Scenario: "nodes can be registered to allow two way communication"
(tested using mock node).
"""

from conftest import register_node, post_registration


def test_register_node_succeeds_and_communicates_two_way(client, mock_node):
    """Registering a node stores it, and the router actually reached out to
    the mock node's /status endpoint to verify it (two way communication)."""
    status_requests_before = mock_node.status_requests

    response = register_node(client, mock_node)

    assert response.status_code == 201
    body = response.get_json()
    assert body["name"] == mock_node.name
    assert body["id"] == mock_node.id
    assert body["host"] == mock_node.host
    assert body["type"] == mock_node.type

    # The router must have polled the mock node during registration.
    assert mock_node.status_requests > status_requests_before


def test_register_node_metadata_must_match_mock_node(client, mock_node):
    """A node claiming to be someone else is rejected."""
    response = client.post("/register/", json={
        "name": "impostor",
        "id": mock_node.id,
        "host": mock_node.host,
        "type": mock_node.type,
    })
    assert response.status_code == 403
    assert "larping" in response.get_json()["error"]


def test_register_existing_node_returns_ok(client, mock_node):
    register_node(client, mock_node)

    response = post_registration(client, mock_node)

    assert response.status_code == 200
    assert response.get_json() == {"message": "node already exists"}


def test_register_unreachable_node_is_rejected(client):
    """Nothing listens on this host, so the handshake must fail."""
    response = client.post("/register/", json={
        "name": "ghost-node",
        "id": "ghost-1",
        "host": "127.0.0.1:1",  # nothing listens on port 1
        "type": "worker",
    })
    assert response.status_code == 503
    assert response.get_json()["error"] == "Node not reachable"


def test_register_invalid_payload_is_rejected(client):
    response = client.post("/register/", json={"name": "incomplete"})
    assert response.status_code == 400
    assert response.get_json()["error"] == "Validation failed"


def test_register_non_json_body_is_rejected(client):
    response = client.post("/register/", data="not json",
                           content_type="text/plain")
    assert response.status_code == 400

