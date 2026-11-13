from flask import Blueprint

bp = Blueprint("api", __name__)


@bp.route("/")
def hello_world():
    return "<p>Hello, World!</p>"

# The node-facing endpoints live in flaskr/node/ and the client-facing ones in
# flaskr/client/. (The old 501 stubs for /register, /status and /ticket were
# removed: POST /register hit the stub instead of the real registration.)
