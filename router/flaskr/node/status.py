from models.node import NodeStatusRequestResponse


def getNodeStatusById(node_id: str) -> NodeStatusRequestResponse:
    ...


def getNodeStatus(host : str) -> NodeStatusRequestResponse:
    ...