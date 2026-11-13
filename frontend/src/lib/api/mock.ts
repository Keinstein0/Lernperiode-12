import type { ApiClient, Assignment, ChatMessage, Node, Project, ProjectInput, Session, Task } from './types';

// In-memory mock router. All methods are asynchronous; no fetch or external network calls.
const delay = async (ms = 110) => { hydrate(); await new Promise<void>((resolve) => setTimeout(resolve, ms)); processDueTasks(); };
const copy = <T>(value: T): T => structuredClone(value);

let projects: Project[] = [
  { slug: 'website-redesign', name: 'Website redesign', description: 'Improve the application interface, navigation and component system across the workspace.', details: 'Design and implement a consistent, accessible interface. Establish page structure and navigation before connecting the frontend to platform services.', status: 'Active', repository: 'https://github.com/Keinstein0/Lernperiode-12', owner: 'keinstein0', created: '5 Nov 2026', updated: '2 hours ago' },
  { slug: 'research-pipeline', name: 'Research pipeline', description: 'Coordinate research, source evaluation and structured reporting.', details: 'Collect supporting material, review evidence and organise findings into reports.', status: 'Active', repository: null, owner: 'Workspace', created: '3 Nov 2026', updated: '5 hours ago' },
  { slug: 'code-review', name: 'Code review', description: 'Review changes, assess quality and track improvements.', details: 'Review code changes, maintain standards and document identified issues.', status: 'Idle', repository: null, owner: 'Workspace', created: '29 Oct 2026', updated: 'Yesterday' },
  { slug: 'documentation', name: 'Documentation', description: 'Maintain architecture notes, developer guidance and technical references.', details: 'Keep project decisions and guidance organised.', status: 'Idle', repository: null, owner: 'Workspace', created: '21 Oct 2026', updated: '2 days ago' }
];
let nodes: Node[] = [
  { id: 'research-01', name: 'Research agent', type: 'keinstein0:research-agent-1.3', status: 'idle', 'task-id': null },
  { id: 'docs-05', name: 'Docs agent', type: 'keinstein0:docs-agent-1.0', status: 'idle', 'task-id': null },
  { id: 'code-02', name: 'Code agent', type: 'keinstein0:code-agent-1.0', status: 'busy', 'task-id': '25' },
  { id: 'review-03', name: 'Review agent', type: 'keinstein0:review-agent-1.0', status: 'idle', 'task-id': null },
  { id: 'test-04', name: 'Test agent', type: 'keinstein0:test-agent-1.0', status: 'error', 'task-id': null },
  { id: 'plan-06', name: 'Planning agent', type: 'keinstein0:planning-agent-1.0', status: 'busy', 'task-id': '29' }
];
function makeTask(id: string, description: string, nodeId: string, status: Task['status'], project: string): Task {
  return { id, 'node-id': nodeId, status, ticket: { 'created-at': '2026-11-13T08:00:00Z', 'created-by': 'keinstein0', 'git-repo-url': projects.find((p) => p.slug === project)?.repository ?? '', 'git-branch-id': 'feature/frontend-prototype', 'files-of-interest': [], description }, report: status === 'done' ? { 'finished-at': '2026-11-13T08:30:00Z', 'finished-by-node-id': nodeId, 'git-repo-url': '', 'git-branch-id': 'feature/frontend-prototype', 'files-of-interest': [], description: `Completed: ${description}` } : null };
}
let tasks: Task[] = [
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
let taskProject = new Map<string, string>([['24','website-redesign'],['25','website-redesign'],['26','website-redesign'],['27','website-redesign'],['28','website-redesign'],['29','research-pipeline'],['30','research-pipeline'],['31','code-review'],['32','documentation']]);
let conversations: ChatMessage[] = [
  { id: 'm1', project: 'website-redesign', author: 'You', time: '09:34', body: 'What should we prioritise next?' },
  { id: 'm2', project: 'website-redesign', author: 'Main agent', time: '09:35', body: 'Navigation structure and task organisation are the current priorities.' },
  { id: 'm3', project: 'website-redesign', author: 'You', time: '09:36', body: 'Keep the conversation separate from the task list.' }
];
let session: Session | null = null;
let hydrated = false;
let completionDue: Record<string, number> = {};
const STORAGE_KEY = 'unisk-mock-state-v2';
function hydrate() {
  if (hydrated || typeof localStorage === 'undefined') return;
  hydrated = true;
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return;
    const state = JSON.parse(raw);
    if (!Array.isArray(state.projects) || !Array.isArray(state.nodes) || !Array.isArray(state.tasks) || !Array.isArray(state.conversations) || !Array.isArray(state.taskProject)) return;
    projects = state.projects; nodes = state.nodes; tasks = state.tasks;
    conversations = state.conversations; taskProject = new Map(state.taskProject);
    nextTaskId = state.nextTaskId ?? 33; nextMessageId = state.nextMessageId ?? 4;
    completionDue = state.completionDue ?? {}; session = state.session ?? null;
  } catch { /* Invalid persisted mock state: use initial fixtures. */ }
}
function save() {
  if (typeof localStorage === 'undefined') return;
  localStorage.setItem(STORAGE_KEY, JSON.stringify({projects,nodes,tasks,conversations,taskProject:[...taskProject],nextTaskId,nextMessageId,completionDue,session}));
}
function processDueTasks() {
  let changed = false;
  for (const [id, due] of Object.entries(completionDue)) {
    if (Date.now() < due) continue;
    const task = tasks.find(t => t.id === id);
    if (task?.status === 'busy') { finishTask(task); changed = true; }
    delete completionDue[id];
  }
  if (changed) save();
}
function finishTask(task: Task) {
  task.status = 'done';
  task.report = { 'finished-at': new Date().toISOString(), 'finished-by-node-id': task['node-id'], 'git-repo-url': task.ticket['git-repo-url'], 'git-branch-id': task.ticket['git-branch-id'], 'files-of-interest': task.ticket['files-of-interest'], description: `Completed: ${task.ticket.description}` };
  const node = nodes.find(n => n.id === task['node-id']);
  if (node?.['task-id'] === task.id) { node.status = 'done'; node['task-id'] = null; }
}
let nextTaskId = 33;
let nextMessageId = 4;
const slugify = (name: string) => name.toLowerCase().trim().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
export const mockApi: ApiClient & { getProjectForTask(id: string): string | undefined; recordAgentMessage(project: string, body: string): Promise<ChatMessage> } = {
  async login(): Promise<Session> { await delay(); session = { user: { login: 'keinstein0' }, token: 'mock-session-not-a-real-jwt' }; save(); return copy(session); },
  async getSession() { await delay(); return session ? copy(session) : null; },
  async logout() { await delay(); session = null; save(); },
  async listNodes() { await delay(); return copy(nodes); },
  async getNodeStatuses() { await delay(); return copy(nodes); },
  async getTask(id) { await delay(); const task = tasks.find((t) => t.id === id); if (!task) throw new Error(`Task ${id} not found`); return copy(task); },
  async listTasks() { await delay(); return copy(tasks); },
  async listProjectTasks(slug) { await delay(); return copy(tasks.filter((t) => taskProject.get(t.id) === slug)); },
  async assignTicket({ nodeId, ticket, projectSlug }: Assignment) {
    await delay();
    const node = nodes.find((n) => n.id === nodeId);
    if (!node) throw new Error('Selected agent does not exist');
    if (node.status !== 'idle' && node.status !== 'done') throw new Error('Selected agent is not available');
    if (!ticket.description.trim()) throw new Error('Task description is required');
    const id = String(nextTaskId++);
    tasks.unshift({ id, 'node-id': nodeId, ticket: copy(ticket), status: 'busy', report: null });
    node.status = 'busy'; node['task-id'] = id;
    const project = projects.find((p) => p.repository === ticket['git-repo-url']);
    taskProject.set(id, projectSlug && projects.some(p => p.slug === projectSlug) ? projectSlug : project?.slug ?? 'website-redesign');
    completionDue[id] = Date.now() + 15000; save();
    return { id };
  },
  async createProject(input: ProjectInput) {
    await delay();
    if (!input.name.trim() || !input.description.trim()) throw new Error('Name and description are required');
    const slug = slugify(input.name);
    if (!slug || projects.some((p) => p.slug === slug)) throw new Error('A project with that name already exists');
    const project: Project = { ...input, slug, name: input.name.trim(), description: input.description.trim(), details: input.details.trim(), repository: input.repository?.trim() || null, status: 'Active', owner: 'keinstein0', created: new Date().toLocaleDateString('en-GB'), updated: 'Just now' };
    projects.unshift(project); save(); return copy(project);
  },
  async updateProject(slug, input) {
    await delay();
    const project = projects.find((p) => p.slug === slug);
    if (!project) throw new Error('Project not found');
    if (input.name !== undefined && !input.name.trim()) throw new Error('Project name cannot be empty');
    if (input.description !== undefined && !input.description.trim()) throw new Error('Description cannot be empty');
    Object.assign(project, input, { updated: 'Just now' }); save(); return copy(project);
  },
  async deleteProject(slug) {
    await delay();
    if (!projects.some(p => p.slug === slug)) throw new Error('Project not found');
    projects = projects.filter(p => p.slug !== slug);
    const removed = tasks.filter(t => taskProject.get(t.id) === slug);
    for (const task of removed) {
      const node = nodes.find(n => n.id === task['node-id']);
      if (node?.['task-id'] === task.id) { node['task-id'] = null; node.status = 'idle'; }
      taskProject.delete(task.id); delete completionDue[task.id];
    }
    tasks = tasks.filter(t => !removed.some(r => r.id === t.id));
    conversations = conversations.filter(m => m.project !== slug);
    save();
  },
  async advanceTask(id) {
    await delay(250);
    const task = tasks.find((t) => t.id === id);
    if (!task) throw new Error('Task not found');
    if (task.status === 'busy') { finishTask(task); delete completionDue[id]; save(); }
    return copy(task);
  },
  async getAgentReply(project) {
    await delay(450);
    if (!projects.some((p) => p.slug === project)) throw new Error('Project not found');
    const last = [...conversations].reverse().find(m => m.project === project && m.author === 'You');
    // Explicit task commands simulate orchestration through the same mock router contract.
    let response = last ? `I’ve received your request: “${last.body}”. I’ll use the project context and coordinate the relevant tasks.` : 'Ready to help with this project.';
    const command = last?.body.match(/^(?:create task|assign task|task):\s*(.+)$/is);
    if (command) {
      const node = nodes.find(n => n.status === 'idle' || n.status === 'done');
      if (node) {
        const p = projects.find(p => p.slug === project)!;
        const { id } = await mockApi.assignTicket({nodeId:node.id,projectSlug:project,ticket:{'created-at':new Date().toISOString(),'created-by':session?.user.login??'keinstein0','git-repo-url':p.repository??'','git-branch-id':'main','files-of-interest':[],description:command[1].trim()}});
        response = `Created task #${id} and assigned it to ${node.name}. You can follow its progress in Tasks.`;
      } else response = 'No agent is currently available to take a new task.';
    }
    const reply: ChatMessage = { id: `m${nextMessageId++}`, project, author: 'Main agent', time: new Date().toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' }), body: response };
    conversations.push(reply); save(); return copy(reply);
  },
  async listProjects() { await delay(); return copy(projects); },
  async getProject(slug) { await delay(); return copy(projects.find((p) => p.slug === slug) ?? null); },
  async listMessages(project) { await delay(); return copy(conversations.filter((m) => m.project === project)); },
  async sendMessage(project, body) {
    await delay();
    if (!projects.some((p) => p.slug === project)) throw new Error('Project not found');
    if (!body.trim()) throw new Error('Message cannot be empty');
    const message: ChatMessage = { id: `m${nextMessageId++}`, project, author: 'You', time: new Date().toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' }), body: body.trim() };
    conversations.push(message); save();
    return copy(message);
  },
  async recordAgentMessage(project, body) {
    await delay();
    const reply: ChatMessage = { id: `m${nextMessageId++}`, project, author: 'Main agent', time: new Date().toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' }), body };
    conversations.push(reply); save(); return copy(reply);
  },
  getProjectForTask(id) { return taskProject.get(id); }
};
