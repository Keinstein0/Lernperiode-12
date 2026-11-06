from abc import ABC, abstractmethod
from typing import Optional
# Assuming your NodeStoreModel is defined elsewhere
from models.node import NodeStoreMetadataModel

class BaseNodeRepository(ABC):
    """The interface that any storage backend must implement."""
    
    @abstractmethod
    def save(self, node: NodeStoreMetadataModel) -> None:
        pass

    @abstractmethod
    def get(self, node_id: str) -> Optional[NodeStoreMetadataModel]:
        pass

    @abstractmethod
    def get_all(self) -> list[NodeStoreMetadataModel]:
        pass



class InMemoryNodeRepository(BaseNodeRepository):
    """An in-memory storage backend using a standard dictionary."""
    
    def __init__(self):
        # Key: node.id, Value: NodeStoreModel
        self._storage: dict[str, NodeStoreMetadataModel] = {}

    def save(self, node: NodeStoreMetadataModel) -> None:
        self._storage[node.id] = node

    def get(self, node_id: str) -> Optional[NodeStoreMetadataModel]:
        return self._storage.get(node_id)

    def get_all(self) -> list[NodeStoreMetadataModel]:
        return list(self._storage.values())