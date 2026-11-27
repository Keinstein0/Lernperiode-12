import { z } from 'zod';
import type { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { store } from '../store.js';
import type { RegisteredNode } from '../types.js';
import { fail, ok } from './result.js';

// ---- Fake "embedding" search ------------------------------------------
// The real router embeds the AgentCard and ranks by cosine similarity.
// The mock does the same with plain word-count vectors, which is close
// enough to get sensible rankings for development.

function tokenize(text: string): string[] {
  return text.toLowerCase().match(/[a-z0-9]+/g) ?? [];
}

function termVector(tokens: string[]): Map<string, number> {
  const v = new Map<string, number>();
  for (const t of tokens) v.set(t, (v.get(t) ?? 0) + 1);
  return v;
}

function cosine(a: Map<string, number>, b: Map<string, number>): number {
  let dot = 0;
  for (const [t, n] of a) dot += n * (b.get(t) ?? 0);
  const norm = (v: Map<string, number>) => Math.sqrt([...v.values()].reduce((s, n) => s + n * n, 0));
  const denom = norm(a) * norm(b);
  return denom === 0 ? 0 : dot / denom;
}

function cardText(node: RegisteredNode): string {
  const c = node.agentCard;
  return [c.name, c.description, ...c.skills.flatMap((s) => [s.name, s.description, ...s.tags])].join(' ');
}

/** Capabilities a node offers: skill ids, skill tags and workspace modes. */
function capabilitiesOf(node: RegisteredNode): Set<string> {
  const c = node.agentCard;
  return new Set([...c.skills.flatMap((s) => [s.id, ...s.tags]), ...c.workspaceModes].map((x) => x.toLowerCase()));
}

/** Short summary used in lists; get_node returns the full card. */
function summary(node: RegisteredNode) {
  return {
    nodeId: node.nodeId,
    name: node.agentCard.name,
    description: node.agentCard.description,
    status: node.status,
    skills: node.agentCard.skills.map((s) => s.id),
    workspaceModes: node.agentCard.workspaceModes,
  };
}

export function registerDiscoveryTools(server: McpServer): void {
  server.registerTool(
    'get_nodes',
    {
      title: 'Find nodes',
      description:
        'Lists the nodes the caller may use, ranked by how well their AgentCard matches the query. ' +
        'Optionally keeps only nodes that have all given capabilities (skill ids, skill tags or workspace modes).',
      inputSchema: {
        query: z.string().describe('What the task is about, e.g. "fix a bug in the typescript frontend".'),
        capability_filter: z
          .array(z.string())
          .optional()
          .describe('Capabilities every returned node must have, e.g. ["git", "code"].'),
      },
      annotations: { readOnlyHint: true },
    },
    async ({ query, capability_filter }) => {
      const queryVec = termVector(tokenize(query));
      const required = (capability_filter ?? []).map((c) => c.toLowerCase());

      const nodes = store
        .listNodes()
        .filter((n) => required.every((c) => capabilitiesOf(n).has(c)))
        .map((n) => ({
          ...summary(n),
          score: Math.round(cosine(queryVec, termVector(tokenize(cardText(n)))) * 1000) / 1000,
        }))
        .sort((a, b) => b.score - a.score);

      return ok({ query, nodes });
    },
  );

  server.registerTool(
    'get_node',
    {
      title: 'Get node',
      description: "Returns one node's AgentCard, status, skills and supported workspace modes.",
      inputSchema: {
        node_id: z.string().describe('ID of the node, e.g. "node-claude-code-1".'),
      },
      annotations: { readOnlyHint: true },
    },
    async ({ node_id }) => {
      const node = store.getNode(node_id);
      if (!node) return fail('NODE_NOT_FOUND', `No node with id "${node_id}".`);
      return ok({
        nodeId: node.nodeId,
        status: node.status,
        lastSeen: node.lastSeen,
        agentCard: node.agentCard,
      });
    },
  );

  server.registerTool(
    'get_extended_agent_card',
    {
      title: 'Get extended agent card',
      description:
        'Returns the extended (authenticated) AgentCard details of a node. ' +
        'Only works for nodes that advertise capabilities.extendedAgentCard.',
      inputSchema: {
        node_id: z.string().describe('ID of the node.'),
      },
      annotations: { readOnlyHint: true },
    },
    async ({ node_id }) => {
      const node = store.getNode(node_id);
      if (!node) return fail('NODE_NOT_FOUND', `No node with id "${node_id}".`);
      if (!node.agentCard.capabilities.extendedAgentCard || !node.extendedCard) {
        return fail('EXTENDED_CARD_NOT_SUPPORTED', `Node "${node_id}" does not advertise an extended agent card.`);
      }
      if (node.status === 'offline') {
        return fail('NODE_UNREACHABLE', `Node "${node_id}" is offline.`);
      }
      return ok({ nodeId: node.nodeId, agentCard: node.agentCard, extended: node.extendedCard });
    },
  );
}
