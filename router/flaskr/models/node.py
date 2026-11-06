from pydantic import BaseModel, Field
from enum import Enum
from .task import ReportModel

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
    task_id: str | None = None

    @classmethod
    def from_store(cls, node: NodeStoreMetadataModel, status: NodeStatus, task_id: str | None = None) -> "NodeModel":
        return cls(
            name=node.name,
            id=node.id,
            type=node.type,
            status=status,
            task_id=task_id
        )

# Node internal store model
class NodeStoreMetadataModel(BaseModel):
    name: str
    id: str
    host: str
    type: str

# Node response to status request
class NodeStatusRequestResponse(NodeStoreMetadataModel):
    status: NodeStatus
    task_id: str | None = None
    report: ReportModel | None = None
