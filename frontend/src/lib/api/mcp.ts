import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StreamableHTTPClientTransport } from '@modelcontextprotocol/sdk/client/streamableHttp.js';

/** Router MCP endpoint. Defaults to the local mock (mock-mcp-server/). */
const MCP_URL: string = import.meta.env.VITE_MCP_URL ?? 'http://localhost:3333/mcp';

/** Error returned by a router tool, e.g. code "NODE_BUSY". */
export class McpToolError extends Error {
	constructor(
		public readonly code: string,
		message: string
	) {
		super(`${code}: ${message}`);
		this.name = 'McpToolError';
	}
}

let clientPromise: Promise<Client> | null = null;

/** One shared MCP client, connected on first use. */
function getClient(): Promise<Client> {
	clientPromise ??= (async () => {
		const client = new Client({ name: 'unisk-frontend', version: '0.1.0' });
		await client.connect(new StreamableHTTPClientTransport(new URL(MCP_URL)));
		return client;
	})().catch((err) => {
		clientPromise = null; // allow a retry after a failed connect
		throw err;
	});
	return clientPromise;
}

/** Calls a router tool and returns its structuredContent. Throws McpToolError on tool errors. */
export async function callTool<T = Record<string, unknown>>(
	name: string,
	args: Record<string, unknown> = {}
): Promise<T> {
	const client = await getClient();
	const res = await client.callTool({ name, arguments: args });

	if (res.isError) {
		const text = (res.content as { type: string; text?: string }[])[0]?.text ?? '';
		try {
			const { error } = JSON.parse(text);
			throw new McpToolError(error?.code ?? 'UNKNOWN', error?.message ?? text);
		} catch (err) {
			if (err instanceof McpToolError) throw err;
			throw new McpToolError('UNKNOWN', text || 'Tool call failed');
		}
	}
	return res.structuredContent as T;
}

// ---- Typed shortcuts for the router tools (UniSKv2) ---------------------

export type TaskState =
	| 'TASK_STATE_SUBMITTED'
	| 'TASK_STATE_WORKING'
	| 'TASK_STATE_INPUT_REQUIRED'
	| 'TASK_STATE_COMPLETED'
	| 'TASK_STATE_FAILED'
	| 'TASK_STATE_CANCELED';

export interface NodeSummary {
	nodeId: string;
	name: string;
	description: string;
	status: 'free' | 'busy' | 'offline';
	skills: string[];
	workspaceModes: string[];
	score: number;
}

export type Part = { text: string } | { data: Record<string, unknown> };

export interface A2ATask {
	id: string;
	contextId: string;
	status: { state: TaskState; timestamp: string; message?: { role: string; parts: Part[] } };
	history: { messageId: string; role: 'user' | 'agent'; parts: Part[] }[];
	artifacts: { artifactId: string; name: string; parts: Part[] }[];
}

export interface RoutedTask {
	nodeId: string;
	task: A2ATask;
}

export const mcp = {
	getNodes: (query: string, capabilityFilter?: string[]) =>
		callTool<{ query: string; nodes: NodeSummary[] }>('get_nodes', {
			query,
			...(capabilityFilter ? { capability_filter: capabilityFilter } : {})
		}),

	sendTask: (nodeId: string, text: string, opts: { taskId?: string; contextId?: string } = {}) =>
		callTool<RoutedTask>('send_task', {
			node_id: nodeId,
			message: { parts: [{ text }], ...opts },
			configuration: { returnImmediately: true }
		}),

	getTask: (taskId: string) => callTool<RoutedTask>('get_task', { task_id: taskId }),

	getTasks: () => callTool<{ tasks: RoutedTask[]; totalSize: number }>('get_tasks', {}),

	cancelTask: (taskId: string) => callTool<RoutedTask>('cancel_task', { task_id: taskId })
};
