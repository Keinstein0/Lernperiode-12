from flask import Blueprint, jsonify

bp = Blueprint("client", __name__, url_prefix='/task')

from . import (auth)


@bp.route("/assign", methods=["POST"])
def postTask():
    ...


@bp.route("/<string:id>", methods=["GET"])
def getTask(id):
    ...