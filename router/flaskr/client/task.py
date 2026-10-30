from flask import Blueprint, jsonify

bp = Blueprint("client", __name__, url_prefix='/task')


@bp.route("/assign", methods=["POST"])
def postTask():
    ...


@bp.route("/<string:id>", methods=["GET"])
def getTask(id):
    ...