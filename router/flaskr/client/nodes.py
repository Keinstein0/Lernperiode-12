from flask import Blueprint, jsonify

bp = Blueprint("client", __name__, url_prefix='/node')


@bp.route("/", methods=["GET"])
def getNodes():
    ...


@bp.route("/<string:id>", methods=["GET"])
def getNode(id):
    ...