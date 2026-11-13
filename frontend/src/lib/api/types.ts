/** Wire-compatible fields from interactions.md. */
export type NodeStatus = 'busy' | 'idle' | 'done' | 'error';
export type TaskStatus = 'busy' | 'done' | 'error';
export interface Node { name: string; id: string; type: string; status: NodeStatus; 'task-id': string | null; }
export interface Ticket { 'created-at': string; 'created-by': string; 'git-repo-url': string; 'git-branch-id': string; 'files-of-interest': string[]; description: string; }
export interface Report { 'finished-at': string; 'finished-by': string; 'git-repo-url': string; 'git-branch-id': string; 'files-of-interest': string[]; description: string; }
export interface Task { id: string; 'node-id': string; ticket: Ticket; status: TaskStatus; report: Report | null; }
export interface Assignment { nodeId: string; ticket: Ticket; }
export interface Session { user: { login: string }; token: string; }
export interface Project { slug: string; name: string; description: string; details: string; status: string; repository: string | null; owner: string; created: string; updated: string; }
export interface ChatMessage { id: string; project: string; author: 'You' | 'Main agent'; time: string; body: string; }
export interface ApiClient {
  login(): Promise<Session>;
  listNodes(): Promise<Node[]>; // GET /nodes
  getNodeStatuses(): Promise<Node[]>; // GET /status
  getTask(id: string): Promise<Task>; // GET /task/:id
  listTasks(): Promise<Task[]>; // Mock-only convenience; not defined in interactions.md
  listProjectTasks(slug: string): Promise<Task[]>; // Mock-only project association
  assignTicket(request: Assignment): Promise<{ id: string }>; // POST /assign
  listProjects(): Promise<Project[]>; // Mock-only project endpoint
  getProject(slug: string): Promise<Project | null>;
  listMessages(project: string): Promise<ChatMessage[]>; // Mock-only conversation endpoint
  sendMessage(project: string, body: string): Promise<ChatMessage>; // Mock-only conversation endpoint
}
