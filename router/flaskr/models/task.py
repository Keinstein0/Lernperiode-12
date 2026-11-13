from __future__ import annotations  # Allows forward references for type hints

from datetime import datetime, timezone
from enum import Enum
from pydantic import Field

from .base import WireModel


class TaskStatus(str, Enum):
    DONE = "done"
    BUSY = "busy"
    ERROR = "error"


class TicketModel(WireModel):
    created_at: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))
    created_by: str
    git_repo_url: str
    git_branch_id: str | None = None  # None: the node creates its own branch
    files_of_interest: list[str] = Field(default_factory=list)
    description: str


class ReportModel(WireModel):
    finished_at: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))
    finished_by_node_id: str
    git_repo_url: str
    git_branch_id: str
    files_of_interest: list[str] = Field(default_factory=list)
    description: str



class TaskStoreModel(WireModel):
    id: str
    node_id: str
    ticket: TicketModel

# Task as seen for the client
class TaskModel(TaskStoreModel):
    status: TaskStatus
    report: ReportModel | None = None


class TaskAssignRequestModel(WireModel):
    node_id: str
    ticket: TicketModel