from flask import Blueprint, jsonify

bp = Blueprint("client", __name__)

from . import (auth)
bp.register_blueprint(auth.bp)