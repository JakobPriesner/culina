<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * A distinct entity, or an interactive container. Not the default wrapper: when everything is a card
   * nothing stands out, so first ask whether a heading and whitespace would do.
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

  /* A two-pixel lift: enough to say "this responds" without making a grid breathe. */
  .interactive:hover {
    transform: translateY(-2px);
    box-shadow: var(--shadow-overlay);
  }
</style>
