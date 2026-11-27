import { EventEmitter } from 'node:events';
import { randomUUID } from 'node:crypto';
import { isTerminal, store } from './store.js';
import type { Message, Part, TaskRecord, TaskState } from './types.js';

/**
 * Fakes a node working on tasks. Every task moves through the A2A states on a timer:
 *   SUBMITTED -> WORKING -> COMPLETED
 * Trigger words in the last user message change the outcome:
 *   contains "fail" -> FAILED
 *   contains "?"    -> INPUT_REQUIRED (continue with send_task + taskId)
 */

/** Time per simulated step in ms. Set MOCK_STEP_MS=200 for fast tests. */
const STEP_MS = Number(process.env.MOCK_STEP_MS ?? 2000);

export interface TaskUpdateEvent {
  taskId: string;
  record: TaskRecord;
  /** true once the task is terminal or waits for input (nothing more will happen on its own). */
  final: boolean;
}

/** Emits 'task-update' on every state change. Used by blocking send_task (and streaming later). */
export const taskEvents = new EventEmitter();
taskEvents.setMaxListeners(100);

const timers = new Map<string, NodeJS.Timeout[]>();

const now = () => new Date().toISOString();
export const newId = (prefix: string) => `${prefix}-${randomUUID().slice(0, 8)}`;

function textOf(parts: Part[]): string {
  return parts.map((p) => ('text' in p ? p.text : '')).join(' ');
}

function agentMessage(record: TaskRecord, text: string): Message {
  return {
    messageId: newId('msg'),
    role: 'agent',
    parts: [{ text }],
    taskId: record.task.id,
    contextId: record.task.contextId,
  };
}

function setState(record: TaskRecord, state: TaskState, message?: Message): void {
  record.task.status = { state, timestamp: now(), ...(message ? { message } : {}) };
  if (message) record.task.history.push(message);

  // Node is busy while it works on something and free again afterwards.
  const node = store.getNode(record.nodeId);
  if (node && node.status !== 'offline') {
    node.status = state === 'TASK_STATE_WORKING' || state === 'TASK_STATE_SUBMITTED' ? 'busy' : 'free';
    node.lastSeen = now();
  }

  store.saveTask(record);
  const final = isTerminal(state) || state === 'TASK_STATE_INPUT_REQUIRED';
  taskEvents.emit('task-update', { taskId: record.task.id, record, final } satisfies TaskUpdateEvent);
  if (final) clearTimers(record.task.id);
}

function schedule(taskId: string, delay: number, fn: () => void): void {
  const t = setTimeout(fn, delay);
  timers.set(taskId, [...(timers.get(taskId) ?? []), t]);
}

function clearTimers(taskId: string): void {
  for (const t of timers.get(taskId) ?? []) clearTimeout(t);
  timers.delete(taskId);
}

/** Starts (or resumes after INPUT_REQUIRED) the fake work on a task. */
export function simulate(record: TaskRecord): void {
  const taskId = record.task.id;
  const lastUser = [...record.task.history].reverse().find((m) => m.role === 'user');
  const text = lastUser ? textOf(lastUser.parts).toLowerCase() : '';

  if (record.task.status.state === 'TASK_STATE_INPUT_REQUIRED') {
    // Resumed after the user answered: go straight back to work.
    setState(record, 'TASK_STATE_WORKING');
  } else {
    setState(record, 'TASK_STATE_SUBMITTED');
    schedule(taskId, STEP_MS, () => setState(record, 'TASK_STATE_WORKING'));
  }
  schedule(taskId, STEP_MS * 3, () => {
    if (text.includes('fail')) {
      setState(record, 'TASK_STATE_FAILED', agentMessage(record, 'Simulated failure: the node could not finish the task.'));
    } else if (text.includes('?')) {
      setState(record, 'TASK_STATE_INPUT_REQUIRED', agentMessage(record, 'Simulated question: which branch should I base my work on?'));
    } else {
      record.task.artifacts.push({
        artifactId: newId('art'),
        name: 'result',
        parts: [
          { text: `Simulated result for: "${textOf(lastUser?.parts ?? [])}"` },
          {
            data: {
              gitRepoUrl: 'https://github.com/Keinstein0/Lernperiode-12',
              gitBranch: `node/${record.nodeId}/${taskId}`,
              filesOfInterest: ['README.md'],
            },
          },
        ],
      });
      setState(record, 'TASK_STATE_COMPLETED', agentMessage(record, 'Done. Results are in the artifacts.'));
    }
  });
}

/** Stops the fake work and marks the task as canceled. */
export function cancel(record: TaskRecord): void {
  clearTimers(record.task.id);
  setState(record, 'TASK_STATE_CANCELED');
}

/** Resolves once the task is final (terminal or INPUT_REQUIRED), or after timeoutMs. */
export function waitUntilFinal(taskId: string, timeoutMs = STEP_MS * 5): Promise<void> {
  const current = store.getTaskRecord(taskId);
  if (current && (isTerminal(current.task.status.state) || current.task.status.state === 'TASK_STATE_INPUT_REQUIRED')) {
    return Promise.resolve();
  }
  return new Promise((resolve) => {
    const onUpdate = (e: TaskUpdateEvent) => {
      if (e.taskId === taskId && e.final) done();
    };
    const timer = setTimeout(() => done(), timeoutMs);
    function done() {
      clearTimeout(timer);
      taskEvents.off('task-update', onUpdate);
      resolve();
    }
    taskEvents.on('task-update', onUpdate);
  });
}
