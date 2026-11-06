from flask import current_app

from ..models.node import NodeStatusRequestResponse
from ..store.baseNodeRepository import BaseNodeRepository

def getNodeStatusById(node_id: str) -> NodeStatusRequestResponse:
    node_repo : BaseNodeRepository = current_app.extensions['node_repo']

    node = node_repo.get(node_id)
    if not node:
        raise ValueError("Requested node does not exist in store")

    return getNodeStatus(node.host)


def getNodeStatus(node_host: str) -> NodeStatusRequestResponse:
    """Queries the node at `node_host` for its current status."""
    import requests  # noqa: PLC0415  (kept local: only needed once probing is implemented)
    response = requests.get(f"http://{node_host}/status", timeout=5)
    response.raise_for_status()
    return NodeStatusRequestResponse.model_validate(response.json())