from enum import Enum

from .base import WireModel
from .task import ReportModel

class NodeStatus(str, Enum):
    DONE = "done"
    BUSY = "busy"
    ERROR = "error"
    IDLE = "idle"
    UNREACHABLE = "unreachable" # status fetching impossible, node returned an error

# Node internal store model
class NodeStoreMetadataModel(WireModel):
    name: str
    id: str
    host: str
    type: str

# Node as seen for the client
class NodeModel(WireModel):
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

# Node response to status request
class NodeStatusRequestResponse(NodeStoreMetadataModel):
    status: NodeStatus
    task_id: str | None = None
    report: ReportModel | None = None
