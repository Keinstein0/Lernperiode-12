from flask import Blueprint, jsonify

bp = Blueprint("client", __name__)

from . import (register)

bp.register_blueprint(register.bp)