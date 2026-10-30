from pydantic import BaseModel
from enum import Enum
from pydantic import (BaseModel, Field)
from typing import Optional
import datetime

class NodeStatus(str, Enum):
    DONE = "done"
    IN_PROGRESS = "in progress"
    ERROR = "error"
    IDLE = "idle"

class NodeModel(BaseModel):
    name: str;
    id: str;
    type: str;

    status: NodeStatus;
    task_id: str;