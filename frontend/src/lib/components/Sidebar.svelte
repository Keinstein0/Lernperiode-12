<script>
 import { onMount } from 'svelte';
 import { api } from '#lib/api';
 import Icon from './Icon.svelte';
 let projects = $state([]);
 onMount(() => { api.listProjects().then((items) => projects = items).catch(() => {}); });
</script>
<aside class="sidebar">
 <div class="sidebar-group-label">Workspace</div>
 <nav class="sidebar-nav" aria-label="Main navigation"><a href="/"><Icon name="folders"/>Projects</a><a href="/agents"><Icon name="bot"/>Agents</a></nav>
 <div class="sidebar-group-label sidebar-separator">Recent projects</div>
 <nav class="sidebar-nav recent-projects" aria-label="Recent projects">{#each projects.slice(0,4) as project}<a href="/projects/{project.slug}"><Icon name="folder"/>{project.name}</a>{/each}</nav>
</aside>
