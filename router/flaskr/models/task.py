from pydantic import BaseModel
from enum import Enum
from pydantic import (BaseModel, Field)
from typing import Optional
import datetime

# Define your status enumerator
class TaskStatus(str, Enum):
    DONE = "done"
    IN_PROGRESS = "in progress"
    ERROR = "error"

class Task(BaseModel):
    id: str
    node_id: str
    ticket: Ticket
    status: TaskStatus
    report: None | Report

class Report(BaseModel):
    finished_at: datetime = Field(default_factory=datetime.now)
    finished_by_node_id: str

    git_repo_url: str
    git_branch_id: str

    # eventual wonky parameters:
    # TODO

    description: str

class Ticket(BaseModel):
    created_at: datetime = Field(default_factory=datetime.now)
    created_by_github_user_id: str

    git_repo_url: str
    git_branch_id: str

    # eventual wonky parameters:
    # TODO

    description: str
