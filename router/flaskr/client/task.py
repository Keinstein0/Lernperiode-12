from flask import Blueprint, jsonify, request, current_app
from pydantic import ValidationError

from models.task import TaskAssignRequestModel, TaskModel, TaskStatus
from node.ticket import assignTicket

from store.baseTaskRepository import BaseTaskRepository

from node.status import getNodeStatusById
from models.node import NodeStatus

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


    full_task = TaskModel(
        id = "12345", # new uuid, created from node response
        node_id = task_payload.node_id,
        ticket = task_payload.ticket,
        status = TaskStatus.IN_PROGRESS
    )

    # store in task map
    try:
        task_repo: BaseTaskRepository = current_app.extensions['task_repo']
        task_repo.save(full_task)
    except Exception as ex: #TODO maybe cancel task?
        return jsonify({
            "error": "database failure",
            "exception": str(ex)
        }), 500

    # assign task to node http request
    try:
        assignTicket(full_task)
    except Exception as ex:
        return jsonify({
            "error": "assign failure",
            "exception": str(ex)
        }), 502

    return jsonify({
        "status": "assigned",
        "task": full_task.model_dump()
    }, 201)




    


@bp.route("/<string:id>", methods=["GET"])
def getTask(id):
    try:
        task_repo: BaseTaskRepository = current_app.extensions['task_repo']
        task = task_repo.get(id)
    except Exception as ex: #TODO maybe cancel task?
        return jsonify({
            "error": "database failure",
            "exception": str(ex)
        }), 500

    status = getNodeStatusById(task.node_id)
    if (status.task_id != task.id): # TODO clarify this funky edge case
        return jsonify({
            "error": "task no longer active and can therefore no longer be retrieved",
        }), 404


    fullTask = TaskModel(
        id=task.id,
        node_id=task.node_id,
        ticket=task.ticket,
        status=convertNodeStatusToTaskStatus(status.status),
        report=status.report,
    )



def convertNodeStatusToTaskStatus(status : NodeStatus) -> TaskStatus:
    ...