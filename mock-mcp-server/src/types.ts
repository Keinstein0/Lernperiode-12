/**
 * Data shapes for the mock router. Loosely follows A2A v1 (AgentCard, Task,
 * Message, Artifact) plus the UniSK-specific bits from uniskv2-interactions.md.
 */

/** UniSK workspace extension advertised in the AgentCard. */
export type WorkspaceMode = 'git' | 'conngit' | 'files';

/** Liveness as reported by POST /nodes/heartbeat. */
export type NodeStatus = 'free' | 'busy' | 'offline';

export interface AgentSkill {
  id: string;
  name: string;
  description: string;
  tags: string[];
}

export interface AgentCard {
  name: string;
  description: string;
  url: string;
  version: string;
  capabilities: {
    streaming: boolean;
    pushNotifications: boolean;
    extendedAgentCard: boolean;
  };
  skills: AgentSkill[];
  /** UniSK extension param (see uniskv2-interactions.md). */
  workspaceModes: WorkspaceMode[];
}

export interface RegisteredNode {
  nodeId: string;
  status: NodeStatus;
  lastSeen: string; // ISO timestamp
  agentCard: AgentCard;
  /** Only returned by get_extended_agent_card, if the card advertises it. */
  extendedCard?: Record<string, unknown>;
}

export type TaskState =
  | 'TASK_STATE_SUBMITTED'
  | 'TASK_STATE_WORKING'
  | 'TASK_STATE_INPUT_REQUIRED'
  | 'TASK_STATE_COMPLETED'
  | 'TASK_STATE_FAILED'
  | 'TASK_STATE_CANCELED';

/** States after which a task can no longer change. */
export const TERMINAL_STATES: readonly TaskState[] = [
  'TASK_STATE_COMPLETED',
  'TASK_STATE_FAILED',
  'TASK_STATE_CANCELED',
];

export type Part = { text: string } | { data: Record<string, unknown> } | { url: string; mediaType?: string };

export interface Message {
  messageId: string;
  role: 'user' | 'agent';
  parts: Part[];
  taskId?: string;
  contextId?: string;
}

export interface Artifact {
  artifactId: string;
  name: string;
  parts: Part[];
}

export interface TaskStatus {
  state: TaskState;
  timestamp: string; // ISO timestamp
  message?: Message;
}

export interface Task {
  id: string;
  contextId: string;
  status: TaskStatus;
  history: Message[];
  artifacts: Artifact[];
}

/** Auth Node record: identity + which target nodes this node may use. */
export interface NodeAuth {
  nodeId: string;
  isAdmin: boolean;
  allowedNodes: string[];
}

/** Router-side bookkeeping: which node a task was routed to. */
export interface TaskRecord {
  task: Task;
  nodeId: string;
  createdAt: string;
}
