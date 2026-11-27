import { z } from 'zod';
import type { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { isTerminal, store } from '../store.js';
import { cancel } from '../simulator.js';
import type { NodeAuth } from '../types.js';
import { fail, ok } from './result.js';

/**
 * Fake caller roles, read from the Authorization header:
 *   (no header)              -> admin  (convenient for local development)
 *   Bearer mock-admin        -> admin
 *   anything else            -> user   (admin tools return FORBIDDEN)
 */
type Headers = Record<string, string | string[] | undefined>;

function isAdminCaller(headers: Headers | undefined): boolean {
  const raw = headers?.authorization;
  const value = Array.isArray(raw) ? raw[0] : raw;
  if (!value) return true;
  return value.trim() === 'Bearer mock-admin';
}

const FORBIDDEN = () => fail('FORBIDDEN', 'Admin role required. Use "Authorization: Bearer mock-admin" or no header.');
const CONFIRM_REQUIRED = (action: string) =>
  fail('CONFIRMATION_REQUIRED', `${action} is irreversible. Call again with confirm: true.`);

/** Auth record joined with registry info, as an admin sees it. */
function adminView(a: NodeAuth) {
  const node = store.getNode(a.nodeId);
  return {
    nodeId: a.nodeId,
    name: node?.agentCard.name ?? null,
    status: node?.status ?? null,
    isAdmin: a.isAdmin,
    allowedNodes: a.allowedNodes,
  };
}

export function registerAdminTools(server: McpServer): void {
  server.registerTool(
    'admin_list_nodes',
    {
      title: 'Admin: list nodes',
      description: 'Admin only. Lists all node identities and their permissions.',
      inputSchema: {
        filter: z.string().optional().describe('Only nodes whose id or name contains this text.'),
      },
      annotations: { readOnlyHint: true },
    },
    async ({ filter }, extra) => {
      if (!isAdminCaller(extra.requestInfo?.headers)) return FORBIDDEN();
      const f = filter?.toLowerCase();
      const nodes = store
        .listAuth()
        .map(adminView)
        .filter((n) => !f || n.nodeId.toLowerCase().includes(f) || (n.name ?? '').toLowerCase().includes(f));
      return ok({ nodes });
    },
  );

  server.registerTool(
    'admin_get_node',
    {
      title: 'Admin: get node',
      description: "Admin only. Returns one node's identity and permissions.",
      inputSchema: {
        target_node_id: z.string().describe('ID of the node.'),
      },
      annotations: { readOnlyHint: true },
    },
    async ({ target_node_id }, extra) => {
      if (!isAdminCaller(extra.requestInfo?.headers)) return FORBIDDEN();
      const a = store.getAuth(target_node_id);
      if (!a) return fail('NODE_NOT_FOUND', `No node with id "${target_node_id}".`);
      return ok(adminView(a));
    },
  );

  server.registerTool(
    'admin_update_node_permissions',
    {
      title: 'Admin: update node permissions',
      description: 'Admin only. Adds and/or removes target nodes that a node is allowed to use.',
      inputSchema: {
        target_node_id: z.string().describe('Node whose permissions change.'),
        add_allowed_nodes: z.array(z.string()).optional().describe('Node ids to allow.'),
        remove_allowed_nodes: z.array(z.string()).optional().describe('Node ids to disallow.'),
      },
    },
    async ({ target_node_id, add_allowed_nodes = [], remove_allowed_nodes = [] }, extra) => {
      if (!isAdminCaller(extra.requestInfo?.headers)) return FORBIDDEN();
      const a = store.getAuth(target_node_id);
      if (!a) return fail('NODE_NOT_FOUND', `No node with id "${target_node_id}".`);

      const unknown = add_allowed_nodes.filter((id) => !store.getAuth(id));
      if (unknown.length) return fail('NODE_NOT_FOUND', `Unknown node ids: ${unknown.join(', ')}.`);
      if (add_allowed_nodes.includes(target_node_id)) {
        return fail('INVALID_PERMISSION', 'A node cannot be given permission on itself.');
      }

      const allowed = new Set(a.allowedNodes);
      add_allowed_nodes.forEach((id) => allowed.add(id));
      remove_allowed_nodes.forEach((id) => allowed.delete(id));
      a.allowedNodes = [...allowed];
      return ok(adminView(a));
    },
  );

  server.registerTool(
    'admin_promote_node',
    {
      title: 'Admin: promote node',
      description: 'Admin only. Grants admin privileges to a node. Requires confirm: true.',
      inputSchema: {
        target_node_id: z.string().describe('Node to promote.'),
        confirm: z.boolean().describe('Must be true. Safety check against accidental promotion.'),
      },
      annotations: { destructiveHint: true },
    },
    async ({ target_node_id, confirm }, extra) => {
      if (!isAdminCaller(extra.requestInfo?.headers)) return FORBIDDEN();
      if (!confirm) return CONFIRM_REQUIRED('Promoting a node to admin');
      const a = store.getAuth(target_node_id);
      if (!a) return fail('NODE_NOT_FOUND', `No node with id "${target_node_id}".`);
      a.isAdmin = true;
      return ok(adminView(a));
    },
  );

  server.registerTool(
    'admin_delete_node',
    {
      title: 'Admin: delete node',
      description:
        'Admin only. Revokes a node identity, removes it from the registry and cancels its running tasks. ' +
        'Requires confirm: true.',
      inputSchema: {
        target_node_id: z.string().describe('Node to delete.'),
        confirm: z.boolean().describe('Must be true. Safety check against accidental deletion.'),
      },
      annotations: { destructiveHint: true },
    },
    async ({ target_node_id, confirm }, extra) => {
      if (!isAdminCaller(extra.requestInfo?.headers)) return FORBIDDEN();
      if (!confirm) return CONFIRM_REQUIRED('Deleting a node');
      if (!store.getAuth(target_node_id)) return fail('NODE_NOT_FOUND', `No node with id "${target_node_id}".`);

      const canceledTaskIds: string[] = [];
      for (const r of store.listTasks({ nodeId: target_node_id })) {
        if (!isTerminal(r.task.status.state)) {
          cancel(r);
          canceledTaskIds.push(r.task.id);
        }
      }
      store.deleteNode(target_node_id);
      return ok({ deleted: target_node_id, canceledTaskIds });
    },
  );
}
