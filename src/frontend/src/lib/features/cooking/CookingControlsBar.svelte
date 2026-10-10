<script lang="ts">
  import type { Snippet } from 'svelte';

  // Shared frame for the controls and their skeleton. `.moves`/`.advance` are styled here as `:global`
  // because both render them from different components.
  interface Props {
    children: Snippet;
    /** Bar height, so the surface can keep a step clear of it. */
    height?: number;
    /** For the skeleton: hides the bar from assistive tech. */
    decorative?: boolean;
  }

  let { children, height = $bindable(0), decorative = false }: Props = $props();
</script>

<div class="controls" aria-hidden={decorative ? 'true' : undefined} bind:clientHeight={height}>
  {@render children()}
</div>

<style>
  .controls {
    position: sticky;
    bottom: calc(max(var(--bottom-inset), env(safe-area-inset-bottom, 0px)) + var(--space-4));
    z-index: var(--z-sticky);
    display: grid;
    /* Wraps whole groups as space allows, including with enlarged text. */
    grid-template-columns: repeat(auto-fit, minmax(min(100%, 25rem), 1fr));
    align-items: center;
    gap: var(--space-3);
    margin-top: var(--space-8);
    padding: var(--space-3) var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-overlay);
    box-shadow: var(--shadow-overlay);
  }

  .controls :global(.moves) {
    display: grid;
    grid-template-columns: auto auto minmax(0, 1fr);
    align-items: center;
    min-width: 0;
    gap: var(--space-2);
  }

  /* Next takes the remaining space: it is the primary action. */
  .controls :global(.advance) {
    min-width: 0;
  }

  /* Short or narrow phones: the bar stays pinned, so it spends less of the screen. */
  @media screen and (max-height: 32rem), screen and (max-width: 24rem) {
    .controls {
      gap: var(--space-2);
      margin-top: var(--space-4);
      padding: var(--space-2) var(--space-3);
    }
  }
</style>
