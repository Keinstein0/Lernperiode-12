from flask import Blueprint, jsonify, request, re
from models.node import NodeStoreMetadataModel
from pydantic import ValidationError
from status import getNodeStatus


bp = Blueprint("client", __name__, url_prefix='/register')


@bp.route("/", methods=["POST"])
def register(): 
    json_data = request.get_json()

    # 1. Validate JSON
    if not json_data:
        return jsonify({"error": "Request body must be valid JSON"}), 400

    # 2. Validate JSON schema
    try:
        node_registration_request_payload = NodeStoreMetadataModel(**json_data)
    except ValidationError as e:
        return jsonify({
            "error": "Validation failed", 
            "details": e.errors()
        }), 400

    # 3. Validate node exists
    try:
        node_status = getNodeStatus(node_registration_request_payload)

    except:
        return jsonify({
            "error": "Node not reachable"
        }), 400

    # 4. Validate node metatada matches
    metadata_matches = node_registration_request_payload == NodeStoreMetadataModel.model_validate(node_status.model_dump())
    if not metadata_matches:
        return jsonify({
            "error": "Node larping as other node detected"
        }),

    # store node map
