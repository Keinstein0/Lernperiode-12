from pydantic import BaseModel
from enum import Enum
from pydantic import (BaseModel, Field)
from typing import Optional
from task import ReportModel
import datetime

class NodeStatus(str, Enum):
    DONE = "done"
    IN_PROGRESS = "in progress"
    ERROR = "error"
    IDLE = "idle"
    UNREACHABLE = "unreachable" # status fetching impossible, node returned an error

# Node as seen for the client
class NodeModel(BaseModel):
    name: str
    id: str
    type: str

    status: NodeStatus
    task_id: str

# Node internal store model
class NodeStoreMetadataModel(BaseModel):
    name: str
    id: str
    host: str
    type: str

# Node response to status request
class NodeStatusRequestResponse(NodeStoreMetadataModel):
    status: NodeStatus
    task_id: Optional[str]
    report: Optional[ReportModel]
