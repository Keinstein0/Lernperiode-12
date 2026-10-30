from flask import Blueprint, jsonify, re
from models.node import NodeModel, NodeStatus


bp = Blueprint("client", __name__, url_prefix='/node')


@bp.route("/", methods=["GET"])
def getNodes(): 
    nodes = [
        NodeModel(
            name = "testnode 1",
            id = "1",
            type = "keinstein0:mocknode",
            status = NodeStatus.IDLE,
            task_id = "1"
        ),
        NodeModel(
            name = "testnode 2",
            id = "2",
            type = "keinstein0:mocknode",
            status = NodeStatus.IDLE,
            task_id = "2"
        ),
        NodeModel(
            name = "testnode 3",
            id = "3",
            type = "keinstein0:mocknode",
            status = NodeStatus.IDLE,
            task_id = "3"
        )
    ]

    return jsonify([node.model_dump() for node in nodes]), 200

@bp.route("/<string:id>", methods=["GET"])
def getNode(id):
    node = NodeModel(
        name = "testnode 1",
        id = "1",
        type = "keinstein0:mocknode",
        status = NodeStatus.IDLE,
        task_id = "1"
    ),

    return jsonify(node), 200