import functools
import os

from flask import (
    Blueprint, flash, g, jsonify, redirect, render_template, request, session, url_for
)
from pydantic import ValidationError


from models.githubAuth import (GitHubCallbackQuery, GitHubTokenResponse)
from models.clientAuth import (AuthResponse)

bp = Blueprint('auth', __name__, url_prefix='/auth')

GITHUB_CLIENT_ID = os.getenv("GITHUB_CLIENT_ID", "github_client_id")
GITHUB_AUTH_URL = "https://github.com/login/oauth/authorize"

@bp.route("/github", methods=["GET"])
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



@bp.route("/github/callback", methods=["GET"])
def github_callback():
    try:
        # 1. Validate incoming query params from GitHub using Pydantic
        query_data = GitHubCallbackQuery(
            code=request.args.get('code'),
            state=request.args.get('state')
        )
    except ValidationError as e:
        return jsonify({"error": "Missing authorization code", "details": e.errors()}), 400


    # do some validation and stuff

    response = AuthResponse(
        access_token="imagine-a-token-here"
    )

    return jsonify(response)