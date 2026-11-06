from pydantic import BaseModel

class GitHubCallbackQuery(BaseModel):
    code: str
    state: str | None = None

class GitHubTokenResponse(BaseModel):
    access_token: str
    token_type: str = "bearer"
    scope: str = ""