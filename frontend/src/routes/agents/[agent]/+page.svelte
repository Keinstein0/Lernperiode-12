<script lang="ts">
 import { onMount } from 'svelte';
 import { page } from '$app/state';
 import AppShell from '#lib/components/AppShell.svelte';
 import Icon from '#lib/components/Icon.svelte';
 import StatusIcon from '#lib/components/StatusIcon.svelte';
 import { api } from '#lib/api';
 import type { Node, Task } from '#lib/api';
 let agent=$state<Node|null>(null);let tasks=$state<Task[]>([]);let error=$state('');let loading=$state(true);
 let active=$derived(tasks.filter(t=>t.status==='busy'));
 async function refresh(){const id=page.params.agent;try{const [nodes,allTasks]=await Promise.all([api.getNodeStatuses(),api.listTasks()]);if(page.params.agent!==id)return;agent=nodes.find(n=>n.id===id)??null;tasks=allTasks.filter(t=>t['node-id']===id);}catch(e){error=e instanceof Error?e.message:String(e);}finally{loading=false;}}
 onMount(()=>{void refresh();const timer=setInterval(()=>void refresh(),Number(localStorage.getItem('unisk-polling-ms') || '4000') || 2147483647);return ()=>clearInterval(timer);});
</script>
<AppShell><div class="content-wrap"><div class="breadcrumbs"><a href="/agents">Agents</a><span>/</span>{page.params.agent}</div>
{#if loading}<p class="muted-note">Loading agent…</p>{:else if error}<p class="error-note" role="alert">{error}</p>{:else if agent}
<header class="page-heading"><div><h1><Icon name="bot" size={21}/>{agent.name}</h1><p class="agent-type">{agent.type}</p></div><span class="agent-status"><StatusIcon status={agent.status}/>{agent.status}</span></header>
<div class="agent-summary"><dl class="detail-list"><div><dt><Icon name="activity"/>Status</dt><dd>{agent.status}</dd></div><div><dt><Icon name="user"/>Node ID</dt><dd>{agent.id}</dd></div><div><dt><Icon name="tasks"/>Active tasks</dt><dd>{active.length}</dd></div><div><dt><Icon name="circle-check"/>Completed</dt><dd>{tasks.filter(t=>t.status==='done').length}</dd></div></dl></div>
<section class="agent-group"><header><h2>Assigned tasks</h2><span class="count">{tasks.length}</span></header><div class="tasks-list">{#each tasks as task (task.id)}<div class="task-item"><StatusIcon status={task.status}/><div class="task-info"><strong>{task.ticket.description}</strong><small>#{task.id} · {task.ticket['git-branch-id']}</small>{#if task.report}<small>{task.report.description}</small>{/if}</div><span class="task-pill">{task.status}</span></div>{:else}<p class="empty-state">No assigned tasks.</p>{/each}</div></section>
{:else}<h1>Agent not found</h1>{/if}</div></AppShell>
