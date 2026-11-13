<script>
 import Icon from './Icon.svelte';
</script>
<aside class="sidebar">
 <div class="sidebar-group-label">Workspace</div>
 <nav class="sidebar-nav" aria-label="Main navigation">
  <a href="/"><Icon name="folders"/>Projects</a>
  <a href="/agents"><Icon name="bot"/>Agents</a>
 </nav>
 <div class="sidebar-group-label sidebar-separator">Recent projects</div>
 <nav class="sidebar-nav recent-projects" aria-label="Recent projects">
  <a href="/projects/website-redesign"><Icon name="folder"/>Website redesign</a>
  <a href="/projects/research-pipeline"><Icon name="folder"/>Research pipeline</a>
  <a href="/projects/code-review"><Icon name="folder"/>Code review</a>
  <a href="/projects/documentation"><Icon name="folder"/>Documentation</a>
 </nav>
</aside>
