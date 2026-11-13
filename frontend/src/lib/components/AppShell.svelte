<script lang="ts">
 import { onMount } from 'svelte';
 import { goto } from '$app/navigation';
 import { api } from '#lib/api';
 import Sidebar from './Sidebar.svelte';
 import Topbar from './Topbar.svelte';
 let { children } = $props();
 let authenticated = $state(false);
 onMount(() => {
  let active = true;
  void api.getSession().then((session) => {
   if (!active) return;
   if (!session) void goto('/login');
   else authenticated = true;
  }).catch(() => { if (active) void goto('/login'); });
  return () => { active = false; };
 });
</script>
{#if authenticated}
 <div class="app-frame"><Topbar/><div class="app-body"><Sidebar/><main class="main-area">{@render children()}</main></div></div>
{:else}
 <div class="app-frame session-loading"><span role="status">Loading workspace…</span></div>
{/if}
