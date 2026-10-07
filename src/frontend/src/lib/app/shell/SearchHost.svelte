<script lang="ts">
  import type { Component } from 'svelte';

  import { session } from '$features/auth/session.svelte';
  import { searchOverlay } from '$features/recipes/search/overlayState.svelte';

  /**
   * Search as the shell offers it: the shortcut, and the overlay it opens.
   *
   * The overlay is fetched the first time it is opened. Nobody pays for it on
   * first load: it is a few kilobytes that only matter once somebody reaches
   * for search, and by then a moment's import is hidden behind the sheet
   * rising.
   */
  interface Props {
    /** Whether search is on offer on this page. */
    searchable: boolean;
  }

  let { searchable }: Props = $props();

  let Overlay = $state<Component<{
    open: boolean;
    householdId: string;
    userId: string;
    onclose: () => void;
  }> | null>(null);

  $effect(() => {
    if (searchOverlay.open && Overlay === null) {
      void import('$features/recipes/search/SearchOverlay.svelte').then((loaded) => {
        Overlay = loaded.default;
      });
    }
  });

  /**
   * ⌘K / Ctrl-K anywhere, and "/" wherever nothing is being typed — the habit
   * people already have from every other app with a search.
   */
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
