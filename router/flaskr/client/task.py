from uuid import uuid4

from flask import Blueprint, jsonify, request, current_app
from pydantic import ValidationError

from ..models.task import TaskAssignRequestModel, TaskModel, TaskStatus
from ..models.node import NodeStatus
from ..node.ticket import assignTicket
from ..node.status import getNodeStatusById
from ..store.baseTaskRepository import BaseTaskRepository

bp = Blueprint("task", __name__, url_prefix='/task')


@bp.route("/", methods=["GET"])
def getTasks():
    try:
        task_repo: BaseTaskRepository = current_app.extensions['task_repo']
        tasks = task_repo.get_all()
    except Exception as ex:
        return jsonify({
            "error": "database failure",
            "exception": str(ex)
        }), 500

    return jsonify([task.to_json() for task in tasks]), 200


@bp.route("/assign", methods=["POST"])
def postTask():
    json_data = request.get_json(silent=True)

    if not json_data:
        return jsonify({"error": "Request body must be valid JSON"}), 400

    try:
        task_payload = TaskAssignRequestModel.model_validate(json_data)
    except ValidationError as e:
        return jsonify({
            "error": "Validation failed", 
            "details": e.errors()
        }), 400


    # verify the target node exists and is available for work
    try:
        node_status = getNodeStatusById(task_payload.node_id)
    except ValueError:
        return jsonify({"error": "node does not exist"}), 404
    except Exception as ex:
        return jsonify({
            "error": "node is unreachable",
            "exception": str(ex)
        }), 503

    if node_status.status == NodeStatus.BUSY:
        return jsonify({"error": "node is busy"}), 409
    if node_status.status == NodeStatus.UNREACHABLE:
        return jsonify({"error": "node is unavailable"}), 503

    full_task = TaskModel(
        id = str(uuid4()),
        node_id = task_payload.node_id,
        ticket = task_payload.ticket,
        status = TaskStatus.BUSY
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
    except NotImplementedError:
        pass  # ticket assignment not implemented yet; task stays queued in repo
    except Exception as ex:
        return jsonify({
            "error": "assign failure",
            "exception": str(ex)
        }), 502

    return jsonify({
        "status": "assigned",
        "task": full_task.to_json()
    }), 201




    


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


    if task is None:
        return jsonify({
            "error": "task not found",
        }), 404


    try:
        status = getNodeStatusById(task.node_id)
    except Exception as ex:
        return jsonify({
            "error": "failed to retrieve node status",
            "exception": str(ex)
        }), 502

    if (status.task_id != task.id): # TODO clarify this funky edge case
        return jsonify({
            "error": "task no longer active and can therefore no longer be retrieved",
        }), 404


    try:
        task_status = convertNodeStatusToTaskStatus(status.status)
    except ValueError as ex:
        return jsonify({
            "error": "node status cannot be mapped to a task status",
            "exception": str(ex)
        }), 502

    fullTask = TaskModel(
        id=task.id,
        node_id=task.node_id,
        ticket=task.ticket,
        status=task_status,
        report=status.report,
    )

    return jsonify(fullTask.to_json()), 200




def convertNodeStatusToTaskStatus(status : NodeStatus) -> TaskStatus:
    """Maps a node status to the corresponding task status.

    Raises:
        ValueError: for node states (IDLE, UNREACHABLE) that have no
            meaningful task status equivalent.
    """
    mapping = {
        NodeStatus.DONE: TaskStatus.DONE,
        NodeStatus.BUSY: TaskStatus.BUSY,
        NodeStatus.ERROR: TaskStatus.ERROR,
    }
    try:
        return mapping[NodeStatus(status)]
    except KeyError:
        raise ValueError(f"Node status '{status}' cannot be converted to a task status")