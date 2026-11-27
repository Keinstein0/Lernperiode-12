import { z } from 'zod';
import type { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { isTerminal, store, withHistoryLength } from '../store.js';
import { cancel, newId, simulate, waitUntilFinal } from '../simulator.js';
import type { TaskRecord, TaskState } from '../types.js';
import { fail, ok } from './result.js';

const TASK_STATES = [
  'TASK_STATE_SUBMITTED',
  'TASK_STATE_WORKING',
  'TASK_STATE_INPUT_REQUIRED',
  'TASK_STATE_COMPLETED',
  'TASK_STATE_FAILED',
  'TASK_STATE_CANCELED',
] as const satisfies readonly TaskState[];

const partSchema = z.union([
  z.object({ text: z.string() }),
  z.object({ data: z.record(z.string(), z.unknown()) }),
]);

const messageSchema = z.object({
  parts: z.array(partSchema).min(1).describe('Message content, e.g. [{ "text": "Fix the login bug" }].'),
  taskId: z.string().optional().describe('Continue this task (must be in TASK_STATE_INPUT_REQUIRED).'),
  contextId: z.string().optional().describe('Start the new task in this existing context (conversation thread).'),
});

const configurationSchema = z
  .object({
    returnImmediately: z
      .boolean()
      .optional()
      .describe('true = return right after submitting. false (default) = wait until the task is done or needs input.'),
    historyLength: z.number().int().min(0).optional().describe('How many history messages to return.'),
    acceptedOutputModes: z.array(z.string()).optional().describe('Accepted output media types (ignored by the mock).'),
  })
  .optional();

/** Router response shape: the A2A task plus the node it was routed to. */
function view(record: TaskRecord, historyLength?: number, includeArtifacts = true) {
  const task = withHistoryLength(record.task, historyLength);
  return { nodeId: record.nodeId, task: includeArtifacts ? task : { ...task, artifacts: [] } };
}

export function registerTaskTools(server: McpServer): void {
  server.registerTool(
    'send_task',
    {
      title: 'Send task',
      description:
        'Sends a message to a node. Starts a new task, or continues one that is in TASK_STATE_INPUT_REQUIRED ' +
        '(pass message.taskId). Mock: text containing "fail" ends in FAILED, text containing "?" ends in INPUT_REQUIRED.',
      inputSchema: {
        node_id: z.string().describe('Node that should do the task.'),
        message: messageSchema,
        configuration: configurationSchema,
      },
    },
    async ({ node_id, message, configuration }) => {
      const node = store.getNode(node_id);
      if (!node) return fail('NODE_NOT_FOUND', `No node with id "${node_id}".`);
      if (node.status === 'offline') return fail('NODE_UNREACHABLE', `Node "${node_id}" is offline.`);

      let record: TaskRecord;

      if (message.taskId) {
        // Continue an existing task.
        const existing = store.getTaskRecord(message.taskId);
        if (!existing) return fail('TASK_NOT_FOUND', `No task with id "${message.taskId}".`);
        if (existing.nodeId !== node_id) {
          return fail('TASK_NODE_MISMATCH', `Task "${message.taskId}" belongs to node "${existing.nodeId}".`);
        }
        if (existing.task.status.state !== 'TASK_STATE_INPUT_REQUIRED') {
          return fail('TASK_NOT_CONTINUABLE', `Task "${message.taskId}" is ${existing.task.status.state}, not waiting for input.`);
        }
        record = existing;
      } else {
        // New task: the node must not be busy with something else.
        if (node.status === 'busy') return fail('NODE_BUSY', `Node "${node_id}" is busy with another task.`);
        const taskId = newId('task');
        record = {
          nodeId: node_id,
          createdAt: new Date().toISOString(),
          task: {
            id: taskId,
            contextId: message.contextId ?? newId('ctx'),
            status: { state: 'TASK_STATE_SUBMITTED', timestamp: new Date().toISOString() },
            history: [],
            artifacts: [],
          },
        };
      }

      record.task.history.push({
        messageId: newId('msg'),
        role: 'user',
        parts: message.parts,
        taskId: record.task.id,
        contextId: record.task.contextId,
      });
      simulate(record);

      if (!configuration?.returnImmediately) await waitUntilFinal(record.task.id);
      return ok(view(record, configuration?.historyLength));
    },
  );

  server.registerTool(
    'get_task',
    {
      title: 'Get task',
      description: 'Returns the current state, artifacts and message history of a task.',
      inputSchema: {
        task_id: z.string().describe('ID of the task.'),
        historyLength: z.number().int().min(0).optional().describe('How many history messages to return.'),
      },
      annotations: { readOnlyHint: true },
    },
    async ({ task_id, historyLength }) => {
      const record = store.getTaskRecord(task_id);
      if (!record) return fail('TASK_NOT_FOUND', `No task with id "${task_id}".`);
      return ok(view(record, historyLength));
    },
  );

  server.registerTool(
    'get_tasks',
    {
      title: 'List tasks',
      description: 'Lists known tasks, newest first. Supports filtering and paging.',
      inputSchema: {
        contextId: z.string().optional().describe('Only tasks in this context.'),
        status: z.enum(TASK_STATES).optional().describe('Only tasks in this state.'),
        pageSize: z.number().int().min(1).max(100).optional().describe('Tasks per page (default 20).'),
        pageToken: z.string().optional().describe('nextPageToken from the previous call.'),
        includeArtifacts: z.boolean().optional().describe('Include artifacts (default false).'),
      },
      annotations: { readOnlyHint: true },
    },
    async ({ contextId, status, pageSize = 20, pageToken, includeArtifacts = false }) => {
      const all = store.listTasks({ contextId, state: status });
      const offset = pageToken ? Number(pageToken) : 0;
      if (!Number.isInteger(offset) || offset < 0) return fail('INVALID_PAGE_TOKEN', `Invalid pageToken "${pageToken}".`);

      const page = all.slice(offset, offset + pageSize);
      const next = offset + pageSize < all.length ? String(offset + pageSize) : undefined;
      return ok({
        tasks: page.map((r) => view(r, undefined, includeArtifacts)),
        totalSize: all.length,
        ...(next ? { nextPageToken: next } : {}),
      });
    },
  );

  server.registerTool(
    'get_contexts',
    {
      title: 'List contexts',
      description: 'Lists known contexts (conversation threads) with their tasks and nodes.',
      inputSchema: {
        node_id: z.string().optional().describe('Only contexts with tasks on this node.'),
        active_only: z.boolean().optional().describe('Only contexts that still have an unfinished task.'),
      },
      annotations: { readOnlyHint: true },
    },
    async ({ node_id, active_only }) => {
      return ok({ contexts: store.listContexts({ nodeId: node_id, activeOnly: active_only }) });
    },
  );

  server.registerTool(
    'cancel_task',
    {
      title: 'Cancel task',
      description: 'Cancels a task that is still running or waiting for input.',
      inputSchema: {
        task_id: z.string().describe('ID of the task.'),
      },
      annotations: { destructiveHint: true },
    },
    async ({ task_id }) => {
      const record = store.getTaskRecord(task_id);
      if (!record) return fail('TASK_NOT_FOUND', `No task with id "${task_id}".`);
      if (isTerminal(record.task.status.state)) {
        return fail('TASK_NOT_CANCELABLE', `Task "${task_id}" is already ${record.task.status.state}.`);
      }
      cancel(record);
      return ok(view(record));
    },
  );
}
