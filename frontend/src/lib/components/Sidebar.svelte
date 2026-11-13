<script>
 import { page } from '$app/state';
 import { api } from '#lib/api';
 import Icon from './Icon.svelte';
 let projects = $state([]);
 $effect(() => {
  // Refresh the recent-project navigation on every route change, including
  // after a project is created or deleted.
  const location = page.url.pathname;
  let valid = true;
  void api.listProjects().then(items => { if (valid) projects = items; }).catch(() => {});
  return () => { valid = false; };
 });
</script>
<aside class="sidebar">
 <div class="sidebar-group-label">Workspace</div>
 <nav class="sidebar-nav" aria-label="Main navigation"><a href="/"><Icon name="folders"/>Projects</a><a href="/agents"><Icon name="bot"/>Agents</a><a href="/settings"><Icon name="settings"/>Settings</a></nav>
 <div class="sidebar-group-label sidebar-separator">Recent projects</div>
 <nav class="sidebar-nav recent-projects" aria-label="Recent projects">{#each projects.slice(0,4) as project}<a href="/projects/{project.slug}"><Icon name="folder"/>{project.name}</a>{/each}</nav>
</aside>
