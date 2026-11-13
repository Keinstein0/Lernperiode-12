<script>
 import { onMount } from 'svelte';
 import AppShell from '#lib/components/AppShell.svelte'; import Icon from '#lib/components/Icon.svelte'; import StatusIcon from '#lib/components/StatusIcon.svelte'; import { api } from '#lib/api';
 let nodes=$state([]); let busy=$state(false); let error=$state('');
 async function refresh() { busy=true; error=''; try { nodes=await api.getNodeStatuses(); } catch(e) {error=e.message;} finally {busy=false;} }
 onMount(() => { refresh(); });
</script>
<AppShell><div class="content-wrap"><div class="breadcrumbs">Workspace <span>/</span> Agents</div><header class="page-heading"><div><h1>Agents</h1></div><button class="secondary-button" onclick={refresh} disabled={busy}><Icon name="refresh"/>{busy?'Refreshing…':'Refresh status'}</button></header>
{#if error}<p class="error-note" role="alert">{error}</p>{/if}<section class="agent-group"><header><h2>All agents</h2><span class="count">{nodes.length}</span></header><div class="agent-directory">{#each nodes as node}<div class="directory-row"><div><strong class="agent-name"><Icon name="bot"/>{node.name}</strong><small>{node.id} · {node.type}{node['task-id'] ? ` · Task #${node['task-id']}` : ''}</small></div><span class="agent-status"><StatusIcon status={node.status}/>{node.status}</span></div>{/each}</div></section></div></AppShell>
