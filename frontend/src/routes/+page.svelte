<script>
 import { onMount } from 'svelte';
 import AppShell from '#lib/components/AppShell.svelte';
 import Icon from '#lib/components/Icon.svelte';
 import { api } from '#lib/api';
 let projects = $state([]); let taskCounts = $state({}); let loading = $state(true); let error = $state('');
 onMount(() => { api.listProjects().then(async (p) => { projects=p; const counts=await Promise.all(p.map(async project => [project.slug,(await api.listProjectTasks(project.slug)).length])); taskCounts=Object.fromEntries(counts); }).catch(e => error=e.message).finally(() => loading=false); });
</script>
<AppShell><div class="content-wrap"><div class="breadcrumbs">Workspace <span>/</span> Projects</div><header class="page-heading"><div><h1>Projects</h1></div></header>
{#if loading}<p class="muted-note" role="status">Loading projects…</p>{:else if error}<p class="error-note" role="alert">{error}</p>{:else}<section class="project-list" aria-label="Projects"><div class="list-header">Projects <span class="count">{projects.length}</span></div>{#each projects as project}<article class="project-row"><div class="project-row-title"><a href="/projects/{project.slug}"><Icon name="folder"/>{project.name}</a><span class="status-outline">{project.status}</span></div><p>{project.description}</p><div class="row-meta"><span>{taskCounts[project.slug]??0} tasks</span><span>Updated {project.updated}</span></div></article>{/each}</section>{/if}</div></AppShell>
