from pydantic import BaseModel


class AuthResponse(BaseModel):
    access_token: str
    token_type: str = "Bearer"
    expires_in: int = 360000 # TODO shorten and implement refresh token logic