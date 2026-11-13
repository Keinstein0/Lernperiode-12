import { mockApi } from './mock';
import type { ApiClient, Assignment, ChatMessage, Node, Task } from './types';

/**
 * Router-backed demo adapter. Only documented router endpoints use HTTP.
 * Projects, chat and demo sign-in remain local until their API contracts exist.
 * Use the same-origin /api proxy; credentials are never forwarded to nodes.
 */
const ROOT = '/api';
async function request<T>(path: string, options?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${ROOT}${path}`, {
      ...options,
      headers: { ...(options?.body ? { 'Content-Type': 'application/json' } : {}), ...options?.headers }
    });
  } catch {
    throw new Error('Cannot reach the router. Check that the demo containers are running.');
  }
  const data: unknown = await response.json().catch(() => null);
  if (!response.ok) {
    const message = data && typeof data === 'object' && 'error' in data
      ? String((data as {error: unknown}).error)
      : `Router request failed (${response.status})`;
    throw new Error(message);
  }
  return data as T;
}

const associationKey = 'unisk-router-project-task-ids';
function associations(): Record<string, string> {
  if (typeof localStorage === 'undefined') return {};
  try { return JSON.parse(localStorage.getItem(associationKey) || '{}') as Record<string,string>; }
  catch { return {}; }
}
function setAssociation(id: string, project: string) {
  if (typeof localStorage === 'undefined') return;
  const data = associations(); data[id] = project;
  localStorage.setItem(associationKey, JSON.stringify(data));
}
async function listRouterTasks(): Promise<Task[]> { return request<Task[]>('/task/'); }
function projectForTask(task: Task): string | undefined {
  return associations()[task.id];
}

export const liveApi: ApiClient = {
  ...mockApi,
  async listNodes(): Promise<Node[]> { return request<Node[]>('/node/'); },
  async getNodeStatuses(): Promise<Node[]> { return request<Node[]>('/node/'); },
  async listTasks(): Promise<Task[]> {
    const tasks = await listRouterTasks();
    // The router's task list currently contains stored status snapshots.
    return Promise.all(tasks.map(async task => {
      try { return await request<Task>(`/task/${encodeURIComponent(task.id)}`); }
      catch { return task; }
    }));
  },
  async getTask(id: string): Promise<Task> {
    return request<Task>(`/task/${encodeURIComponent(id)}`);
  },
  async listProjectTasks(slug: string): Promise<Task[]> {
    const tasks = await liveApi.listTasks();
    const project = await mockApi.getProject(slug);
    return tasks.filter(task => projectForTask(task) === slug ||
      (!projectForTask(task) && Boolean(project?.repository) && task.ticket['git-repo-url'] === project?.repository));
  },
  async assignTicket({ nodeId, ticket, projectSlug }: Assignment): Promise<{ id: string }> {
    const result = await request<{status: string; task: Task}>('/task/assign', {
      method: 'POST', body: JSON.stringify({ 'node-id': nodeId, ticket })
    });
    if (projectSlug) setAssociation(result.task.id, projectSlug);
    return { id: result.task.id };
  },
  async getAgentReply(project: string): Promise<ChatMessage> {
    const history = await mockApi.listMessages(project);
    const latest = [...history].reverse().find(m => m.author === 'You');
    const command = latest?.body.match(/^(?:create task|assign task|task):\s*(.+)$/is);
    if (!command) return mockApi.getAgentReply(project);
    // Intercept task commands to use actual router tickets rather than mock tasks.
    const nodes = await liveApi.getNodeStatuses();
    const available = nodes.find(n => n.status === 'idle' || n.status === 'done');
    const p = await mockApi.getProject(project);
    let response = 'No available agent can accept this task.';
    if (available && p) {
      const { id } = await liveApi.assignTicket({
        nodeId: available.id, projectSlug: project,
        ticket: {
          'created-at': new Date().toISOString(), 'created-by': 'demo-user',
          'git-repo-url': p.repository ?? '', 'git-branch-id': 'demo',
          'files-of-interest': [], description: command[1].trim()
        }
      });
      response = `Created task #${id} and assigned it to ${available.name}. Track its progress in Tasks.`;
    }
    // The local conversation store has no append-agent-message method; use a
    // dedicated non-command request in the mock adapter to persist this response.
    return mockApi.recordAgentMessage(project, response);
  },
  async advanceTask(id: string): Promise<Task> {
    // Real nodes are not manually advanced; check their latest reported state.
    return liveApi.getTask(id);
  },
  async deleteProject(slug: string) {
    await mockApi.deleteProject(slug);
    if (typeof localStorage !== 'undefined') {
      const retained = Object.fromEntries(Object.entries(associations()).filter(([,project]) => project !== slug));
      localStorage.setItem(associationKey, JSON.stringify(retained));
    }
  }
};
