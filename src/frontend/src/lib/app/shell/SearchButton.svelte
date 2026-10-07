<script lang="ts">
  import { m } from '$shell/i18n';
  import { searchOverlay } from '$features/recipes/search/overlayState.svelte';
</script>

<button
  type="button"
  class="search"
  aria-label={m['search.open']()}
  title="{m['search.open']()} (⌘K)"
  aria-keyshortcuts="Meta+K Control+K /"
  onclick={() => searchOverlay.show()}
>
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true">
    <circle cx="10.5" cy="10.5" r="6.5" />
    <path d="m15.5 15.5 4 4" stroke-linecap="round" />
  </svg>
</button>

<style>
  /* The household's button beside it is drawn the same way, in its own file. */
  .search {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
    min-width: var(--control-sm);
    min-height: var(--control-sm);
    padding: var(--space-2);
    border: 0;
    border-radius: var(--radius-full);
    background: transparent;
    color: var(--text);
    cursor: pointer;
    transition: background-color var(--duration-fast) var(--ease-out);
  }

  .search:hover {
    background: var(--surface-selected);
  }

  .search:active {
    background: var(--surface-hover);
  }

  .search svg {
    width: calc(var(--space-4) + var(--space-1));
    height: calc(var(--space-4) + var(--space-1));
  }
</style>
