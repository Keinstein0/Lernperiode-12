from flask import Blueprint, jsonify, request, current_app
from pydantic import ValidationError

from ..models.node import NodeStoreMetadataModel
from ..store.baseNodeRepository import BaseNodeRepository
from .status import getNodeStatus


bp = Blueprint("register", __name__, url_prefix='/register')


# strict_slashes=False: both /register and /register/ work (a 308 redirect
# would make the node's HTTP client fail instead of following it)
@bp.route("/", methods=["POST"], strict_slashes=False)
def register():
    # 1. Validate JSON
    json_data = request.get_json(silent=True)
    if not json_data:
        return jsonify({"error": "Request body must be valid JSON"}), 400

    # 2. Validate JSON schema
    try:
        node_registration_request_payload = NodeStoreMetadataModel.model_validate(json_data)
    except ValidationError as e:
        return jsonify({
            "error": "Validation failed",
            "details": e.errors()
        }), 400

    # 3. Validate node exists
    try:
        node_status = getNodeStatus(node_registration_request_payload.host)
    except NotImplementedError:
        # Status probing not implemented yet: accept registration without
        # verifying the node is reachable or that metadata matches.
        node_status = None
    except Exception:
        return jsonify({
            "error": "Node not reachable"
        }), 503

    # 4. Validate node metadata matches
    if node_status is not None:
        metadata_matches = node_registration_request_payload == NodeStoreMetadataModel.model_validate(node_status.model_dump())
        if not metadata_matches:
            return jsonify({
                "error": "Node larping as other node detected"
            }), 403

    # 5. Check if already exists / 6. Put into repo if not exists
    node_repo : BaseNodeRepository = current_app.extensions['node_repo']

    try:
        existing = node_repo.get(node_registration_request_payload.id)
        if (existing):
            return jsonify({
                "message" : "node already exists"
            }), 200

        node_repo.save(node_registration_request_payload)
        return jsonify(
            node_registration_request_payload.to_json()
        ), 201

    except Exception as ex:
        return jsonify({
            "error": "database failure",
            "exception": str(ex)
        }), 500
