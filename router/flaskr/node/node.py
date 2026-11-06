from flask import Blueprint

bp = Blueprint("node", __name__)

from . import register  # noqa: E402  (import after bp so register can import this package)

bp.register_blueprint(register.bp)