<script lang="ts">
 import { page } from '$app/state';
 import { tick, onMount } from 'svelte';
 import { goto } from '$app/navigation';
 import AppShell from './AppShell.svelte';
 import Icon from './Icon.svelte';
 import StatusIcon from './StatusIcon.svelte';
 import ChatContent from './ChatContent.svelte';
 import { api } from '#lib/api';
 import type { Project, Task, Node, ChatMessage } from '#lib/api';
 let {tab='overview'}: {tab?: 'overview'|'conversation'|'tasks'}=$props();
 let project=$state<Project|null>(null); let projectTasks=$state<Task[]>([]); let nodes=$state<Node[]>([]); let messages=$state<ChatMessage[]>([]);
 let loading=$state(true); let error=$state(''); let notice=$state('');
 let messageText=$state(''); let sending=$state(false); let replying=$state(false); let stream: HTMLDivElement;
 let showNewTask=$state(false); let taskDescription=$state(''); let selectedNode=$state(''); let branch=$state('main'); let files=$state(''); let assigning=$state(false); let selectedTask=$state<Task|null>(null); let filter=$state('all'); let query=$state('');
 let chatSearch=$state(''); let taskAgent=$state('all'); let taskSort=$state('newest'); let retrying=$state(false);
 let deleting=$state(false); let deleteBusy=$state(false);
 let editing=$state(false); let editName=$state(''); let editDescription=$state(''); let editDetails=$state(''); let editRepository=$state(''); let savingProject=$state(false);
 let visibleTasks=$derived(projectTasks.filter(t=>(filter==='all'||t.status===filter) && (taskAgent==='all'||t['node-id']===taskAgent) && (`${t.ticket.description} ${t.id} ${t['node-id']}`).toLowerCase().includes(query.toLowerCase())).sort((a,b)=>taskSort==='oldest'?a.ticket['created-at'].localeCompare(b.ticket['created-at']):b.ticket['created-at'].localeCompare(a.ticket['created-at'])));
 let visibleMessages=$derived(messages.filter(m=>!chatSearch||`${m.body} ${m.author}`.toLowerCase().includes(chatSearch.toLowerCase())));
 let availableNodes=$derived(nodes.filter(n=>n.status==='idle'||n.status==='done'));
 let requestVersion = 0;
 async function reload(slug: string) {
  const version = ++requestVersion;
  loading = true;
  error = '';
  notice = '';
  project = null;
  projectTasks = [];
  nodes = [];
  messages = [];
  selectedTask = null;
  showNewTask = false;
  editing = false;
  messageText = '';
  try {
   const [p, t, n, m] = await Promise.all([
    api.getProject(slug), api.listProjectTasks(slug), api.getNodeStatuses(), api.listMessages(slug)
   ]);
   if (version !== requestVersion) return;
   project = p;
   projectTasks = t;
   nodes = n;
   messages = m;
   if (p) branch = p.repository ? 'feature/frontend-prototype' : 'main';
  } catch (e) {
   if (version === requestVersion) error = err(e);
  } finally {
   if (version === requestVersion) loading = false;
  }
 }
 onMount(() => {
  // Refresh the active project and task statuses, without timer leaks on navigation.
  const timer = setInterval(() => { if (!loading && project) void refreshTasks(true); }, Number(localStorage.getItem('unisk-polling-ms') || '4000') || 2147483647);
  return () => clearInterval(timer);
 });
 // SvelteKit reuses this component when moving between /projects/[project] URLs.
 // Track the route parameter so sidebar navigation always loads the selected project.
 $effect(() => {
  const slug = page.params.project;
  if (slug) void reload(slug);
 });
 const err=(e: unknown)=>e instanceof Error?e.message:String(e);
 function startEdit(){if(!project)return;editName=project.name;editDescription=project.description;editDetails=project.details;editRepository=project.repository??'';editing=true;}
 async function saveProject(){if(!project)return;savingProject=true;notice='';try{project=await api.updateProject(project.slug,{name:editName,description:editDescription,details:editDetails,repository:editRepository||null});editing=false;notice='Project updated';}catch(e){notice=err(e);}finally{savingProject=false;}}
 async function scrollToLatest(){await tick();stream?.scrollTo({top:stream.scrollHeight,behavior:'smooth'});}
 async function sendMessage(){const body=messageText.trim();if(!body||sending||replying||!project)return;const slug=project.slug;sending=true;notice='';try{const sent=await api.sendMessage(slug,body);if(project?.slug!==slug)return;messages=[...messages,sent];messageText='';await scrollToLatest();sending=false;replying=true;const reply=await api.getAgentReply(slug);if(project?.slug!==slug)return;messages=[...messages,reply];await refreshTasks(true);await scrollToLatest();}catch(e){notice=err(e);}finally{sending=false;replying=false;}}
 function handleComposerKey(e:KeyboardEvent){if(e.key==='Enter'&&!e.shiftKey&&!e.isComposing){e.preventDefault();void sendMessage();}}
 async function createTask(){if(!project||!taskDescription.trim()||!selectedNode||assigning)return;assigning=true;notice='';try{const {id}=await api.assignTicket({nodeId:selectedNode,projectSlug:project.slug,ticket:{'created-at':new Date().toISOString(),'created-by':'demo-user','git-repo-url':project.repository??'','git-branch-id':branch.trim()||'main','files-of-interest':files.split(',').map(f=>f.trim()).filter(Boolean),description:taskDescription.trim()}});projectTasks=await api.listProjectTasks(project.slug);nodes=await api.getNodeStatuses();taskDescription='';files='';showNewTask=false;notice=`Task #${id} assigned`;}catch(e){notice=err(e);}finally{assigning=false;}}
 async function inspectTask(id:string){try{selectedTask=await api.getTask(id);}catch(e){notice=err(e);}}
 async function refreshTasks(silent=false){if(!project)return;const slug=project.slug;try{const [nextTasks,nextNodes]=await Promise.all([api.listProjectTasks(slug),api.getNodeStatuses()]);if(project?.slug!==slug)return;projectTasks=nextTasks;nodes=nextNodes;if(selectedTask)selectedTask=nextTasks.find(t=>t.id===selectedTask?.id)??null;}catch(e){if(!silent)notice=err(e);}}
 async function deleteCurrentProject(){if(!project||deleteBusy)return;deleteBusy=true;try{await api.deleteProject(project.slug);await goto('/');}catch(e){notice=err(e);}finally{deleteBusy=false;}}
 function downloadText(name:string,text:string){const url=URL.createObjectURL(new Blob([text],{type:'text/plain;charset=utf-8'}));const a=document.createElement('a');a.href=url;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}
 function exportConversation(){if(!project)return;downloadText(`unisk-${project.slug}-conversation.md`,messages.map(m=>`### ${m.author} · ${m.time}\n\n${m.body}`).join('\n\n---\n\n'));}
 function exportTaskReport(task:Task){if(!task.report)return;downloadText(`unisk-task-${task.id}-report.md`, `# Task ${task.id}\n\n${task.ticket.description}\n\n## Report\n\n${task.report.description}\n\nCompleted: ${task.report['finished-at']}`);}
 async function retryTask(){if(!project||!selectedTask||retrying)return;retrying=true;try{const t=selectedTask;const {id}=await api.assignTicket({nodeId:t['node-id'],projectSlug:project.slug,ticket:{...t.ticket,'created-at':new Date().toISOString()}});notice=`Created replacement task #${id}`;selectedTask=null;await refreshTasks();}catch(e){notice=err(e);}finally{retrying=false;}}
 async function simulateComplete(){if(!selectedTask)return;try{selectedTask=await api.advanceTask(selectedTask.id);await refreshTasks();notice='Task status refreshed';}catch(e){notice=err(e);}}
</script>
<AppShell><div class="content-wrap project-content">
{#if loading}<p class="muted-note" role="status">Loading project…</p>{:else if error}<p class="error-note" role="alert">{error}</p>{:else if project}
<div class="breadcrumbs"><a href="/">Projects</a><span>/</span>{project.name}</div>
<div class="project-heading"><div><h1>{project.name}</h1><p>{project.description}</p></div><span class="status-outline">{project.status}</span></div>
<nav class="project-tabs" aria-label="Project sections"><a class:active={tab==='overview'} href="/projects/{project.slug}"><Icon name="layout"/>Overview</a><a class:active={tab==='conversation'} href="/projects/{project.slug}/conversation"><Icon name="message"/>Conversation</a><a class:active={tab==='tasks'} href="/projects/{project.slug}/tasks"><Icon name="tasks"/>Tasks <span class="count">{projectTasks.length}</span></a></nav>
{#if tab==='overview'}
<div class="project-overview"><section class="overview-main"><div class="section-title"><h2>About</h2><button class="text-button" onclick={startEdit}><Icon name="edit"/>Edit</button></div><p class="body-copy">{project.details||project.description}</p>
{#if editing}<form class="ticket-form edit-project-form" onsubmit={(e)=>{e.preventDefault();void saveProject();}}><label for="edit-name">Name</label><input id="edit-name" bind:value={editName} required/><label for="edit-description">Description</label><input id="edit-description" bind:value={editDescription} required/><label for="edit-details">About</label><textarea id="edit-details" bind:value={editDetails} rows="4"></textarea><label for="edit-repo">Git repository URL</label><input id="edit-repo" type="url" bind:value={editRepository}/><div class="form-actions"><button class="secondary-button" type="button" onclick={()=>editing=false}>Cancel</button><button class="primary-button" disabled={savingProject} type="submit">{savingProject?'Saving…':'Save changes'}</button></div></form>{/if}
<div class="project-danger"><button class="secondary-button" onclick={()=>deleting=!deleting}><Icon name="trash"/>Delete project</button>{#if deleting}<div class="delete-confirm"><p>Delete this project, its tasks and conversation history?</p><div class="form-actions"><button class="secondary-button" onclick={()=>deleting=false}>Cancel</button><button class="danger-button" disabled={deleteBusy} onclick={()=>void deleteCurrentProject()}>{deleteBusy?'Deleting…':'Delete project'}</button></div></div>{/if}</div>
<h2 class="section-heading">Recent tasks</h2><div class="simple-list">{#each projectTasks.slice(0,4) as task (task.id)}<button class="simple-row selectable-row" onclick={()=>inspectTask(task.id)}><StatusIcon status={task.status}/><div><strong>{task.ticket.description}</strong><small>#{task.id} · {nodes.find(n=>n.id===task['node-id'])?.name??task['node-id']}</small></div><span class="row-end">{task.status}</span></button>{/each}{#if projectTasks.length===0}<p class="empty-state">No tasks yet.</p>{/if}</div><a class="section-link" href="/projects/{project.slug}/tasks">View all tasks →</a></section>
<aside class="overview-side"><h2>Project details</h2><dl class="detail-list"><div><dt><Icon name="activity"/>Status</dt><dd><StatusIcon status={project.status} size={15}/>{project.status}</dd></div><div><dt><Icon name="calendar"/>Created</dt><dd>{project.created}</dd></div><div><dt><Icon name="user"/>Owner</dt><dd>{project.owner}</dd></div><div><dt><Icon name="bot"/>Agents</dt><dd>{new Set(projectTasks.map(t=>t['node-id'])).size}</dd></div></dl><h2 class="section-heading">Repository</h2><div class="repo-panel"><Icon name="git" size={20}/><div>{#if project.repository}<a href={project.repository} target="_blank" rel="noreferrer"><strong>{project.repository.replace('https://github.com/','')}</strong><Icon name="external" size={12}/></a>{:else}<strong>Not linked</strong>{/if}<small>Git repository</small></div></div></aside></div>
{:else if tab==='conversation'}
<section class="chat-workspace" aria-label="Project conversation"><div class="chat-heading"><div class="chat-heading-icon"><Icon name="bot" size={18}/></div><div><h2>Project conversation</h2><p>Main agent <span class="presence-dot"></span> Available</p></div><div class="chat-tools"><label class="visually-hidden" for="chat-search">Search conversation</label><input id="chat-search" type="search" placeholder="Search messages" bind:value={chatSearch}/><button class="secondary-button" onclick={exportConversation} disabled={messages.length===0} title="Export conversation"><Icon name="download"/>Export</button><span class="chat-message-count">{visibleMessages.length} messages</span></div></div>
<div class="chat-scroll" bind:this={stream} aria-label="Conversation messages" aria-live="polite"><div class="chat-timeline">{#if messages.length>0 && visibleMessages.length===0}<div class="chat-empty"><h3>No matching messages</h3><p>Try another search term.</p></div>{/if}{#if messages.length===0}<div class="chat-empty"><Icon name="message" size={24}/><h3>Start a conversation</h3><p>Discuss project work with the main agent.</p></div>{/if}
{#each visibleMessages as msg (msg.id)}<article class="chat-entry" class:from-user={msg.author==='You'}><div class="chat-avatar"><Icon name={msg.author==='You'?'user':'bot'} size={17}/></div><div class="chat-bubble-wrap"><div class="chat-byline"><strong>{msg.author}</strong><time>{msg.time}</time></div><div class="chat-bubble"><ChatContent source={msg.body}/><div class="message-actions"><button type="button" title="Copy message" onclick={()=>void navigator.clipboard?.writeText(msg.body)}><Icon name="copy"/>Copy</button>{#if /task #\d+/i.test(msg.body)}<a href="/projects/{project.slug}/tasks"><Icon name="tasks"/>Tasks</a>{/if}</div></div></div></article>{/each}
{#if replying}<div class="chat-entry" aria-label="Agent responding"><div class="chat-avatar"><Icon name="bot" size={17}/></div><div class="chat-bubble-wrap"><div class="chat-byline"><strong>Main agent</strong></div><div class="chat-bubble typing-state"><span></span><span></span><span></span><span class="visually-hidden">Responding</span></div></div></div>{/if}</div></div>
<form class="chat-composer" onsubmit={(e)=>{e.preventDefault();void sendMessage();}}><label class="visually-hidden" for="chat-input">Message the main agent</label><textarea id="chat-input" rows="2" bind:value={messageText} onkeydown={handleComposerKey} placeholder="Message the main agent…" disabled={sending||replying}></textarea><div class="chat-compose-footer"><span>Enter to send · Use “task: Review documentation” to assign a ticket</span><button class="chat-send" type="submit" disabled={!messageText.trim()||sending||replying} aria-label="Send message"><Icon name="send" size={16}/><span>Send</span></button></div></form></section>
{:else if tab==='tasks'}
<section class="tasks-panel"><div class="tasks-header"><h2>Tasks</h2><div class="header-actions"><button class="secondary-button" onclick={()=>void refreshTasks()}><Icon name="refresh"/>Refresh</button><button class="secondary-button" onclick={()=>showNewTask=!showNewTask}><Icon name="plus"/>New task</button></div></div>
{#if showNewTask}<form class="ticket-form" onsubmit={(e)=>{e.preventDefault();void createTask();}}><label for="task-description">Description</label><textarea id="task-description" bind:value={taskDescription} required rows="3" placeholder="Describe the task"></textarea><div class="ticket-fields"><div><label for="task-agent">Agent</label><select id="task-agent" bind:value={selectedNode} required><option value="">Choose available agent</option>{#each availableNodes as node}<option value={node.id}>{node.name}</option>{/each}</select></div><div><label for="task-branch">Git branch</label><input id="task-branch" bind:value={branch}/></div></div><label for="task-files">Files of interest (comma-separated)</label><input id="task-files" bind:value={files} placeholder="src/main.py, README.md"/><div class="form-actions"><button type="button" class="secondary-button" onclick={()=>showNewTask=false}>Cancel</button><button class="primary-button" type="submit" disabled={assigning||!selectedNode||!taskDescription.trim()}>{assigning?'Assigning…':'Assign ticket'}</button></div></form>{/if}
<div class="task-toolbar"><label class="visually-hidden" for="task-search">Search tasks</label><input id="task-search" type="search" placeholder="Search tasks…" bind:value={query}/><label class="visually-hidden" for="task-status">Filter by status</label><select id="task-status" bind:value={filter}><option value="all">All statuses</option><option value="busy">In progress</option><option value="done">Completed</option><option value="error">Failed</option></select><label class="visually-hidden" for="task-agent-filter">Filter by agent</label><select id="task-agent-filter" bind:value={taskAgent}><option value="all">All agents</option>{#each nodes as node}<option value={node.id}>{node.name}</option>{/each}</select><label class="visually-hidden" for="task-sort">Sort tasks</label><select id="task-sort" bind:value={taskSort}><option value="newest">Newest first</option><option value="oldest">Oldest first</option></select><span>{visibleTasks.length} of {projectTasks.length}</span></div>
<div class="tasks-list">{#each visibleTasks as task (task.id)}<button class="task-item selectable-row" onclick={()=>inspectTask(task.id)}><StatusIcon status={task.status}/><div class="task-info"><strong>{task.ticket.description}</strong><small>#{task.id} · {nodes.find(n=>n.id===task['node-id'])?.name??task['node-id']} · {task.ticket['git-branch-id']} · {new Date(task.ticket['created-at']).toLocaleString()}</small></div><span class="task-pill">{task.status}</span></button>{:else}<p class="empty-state">No tasks match your filters.</p>{/each}</div></section>
{/if}
{#if notice}<p class="action-note" role="status">{notice}</p>{/if}
{#if selectedTask}<section class="task-detail" aria-label="Task details"><div class="task-detail-heading"><h2>Task #{selectedTask.id}</h2><button class="secondary-button" onclick={()=>selectedTask=null}>Close</button></div><div class="task-detail-status"><StatusIcon status={selectedTask.status}/>{selectedTask.status}</div><p>{selectedTask.ticket.description}</p><dl><dt>Assigned node</dt><dd>{nodes.find(n=>n.id===selectedTask?.['node-id'])?.name??selectedTask['node-id']}</dd><dt>Created by</dt><dd>{selectedTask.ticket['created-by']}</dd><dt>Git repository</dt><dd>{selectedTask.ticket['git-repo-url']||'—'}</dd><dt>Branch</dt><dd>{selectedTask.ticket['git-branch-id']}</dd><dt>Files</dt><dd>{selectedTask.ticket['files-of-interest'].join(', ')||'—'}</dd>{#if selectedTask.report}<dt>Finished</dt><dd>{selectedTask.report['finished-at']}</dd><dt>Report</dt><dd>{selectedTask.report.description}</dd>{/if}</dl>{#if selectedTask.report}<button class="secondary-button" onclick={()=>exportTaskReport(selectedTask!)}><Icon name="download"/>Download report</button>{/if}{#if selectedTask.status==='error'}<button class="secondary-button" disabled={retrying} onclick={()=>void retryTask()}><Icon name="refresh"/>{retrying?'Assigning…':'Create retry ticket'}</button>{/if}{#if selectedTask.status==='busy'}<button class="secondary-button" onclick={()=>void simulateComplete()}><Icon name="refresh"/>Refresh status</button>{/if}</section>{/if}
{:else}<h1>Project not found</h1>{/if}
</div></AppShell>
