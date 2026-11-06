from flask import Blueprint

bp = Blueprint("client", __name__)

from . import auth, nodes, task  # noqa: E402

bp.register_blueprint(auth.bp)
bp.register_blueprint(nodes.bp)
bp.register_blueprint(task.bp)