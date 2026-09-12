<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * A distinct entity, or a container that is itself interactive.
   *
   * **A card is not the default wrapper for a block of content.** This is the
   * rule most likely to erode, so it is written here: when everything is a
   * card, nothing stands out, and a page of boxes takes longer to read than the
   * same content separated by space and type. Before reaching for this, ask
   * whether a heading and some whitespace would do — they usually would.
   *
   * Legitimate uses: a recipe in a grid, a household in a list, anything the
   * whole surface of which is one link or one button.
   */
  interface Props {
    children: Snippet;
    /** Makes the whole card one link. The card then has exactly one action. */
    href?: string;
    /** Removes the padding, for a card that starts with an edge-to-edge image. */
    flush?: boolean;
  }

  let { children, href, flush = false }: Props = $props();
</script>

{#if href}
  <a class="card interactive" class:flush {href}>{@render children()}</a>
{:else}
  <div class="card" class:flush>{@render children()}</div>
{/if}

<style>
  .card {
    display: block;
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
    box-shadow: var(--shadow-card);
    padding: var(--space-4);
    color: inherit;
    overflow: hidden;
  }

  .flush {
    padding: 0;
  }

  .interactive {
    text-decoration: none;
    transition:
      transform var(--duration-fast) var(--ease-out),
      box-shadow var(--duration-fast) var(--ease-out);
  }

  /* A lift of two pixels, not four: enough to say "this responds", not enough
     to make a grid of them feel like it is breathing. */
  .interactive:hover {
    transform: translateY(-2px);
    box-shadow: var(--shadow-overlay);
  }
</style>
