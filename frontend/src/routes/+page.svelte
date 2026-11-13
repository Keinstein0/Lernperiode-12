<script lang="ts">
 import { onMount } from 'svelte';
 import AppShell from '#lib/components/AppShell.svelte';
 import Icon from '#lib/components/Icon.svelte';
 import { api } from '#lib/api';
 import type { Project } from '#lib/api';
 let projects = $state<Project[]>([]); let taskCounts = $state<Record<string,number>>({}); let loading = $state(true); let error = $state('');
 let creating = $state(false); let saving = $state(false); let name = $state(''); let description = $state(''); let details = $state(''); let repository = $state('');
 async function reload() { try { projects=await api.listProjects(); const counts=await Promise.all(projects.map(async p => [p.slug,(await api.listProjectTasks(p.slug)).length] as const)); taskCounts=Object.fromEntries(counts); } catch(e) { error=e instanceof Error?e.message:String(e); } finally {loading=false;} }
 async function create() { saving=true;error='';try { await api.createProject({name,description,details,repository:repository||null});name='';description='';details='';repository='';creating=false;await reload(); }catch(e){error=e instanceof Error?e.message:String(e);}finally{saving=false;} }
 onMount(() => { void reload(); });
</script>
<AppShell><div class="content-wrap"><div class="breadcrumbs">Workspace <span>/</span> Projects</div><header class="page-heading"><div><h1>Projects</h1></div><button class="secondary-button" onclick={()=>creating=!creating}><Icon name="plus"/>New project</button></header>
{#if creating}<form class="ticket-form form-card" onsubmit={(e)=>{e.preventDefault();void create();}}><label for="pname">Name</label><input id="pname" bind:value={name} required maxlength="90"/><label for="pdesc">Description</label><input id="pdesc" bind:value={description} required/><label for="pdetails">Details</label><textarea id="pdetails" bind:value={details} rows="3"></textarea><label for="prepo">Git repository URL</label><input id="prepo" bind:value={repository} type="url" placeholder="https://github.com/owner/repository"/><div class="form-actions"><button class="secondary-button" type="button" onclick={()=>creating=false}>Cancel</button><button class="primary-button" type="submit" disabled={saving}>{saving?'Creating…':'Create project'}</button></div></form>{/if}
{#if loading}<p class="muted-note" role="status">Loading projects…</p>{:else if error}<p class="error-note" role="alert">{error}</p>{/if}
{#if !loading}<section class="project-list" aria-label="Projects"><div class="list-header">Projects <span class="count">{projects.length}</span></div>{#each projects as project}<article class="project-row"><div class="project-row-title"><a href="/projects/{project.slug}"><Icon name="folder"/>{project.name}</a><span class="status-outline">{project.status}</span></div><p>{project.description}</p><div class="row-meta"><span>{taskCounts[project.slug]??0} tasks</span>{#if project.repository}<span>Git repository linked</span>{/if}<span>Updated {project.updated}</span></div></article>{/each}</section>{/if}</div></AppShell>
