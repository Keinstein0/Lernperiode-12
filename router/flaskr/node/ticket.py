import requests

from flask import current_app

from ..models.task import TaskModel
from ..store.baseNodeRepository import BaseNodeRepository


def assignTicket(task: TaskModel) -> None:
    """Pushes a task's ticket to the node it was assigned to.

    Raises:
        ValueError: if the task's node is not registered.
        requests.HTTPError: if the node rejects the ticket.
        requests.RequestException: if the node cannot be reached.
    """
    node_repo: BaseNodeRepository = current_app.extensions['node_repo']
    node = node_repo.get(task.node_id)
    if not node:
        raise ValueError(f"Node '{task.node_id}' is not registered")

    # The node checks that the identity block matches itself (403 if not),
    # see interactions.md "ticket"
    response = requests.post(
        f"http://{node.host}/ticket",
        json={
            **node.to_json(),          # name, id, host, type
            "task-id": task.id,
            "ticket": task.ticket.to_json(),
        },
        timeout=5,
    )
    response.raise_for_status()
