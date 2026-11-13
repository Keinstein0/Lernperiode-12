from pydantic import BaseModel, ConfigDict


def to_kebab(name: str) -> str:
    """git_repo_url -> git-repo-url"""
    return name.replace("_", "-")


class WireModel(BaseModel):
    """Base for every model that is sent or received over HTTP.

    Python code uses snake_case field names; the JSON on the wire uses
    kebab-case keys, as defined in interactions.md. Incoming JSON may use
    either form (populate_by_name), outgoing JSON is always kebab-case.
    """

    model_config = ConfigDict(alias_generator=to_kebab, populate_by_name=True)

    def to_json(self) -> dict:
        """JSON-ready dict with kebab-case keys (datetimes as ISO strings)."""
        return self.model_dump(by_alias=True, mode="json")
