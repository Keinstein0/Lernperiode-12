<script>
  /** Shared 16px outline icons, following the Lucide visual language. */
  let { name, size = 16, strokeWidth = 1.8 } = $props();
</script>

<svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width={strokeWidth} stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false" class="ui-icon">
  {#if name === 'folders'}
    <rect x="8" y="4" width="13" height="16" rx="2"/><path d="M4 8H3a1 1 0 0 0-1 1v11a2 2 0 0 0 2 2h12a1 1 0 0 0 1-1v-1"/><path d="M8 8h13"/>
  {:else if name === 'folder'}
    <path d="M3 7a2 2 0 0 1 2-2h5l2 2h7a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>
  {:else if name === 'bot'}
    <rect x="3" y="7" width="18" height="14" rx="3"/><path d="M12 3v4M9 3h6M3 13H1m22 0h-2"/><circle cx="8.5" cy="13" r="1" fill="currentColor" stroke="none"/><circle cx="15.5" cy="13" r="1" fill="currentColor" stroke="none"/><path d="M9 17h6"/>
  {:else if name === 'user'}
    <circle cx="12" cy="8" r="4"/><path d="M4 21a8 8 0 0 1 16 0"/>
  {:else if name === 'layout'}
    <rect x="3" y="3" width="18" height="18" rx="2"/><path d="M3 9h18M9 9v12"/>
  {:else if name === 'message'}
    <path d="M21 11.5a8.5 8.5 0 0 1-8.5 8.5 9 9 0 0 1-4-.9L3 21l1.9-5.5a9 9 0 0 1-.9-4A8.5 8.5 0 0 1 12.5 3 8.5 8.5 0 0 1 21 11.5Z"/>
  {:else if name === 'tasks'}
    <rect x="3" y="3" width="18" height="18" rx="2"/><path d="m7 9 2 2 4-4M14 10h4M7 16h11"/>
  {:else if name === 'git'}
    <circle cx="6" cy="3" r="2"/><path d="M6 5v12"/><circle cx="6" cy="19" r="2"/><circle cx="18" cy="6" r="2"/><path d="M18 8v2a6 6 0 0 1-6 6H6"/>
  {:else if name === 'circle-check'}
    <circle cx="12" cy="12" r="9"/><path d="m8 12 2.5 2.5L16 9"/>
  {:else if name === 'circle-dot'}
    <circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="3"/>
  {:else if name === 'circle-dashed'}
    <path d="M12 3a9 9 0 0 1 4.5 1.2M19 7a9 9 0 0 1 1.8 4.5M20 15a9 9 0 0 1-3 4M13 21a9 9 0 0 1-4.5-1.2M5 17a9 9 0 0 1-1.8-4.5M4 9a9 9 0 0 1 3-4"/>
  {:else if name === 'circle-pause'}
    <circle cx="12" cy="12" r="9"/><path d="M10 9v6m4-6v6"/>
  {:else if name === 'send'}
    <path d="m22 2-7 20-4-9-9-4Z"/><path d="M22 2 11 13"/>
  {:else if name === 'download'}
    <path d="M12 3v12m-5-5 5 5 5-5M4 17v4h16v-4"/>
  {:else if name === 'copy'}
    <rect x="8" y="8" width="13" height="13" rx="2"/><path d="M16 8V5a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h3"/>
  {:else if name === 'settings'}
    <circle cx="12" cy="12" r="3"/><path d="M10 2h4l.7 2.5 2 .8L19 4l3 3-1.3 2.3.8 2L24 12l-2.5.7-.8 2L22 17l-3 3-2.3-1.3-2 .8L14 22h-4l-.7-2.5-2-.8L5 20l-3-3 1.3-2.3-.8-2L0 12l2.5-.7.8-2L2 7l3-3 2.3 1.3 2-.8z"/>
  {:else if name === 'filter'}
    <path d="M4 7h16M7 12h10m-7 5h4"/>
  {:else if name === 'external'}
    <path d="M13 5h6v6m0-6-9 9"/><path d="M19 13v5a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2h5"/>
  {:else if name === 'edit'}
    <path d="M12 20h9M4 16l-1 5 5-1L20 8a2.8 2.8 0 0 0-4-4L4 16Z"/>
  {:else if name === 'trash'}
    <path d="M3 6h18M8 6V4h8v2m3 0-1 15H6L5 6m5 4v7m4-7v7"/>
  {:else if name === 'plus'}
    <path d="M12 5v14M5 12h14"/>
  {:else if name === 'refresh'}
    <path d="M20 11a8 8 0 0 0-14-5L4 8m0-5v5h5M4 13a8 8 0 0 0 14 5l2-2m0 5v-5h-5"/>
  {:else if name === 'clock'}
    <circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>
  {:else if name === 'calendar'}
    <rect x="3" y="5" width="18" height="16" rx="2"/><path d="M7 3v4m10-4v4M3 10h18"/>
  {:else if name === 'activity'}
    <path d="M3 12h4l3-7 4 14 3-7h4"/>
  {:else if name === 'arrow-left'}
    <path d="m12 19-7-7 7-7M5 12h14"/>
  {:else if name === 'github'}
    <path d="M9 19c-4.3 1.3-4.3-2.5-6-3m12 6v-3.3a3 3 0 0 0-.8-2.3c2.7-.3 5.5-1.3 5.5-6a4.7 4.7 0 0 0-1.3-3.2 4.3 4.3 0 0 0-.1-3.2S17.2 3.7 15 5a13 13 0 0 0-6 0C6.8 3.7 5.7 4 5.7 4a4.3 4.3 0 0 0-.1 3.2 4.7 4.7 0 0 0-1.3 3.2c0 4.7 2.8 5.7 5.5 6A3 3 0 0 0 9 18.7V22"/>
  {/if}
</svg>
