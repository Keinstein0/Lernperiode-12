from flask import Blueprint, jsonify, request
from pydantic import ValidationError

from models.task import TaskAssignRequestModel, TaskModel, TaskStatus

bp = Blueprint("client", __name__, url_prefix='/task')


@bp.route("/assign", methods=["POST"])
def postTask():
    json_data = request.get_json()

    if not json_data:
        return jsonify({"error": "Request body must be valid JSON"}), 400

    try:
        task_payload = TaskAssignRequestModel(**json_data)
    except ValidationError as e:
        return jsonify({
            "error": "Validation failed", 
            "details": e.errors()
        }), 400

    # assign task to node http request

    full_task = TaskModel(
        id = "12345", # new uuid, created from node response
        node_id = task_payload.node_id,
        ticket = task_payload.ticket,
        status = TaskStatus.IN_PROGRESS
    )

    # store in task map

    


@bp.route("/<string:id>", methods=["GET"])
def getTask(id):
    ...