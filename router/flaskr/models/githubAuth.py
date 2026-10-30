from pydantic import BaseModel

class GitHubCallbackQuery(BaseModel):
    code: str
    state: str = None

class GitHubTokenResponse(BaseModel):
    access_token: str
    token_type: str
    scope: str