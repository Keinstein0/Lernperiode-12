<script>
  import Icon from './Icon.svelte';
  let { status, size = 17 } = $props();
  let normalized = $derived(status.toLowerCase());
  let name = $derived(normalized === 'closed' || normalized === 'complete' || normalized === 'completed' ? 'circle-check' : normalized === 'in progress' || normalized === 'working' || normalized === 'active' ? 'circle-dot' : normalized === 'offline' || normalized === 'idle' ? 'circle-pause' : normalized === 'online' ? 'circle-check' : 'circle-dashed');
  let tone = $derived(normalized === 'closed' || normalized === 'complete' || normalized === 'completed' || normalized === 'online' ? 'success' : normalized === 'in progress' || normalized === 'working' || normalized === 'active' ? 'progress' : 'muted');
</script>
<span class="status-symbol" class:success={tone === 'success'} class:progress={tone === 'progress'} class:muted={tone === 'muted'} role="img" aria-label={status} title={status}><Icon {name} {size}/></span>
