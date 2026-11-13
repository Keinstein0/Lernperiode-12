<script lang="ts">
 import { onMount } from 'svelte';
 import AppShell from '#lib/components/AppShell.svelte';
 import Icon from '#lib/components/Icon.svelte';
 import { api } from '#lib/api';
 let polling=$state('4000'); let compact=$state(false); let connection=$state('Not checked');let testing=$state(false);let error=$state('');
 onMount(()=>{polling=localStorage.getItem('unisk-polling-ms')||'4000';compact=localStorage.getItem('unisk-compact')==='true';document.documentElement.classList.toggle('compact-mode',compact);});
 function save(){localStorage.setItem('unisk-polling-ms',polling);localStorage.setItem('unisk-compact',String(compact));document.documentElement.classList.toggle('compact-mode',compact);}
 async function test(){testing=true;error='';try{const nodes=await api.getNodeStatuses();connection=`Connected · ${nodes.length} agents available`; }catch(e){connection='Disconnected';error=e instanceof Error?e.message:String(e);}finally{testing=false;}}
</script>
<AppShell><div class="content-wrap settings-wrap"><div class="breadcrumbs">Workspace <span>/</span> Settings</div><header class="page-heading"><h1><Icon name="settings"/> Settings</h1></header>
<section class="settings-section"><h2>Frontend preferences</h2><p>Stored in this browser.</p><label for="polling">Status refresh interval</label><select id="polling" bind:value={polling} onchange={save}><option value="4000">4 seconds</option><option value="10000">10 seconds</option><option value="30000">30 seconds</option><option value="0">Manual only</option></select><label class="setting-check"><input type="checkbox" bind:checked={compact} onchange={save}/> Compact spacing</label></section>
<section class="settings-section"><h2>Router connection</h2><p>API mode: <code>{import.meta.env.VITE_API_MODE==='router'?'Router':'Mock'}</code></p><p role="status">{connection}</p>{#if error}<p class="error-note" role="alert">{error}</p>{/if}<button class="secondary-button" onclick={()=>void test()} disabled={testing}><Icon name="refresh"/>{testing?'Checking…':'Test connection'}</button></section>
<section class="settings-section"><h2>Repository integration</h2><p>Repository URLs and task branches are configured separately for each project. GitHub permissions and commit history require a future backend integration.</p></section></div></AppShell>
