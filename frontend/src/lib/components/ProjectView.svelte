<script>
 import {page} from '$app/state';
 import AppShell from './AppShell.svelte';
 import {projects,tasks} from '#lib/data/projects.js';
 let {tab='overview'}=$props();
 let project=$derived(projects.find(item=>item.slug===page.params.project));
 let projectTasks=$derived(tasks.filter(task=>task.project===page.params.project));
 const messages=[{author:'You',time:'09:34',body:'What should we prioritise next?'},{author:'Main agent',time:'09:35',body:'Navigation structure and task organisation are the current priorities.'},{author:'You',time:'09:36',body:'Keep the conversation separate from the task list.'}];
</script>
<AppShell><div class="content-wrap project-content">
 {#if project}
 <div class="breadcrumbs"><a href="/">Projects</a><span>/</span>{project.name}</div>
 <div class="project-heading"><div><h1>{project.name}</h1><p>{project.description}</p></div><span class="status-outline">{project.status}</span></div>
 <nav class="project-tabs" aria-label="Project sections"><a class:active={tab==='overview'} href="/projects/{project.slug}">Overview</a><a class:active={tab==='conversation'} href="/projects/{project.slug}/conversation">Conversation</a><a class:active={tab==='tasks'} href="/projects/{project.slug}/tasks">Tasks <span class="count">{projectTasks.length}</span></a></nav>
 {#if tab==='overview'}
 <div class="project-overview"><section class="overview-main"><h2>About</h2><p class="body-copy">{project.details}</p><h2 class="section-heading">Recent tasks</h2><div class="simple-list">{#each projectTasks.slice(0,4) as task}<div class="simple-row"><span class:completed={task.status==='Closed'} class="task-dot"></span><div><strong>{task.title}</strong><small>#{task.id} · {task.assignee}</small></div><span class="row-end">{task.status}</span></div>{/each}</div><a class="section-link" href="/projects/{project.slug}/tasks">View all tasks →</a></section><aside class="overview-side"><h2>Project details</h2><dl class="detail-list"><div><dt>Status</dt><dd>{project.status}</dd></div><div><dt>Created</dt><dd>{project.created}</dd></div><div><dt>Owner</dt><dd>{project.owner}</dd></div><div><dt>Agents</dt><dd>{project.agents}</dd></div></dl><h2 class="section-heading">Repository</h2><div class="repo-panel"><span class="repo-glyph">⌘</span><div><strong>{project.repository ?? 'No repository linked'}</strong><small>Git repository</small></div></div></aside></div>
 {:else if tab==='conversation'}
 <section class="conversation-panel" aria-label="Project conversation"><div class="conversation-header"><h2>Conversation</h2><span>Main agent</span></div><div class="conversation-stream">{#each messages as msg}<article class="conversation-message"><div class="message-meta"><strong>{msg.author}</strong><time>{msg.time}</time></div><p>{msg.body}</p></article>{/each}</div><div class="compose-area"><label for="message-input">Message</label><textarea id="message-input" rows="3" aria-label="Message" placeholder="Write a message…" disabled></textarea></div></section>
 {:else if tab==='tasks'}
 <section class="tasks-panel"><div class="tasks-header"><h2>Tasks</h2><span>{projectTasks.length} total</span></div><div class="tasks-list">{#each projectTasks as task}<div class="task-item"><span class:completed={task.status==='Closed'} class="task-dot"></span><div class="task-info"><strong>{task.title}</strong><small>#{task.id} · {task.assignee} · {task.date}</small></div><span class="task-pill">{task.status}</span></div>{/each}</div></section>
 {/if}
 {:else}<div class="breadcrumbs"><a href="/">Projects</a></div><h1>Project not found</h1>{/if}
</div></AppShell>
