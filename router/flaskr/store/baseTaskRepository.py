from abc import ABC, abstractmethod
from typing import Optional
from models.task import TaskModel

class BaseTaskRepository(ABC):
    """The interface that any storage backend must implement."""
    
    @abstractmethod
    def save(self, task: TaskModel) -> None:
        pass

    @abstractmethod
    def get(self, task_id: str) -> Optional[TaskModel]:
        pass

    @abstractmethod
    def get_all(self) -> list[TaskModel]:
        pass



class InMemoryTaskRepository(BaseTaskRepository):
    """An in-memory storage backend using a standard dictionary."""
    
    def __init__(self):
        # Key: node.id, Value: TaskModel
        self._storage: dict[str, TaskModel] = {}

    def save(self, task: TaskModel) -> None:
        self._storage[task.id] = task

    def get(self, task_id: str) -> Optional[TaskModel]:
        return self._storage.get(task_id)

    def get_all(self) -> list[TaskModel]:
        return list(self._storage.values())