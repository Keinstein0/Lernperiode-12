import type { ApiClient, Assignment, ChatMessage, Node, Project, Session, Task } from './types';

// In-memory mock router. All methods are asynchronous; no fetch or external network calls.
const delay = (ms = 110) => new Promise<void>((resolve) => setTimeout(resolve, ms));
const copy = <T>(value: T): T => structuredClone(value);

const projects: Project[] = [
  { slug: 'website-redesign', name: 'Website redesign', description: 'Improve the application interface, navigation and component system across the workspace.', details: 'Design and implement a consistent, accessible interface. Establish page structure and navigation before connecting the frontend to platform services.', status: 'Active', repository: 'https://github.com/Keinstein0/Lernperiode-12', owner: 'keinstein0', created: '5 Nov 2026', updated: '2 hours ago' },
  { slug: 'research-pipeline', name: 'Research pipeline', description: 'Coordinate research, source evaluation and structured reporting.', details: 'Collect supporting material, review evidence and organise findings into reports.', status: 'Active', repository: null, owner: 'Workspace', created: '3 Nov 2026', updated: '5 hours ago' },
  { slug: 'code-review', name: 'Code review', description: 'Review changes, assess quality and track improvements.', details: 'Review code changes, maintain standards and document identified issues.', status: 'Idle', repository: null, owner: 'Workspace', created: '29 Oct 2026', updated: 'Yesterday' },
  { slug: 'documentation', name: 'Documentation', description: 'Maintain architecture notes, developer guidance and technical references.', details: 'Keep project decisions and guidance organised.', status: 'Idle', repository: null, owner: 'Workspace', created: '21 Oct 2026', updated: '2 days ago' }
];
const nodes: Node[] = [
  { id: 'research-01', name: 'Research agent', type: 'keinstein0:research-agent-1.3', status: 'idle', 'task-id': null },
  { id: 'docs-05', name: 'Docs agent', type: 'keinstein0:docs-agent-1.0', status: 'idle', 'task-id': null },
  { id: 'code-02', name: 'Code agent', type: 'keinstein0:code-agent-1.0', status: 'busy', 'task-id': '25' },
  { id: 'review-03', name: 'Review agent', type: 'keinstein0:review-agent-1.0', status: 'idle', 'task-id': null },
  { id: 'test-04', name: 'Test agent', type: 'keinstein0:test-agent-1.0', status: 'error', 'task-id': null },
  { id: 'plan-06', name: 'Planning agent', type: 'keinstein0:planning-agent-1.0', status: 'busy', 'task-id': '29' }
];
function makeTask(id: string, description: string, nodeId: string, status: Task['status'], project: string): Task {
  return { id, 'node-id': nodeId, status, ticket: { 'created-at': '2026-11-13T08:00:00Z', 'created-by': 'keinstein0', 'git-repo-url': projects.find((p) => p.slug === project)?.repository ?? '', 'git-branch-id': 'feature/frontend-prototype', 'files-of-interest': [], description }, report: status === 'done' ? { 'finished-at': '2026-11-13T08:30:00Z', 'finished-by': nodeId, 'git-repo-url': '', 'git-branch-id': 'feature/frontend-prototype', 'files-of-interest': [], description: `Completed: ${description}` } : null };
}
const tasks: Task[] = [
  makeTask('24', 'Audit existing components', 'review-03', 'done', 'website-redesign'),
  makeTask('25', 'Build reusable navigation components', 'code-02', 'busy', 'website-redesign'),
  makeTask('26', 'Research accessibility patterns', 'research-01', 'done', 'website-redesign'),
  makeTask('27', 'Document interface guidelines', 'docs-05', 'busy', 'website-redesign'),
  makeTask('28', 'Review project page hierarchy', 'review-03', 'busy', 'website-redesign'),
  makeTask('29', 'Collect reference sources', 'plan-06', 'busy', 'research-pipeline'),
  makeTask('30', 'Summarise research findings', 'docs-05', 'busy', 'research-pipeline'),
  makeTask('31', 'Evaluate outstanding changes', 'review-03', 'busy', 'code-review'),
  makeTask('32', 'Update developer documentation', 'docs-05', 'busy', 'documentation')
];
const taskProject = new Map<string, string>([['24','website-redesign'],['25','website-redesign'],['26','website-redesign'],['27','website-redesign'],['28','website-redesign'],['29','research-pipeline'],['30','research-pipeline'],['31','code-review'],['32','documentation']]);
const conversations: ChatMessage[] = [
  { id: 'm1', project: 'website-redesign', author: 'You', time: '09:34', body: 'What should we prioritise next?' },
  { id: 'm2', project: 'website-redesign', author: 'Main agent', time: '09:35', body: 'Navigation structure and task organisation are the current priorities.' },
  { id: 'm3', project: 'website-redesign', author: 'You', time: '09:36', body: 'Keep the conversation separate from the task list.' }
];
let nextTaskId = 33;
let nextMessageId = 4;
export const mockApi: ApiClient & { getProjectForTask(id: string): string | undefined } = {
  async login(): Promise<Session> { await delay(); return { user: { login: 'keinstein0' }, token: 'mock-session-not-a-real-jwt' }; },
  async listNodes() { await delay(); return copy(nodes); },
  async getNodeStatuses() { await delay(); return copy(nodes); },
  async getTask(id) { await delay(); const task = tasks.find((t) => t.id === id); if (!task) throw new Error(`Task ${id} not found`); return copy(task); },
  async listTasks() { await delay(); return copy(tasks); },
  async listProjectTasks(slug) { await delay(); return copy(tasks.filter((t) => taskProject.get(t.id) === slug)); },
  async assignTicket({ nodeId, ticket }: Assignment) {
    await delay();
    const node = nodes.find((n) => n.id === nodeId);
    if (!node) throw new Error('Selected agent does not exist');
    if (node.status !== 'idle' && node.status !== 'done') throw new Error('Selected agent is not available');
    if (!ticket.description.trim()) throw new Error('Task description is required');
    const id = String(nextTaskId++);
    tasks.unshift({ id, 'node-id': nodeId, ticket: copy(ticket), status: 'busy', report: null });
    node.status = 'busy'; node['task-id'] = id;
    const project = projects.find((p) => p.repository === ticket['git-repo-url']);
    taskProject.set(id, project?.slug ?? 'website-redesign');
    return { id };
  },
  async listProjects() { await delay(); return copy(projects); },
  async getProject(slug) { await delay(); return copy(projects.find((p) => p.slug === slug) ?? null); },
  async listMessages(project) { await delay(); return copy(conversations.filter((m) => m.project === project)); },
  async sendMessage(project, body) {
    await delay();
    if (!projects.some((p) => p.slug === project)) throw new Error('Project not found');
    if (!body.trim()) throw new Error('Message cannot be empty');
    const message: ChatMessage = { id: `m${nextMessageId++}`, project, author: 'You', time: new Date().toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' }), body: body.trim() };
    conversations.push(message);
    return copy(message);
  },
  getProjectForTask(id) { return taskProject.get(id); }
};
