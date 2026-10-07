<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * The floating frame the cooking controls — and their loading skeleton —
   * sit in, so both stand over the page in exactly the same place.
   *
   * `.moves` and `.advance` are the rows inside it. They are styled from here
   * as `:global` descendants of this frame, rather than twice, because the
   * controls and their skeleton render them from different components.
   */
  interface Props {
    children: Snippet;
    /** How tall the bar is, so the surface can keep a step out from under it. */
    height?: number;
    /** The skeleton is there to hold the place, not to be read. */
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
    /* Wrap whole groups according to their available space, including when
       text is enlarged. Navigation must never become a sliver beside them. */
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

  /* Next takes whatever is left. It is pressed once per step and Previous is
     pressed when something went wrong, and a target's size should say which is
     which. */
  .controls :global(.advance) {
    min-width: 0;
  }

  @media screen and (max-height: 32rem) {
    .controls {
      position: static;
    }
  }
</style>
