import functools

from flask import (
    Blueprint, flash, g, redirect, render_template, request, session, url_for
)

bp = Blueprint('auth', __name__, url_prefix='/auth')

GITHUB_CLIENT_ID = os.getenv("GITHUB_CLIENT_ID", "github_client_id")
GITHUB_AUTH_URL = "https://github.com/login/oauth/authorize"

@app.route("/github", methods=["GET"])
def login_with_github():

    redirect_uri = "keinstein0.ch" # TODO replace placeholder with actual uri

    github_login_url = (
        f"{GITHUB_AUTH_URL}?"
        f"client_id={GITHUB_CLIENT_ID}&"
        f"redirect_uri={redirect_uri}&"
        f"scope=user:email&"
        #f"state={state_token}" # TODO implement for more security
    )

    return redirect(github_login_url)
