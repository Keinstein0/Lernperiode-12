<script>
  import Icon from './Icon.svelte';
  let { status, size = 17 } = $props();
  let normalized = $derived(status.toLowerCase());
  let name = $derived(normalized === 'done' || normalized === 'closed' || normalized === 'complete' || normalized === 'completed' ? 'circle-check' : normalized === 'busy' || normalized === 'in progress' || normalized === 'working' || normalized === 'active' ? 'circle-dot' : normalized === 'error' || normalized === 'offline' || normalized === 'idle' ? 'circle-pause' : normalized === 'online' ? 'circle-check' : 'circle-dashed');
  let tone = $derived(normalized === 'done' || normalized === 'closed' || normalized === 'complete' || normalized === 'completed' || normalized === 'online' ? 'success' : normalized === 'busy' || normalized === 'in progress' || normalized === 'working' || normalized === 'active' ? 'progress' : 'muted');
</script>
<span class="status-symbol" class:success={tone === 'success'} class:progress={tone === 'progress'} class:muted={tone === 'muted'} role="img" aria-label={status} title={status}><Icon {name} {size}/></span>
