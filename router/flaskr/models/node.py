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

class TaskModel(BaseModel):
    id: str
    node_id: str
    ticket: TicketModel
    status: TaskStatus