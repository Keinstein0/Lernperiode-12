from models.node import NodeStatusRequestResponse
from store.baseNodeRepository import BaseNodeRepository
from flask import current_app

def getNodeStatusById(node_id: str) -> NodeStatusRequestResponse:
    node_repo : BaseNodeRepository = current_app.extensions['node_repo']

    node = node_repo.get(node_id)
    if not node:
        raise Exception("Requested node does not exist in store")

    return getNodeStatus(node.host)


def getNodeStatus(node_host: str) -> NodeStatusRequestResponse:
    ...