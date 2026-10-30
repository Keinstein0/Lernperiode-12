from flask import Blueprint, jsonify

bp = Blueprint("client", __name__)

from . import (auth, nodes, task)

bp.register_blueprint(auth.bp)
bp.register_blueprint(nodes.bp)
bp.register_blueprint(task.bp)