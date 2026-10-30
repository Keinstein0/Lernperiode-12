from flask import Flask



def create_app(test_config=None):
    """Application factory: creates and configures the router app."""

    app = Flask(__name__)

    if test_config is None:
        # load the instance config, if it exists, when not testing
        app.config.from_pyfile('config.py', silent=True)
    else:
        # load the test config if passed in
        app.config.from_mapping(test_config)

    from . import (routes)
    from client import client
    # temporary debug bp
    app.register_blueprint(routes.bp)

    # client routes
    app.register_blueprint(client.bp)

    # node routes
    # TODO


    return app
