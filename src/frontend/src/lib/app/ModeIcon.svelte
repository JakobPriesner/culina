<script lang="ts">
  import type { Mode } from './appearance';

  /** Sun, moon, screen: the one picture of each mode, for the header toggle and the account menu's segments alike. */
  let { mode }: { mode: Mode } = $props();
</script>

<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true">
  {#if mode === 'light'}
    <circle cx="12" cy="12" r="4.2" />
    <path
      d="M12 2.4v2.4M12 19.2v2.4M4.2 12H1.8M22.2 12h-2.4M6.3 6.3 4.6 4.6M19.4 19.4l-1.7-1.7M17.7 6.3l1.7-1.7M4.6 19.4l1.7-1.7"
      stroke-linecap="round"
    />
  {:else if mode === 'dark'}
    <path d="M20 13.4A8.4 8.4 0 1 1 10.6 4a6.9 6.9 0 0 0 9.4 9.4Z" stroke-linejoin="round" />
  {:else}
    <rect x="2.8" y="4.5" width="18.4" height="12.5" rx="1.8" />
    <path d="M8.5 20.5h7" stroke-linecap="round" />
  {/if}
</svg>

<style>
  svg {
    display: block;
    width: 100%;
    height: 100%;
  }
</style>
