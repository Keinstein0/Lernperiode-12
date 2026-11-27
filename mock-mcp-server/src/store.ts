import { seedNodes, seedTasks } from './seed.js';
import { TERMINAL_STATES, type RegisteredNode, type Task, type TaskRecord, type TaskState } from './types.js';

/**
 * In-memory state shared by every MCP request. Lives as long as the process;
 * restarting the server resets everything to the seed data.
 */
class MockStore {
  private nodes = new Map<string, RegisteredNode>();
  /** Router routing map: taskId -> record (incl. nodeId). */
  private tasks = new Map<string, TaskRecord>();

  constructor() {
    this.reset();
  }

  reset(): void {
    this.nodes = new Map(seedNodes().map((n) => [n.nodeId, n]));
    this.tasks = new Map(seedTasks().map((r) => [r.task.id, r]));
  }

  // ---- Nodes ----------------------------------------------------------

  listNodes(): RegisteredNode[] {
    return [...this.nodes.values()];
  }

  getNode(nodeId: string): RegisteredNode | undefined {
    return this.nodes.get(nodeId);
  }

  // ---- Tasks ----------------------------------------------------------

  getTaskRecord(taskId: string): TaskRecord | undefined {
    return this.tasks.get(taskId);
  }

  listTasks(filter: { contextId?: string; state?: TaskState; nodeId?: string } = {}): TaskRecord[] {
    return [...this.tasks.values()]
      .filter((r) => !filter.contextId || r.task.contextId === filter.contextId)
      .filter((r) => !filter.state || r.task.status.state === filter.state)
      .filter((r) => !filter.nodeId || r.nodeId === filter.nodeId)
      .sort((a, b) => b.createdAt.localeCompare(a.createdAt)); // newest first
  }

  /** Context = conversation thread. Derived from the tasks that use it. */
  listContexts(filter: { nodeId?: string; activeOnly?: boolean } = {}) {
    const byContext = new Map<string, { contextId: string; nodeIds: Set<string>; taskIds: string[]; active: boolean }>();
    for (const r of this.listTasks({ nodeId: filter.nodeId })) {
      const entry = byContext.get(r.task.contextId) ?? {
        contextId: r.task.contextId,
        nodeIds: new Set<string>(),
        taskIds: [],
        active: false,
      };
      entry.nodeIds.add(r.nodeId);
      entry.taskIds.push(r.task.id);
      entry.active ||= !isTerminal(r.task.status.state);
      byContext.set(r.task.contextId, entry);
    }
    return [...byContext.values()]
      .filter((c) => !filter.activeOnly || c.active)
      .map((c) => ({ ...c, nodeIds: [...c.nodeIds] }));
  }

  saveTask(record: TaskRecord): void {
    this.tasks.set(record.task.id, record);
  }
}

export function isTerminal(state: TaskState): boolean {
  return TERMINAL_STATES.includes(state);
}

/** Trims a task's history to the last `historyLength` messages (A2A semantics). */
export function withHistoryLength(task: Task, historyLength?: number): Task {
  if (historyLength === undefined) return task;
  return { ...task, history: historyLength <= 0 ? [] : task.history.slice(-historyLength) };
}

/** Single shared instance (module singleton). */
export const store = new MockStore();
