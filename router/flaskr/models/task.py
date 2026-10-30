from __future__ import annotations  # Allows forward references for type hints

from datetime import datetime
from enum import Enum
from pydantic import BaseModel, Field


class TaskStatus(str, Enum):
    DONE = "done"
    IN_PROGRESS = "in progress"
    ERROR = "error"


class TicketModel(BaseModel):
    created_at: datetime = Field(default_factory=datetime.now)
    created_by_github_user_id: str
    git_repo_url: str
    git_branch_id: str
    description: str


class ReportModel(BaseModel):
    finished_at: datetime = Field(default_factory=datetime.now)
    finished_by_node_id: str
    git_repo_url: str
    git_branch_id: str
    description: str


class TaskModel(BaseModel):
    id: str
    node_id: str
    ticket: TicketModel
    status: TaskStatus
    report: ReportModel | None = None  # Standard convention: type | None = default