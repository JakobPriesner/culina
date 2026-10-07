<script lang="ts">
  import type { Component } from 'svelte';

  import { session } from '$features/auth/session.svelte';
  import { searchOverlay } from '$features/recipes/search/overlayState.svelte';

  interface Props {
    searchable: boolean;
  }

  let { searchable }: Props = $props();

  let Overlay = $state<Component<{
    open: boolean;
    householdId: string;
    userId: string;
    onclose: () => void;
  }> | null>(null);

  // Lazy import on first open keeps the overlay out of the initial load.
  $effect(() => {
    if (searchOverlay.open && Overlay === null) {
      void import('$features/recipes/search/SearchOverlay.svelte').then((loaded) => {
        Overlay = loaded.default;
      });
    }
  });

  /** ⌘K / Ctrl-K anywhere, and "/" when not typing. */
  function shortcut(event: KeyboardEvent) {
    if (!searchable || searchOverlay.open) {
      return;
    }

    const target = event.target as HTMLElement | null;
    const typing = target?.closest('input, textarea, select, [contenteditable]') !== null;

    if ((event.key === 'k' && (event.metaKey || event.ctrlKey)) || (event.key === '/' && !typing)) {
      event.preventDefault();
      searchOverlay.show();
    }
  }
</script>

<svelte:window onkeydown={shortcut} />

{#if Overlay && session.user && session.activeHouseholdId}
  <Overlay
    open={searchOverlay.open}
    householdId={session.activeHouseholdId}
    userId={session.user.userId}
    onclose={() => searchOverlay.hide()}
  />
{/if}
