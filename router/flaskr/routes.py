from flask import Blueprint, jsonify

bp = Blueprint("api", __name__)


@bp.route("/")
def hello_world():
    return "<p>Hello, World!</p>"


# --- Node registration -------------------------------------------------
# POST /register, see interactions.md "register"
@bp.route("/register", methods=["POST"])
def register():
    return jsonify({"status": "not implemented"}), 501


# --- Status polling ----------------------------------------------------
# GET /status, see interactions.md "get-status"
@bp.route("/status", methods=["GET"])
def status():
    return jsonify({"status": "not implemented"}), 501


# --- Ticket assignment -------------------------------------------------
# POST /ticket, see interactions.md "ticket"
@bp.route("/ticket", methods=["POST"])
def ticket():
    return jsonify({"status": "not implemented"}), 501
