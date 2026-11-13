from flask import Blueprint, jsonify
from flask import current_app
from ..models.node import NodeModel, NodeStatus, NodeStatusRequestResponse
from ..store.baseNodeRepository import BaseNodeRepository
from ..node.status import getNodeStatusById, getNodeStatus



bp = Blueprint("nodes", __name__, url_prefix='/node')


@bp.route("/", methods=["GET"])
def getNodes(): # TODO make async eventually
    node_repo: BaseNodeRepository = current_app.extensions['node_repo']
    stored_nodes = node_repo.get_all()
    
    response_nodes = []
    
    for node in stored_nodes:
        try:
            status = getNodeStatus(node.host)
        except Exception:
            # Fallback if node is unreachable or probing is not implemented.
            status = NodeStatusRequestResponse(
                name=node.name,
                id=node.id,
                host=node.host,
                type=node.type,
                status=NodeStatus.UNREACHABLE,
                task_id=None
            )
        
        node_model = NodeModel.from_store(node, status.status, status.task_id)
        response_nodes.append(node_model.to_json())

    return jsonify(response_nodes), 200

@bp.route("/<string:id>", methods=["GET"])
def getNode(id):
    node_repo: BaseNodeRepository = current_app.extensions['node_repo']

    node = node_repo.get(id)
    if not node:
        return jsonify("node not found"), 404

    try:
        status = getNodeStatus(node.host)
    except Exception:
        # Fallback if node is unreachable or probing is not implemented.
        status = NodeStatusRequestResponse(
            name=node.name,
            id=node.id,
            host=node.host,
            type=node.type,
            status=NodeStatus.UNREACHABLE,
            task_id=None
        )

    node_model = NodeModel.from_store(node, status.status, status.task_id)

    return jsonify(node_model.to_json()), 200