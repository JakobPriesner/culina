<script lang="ts">
  import type { Snippet } from 'svelte';

  /** One refinement or quick search, offered as a pill. */
  interface Props {
    onclick: () => void;
    children: Snippet;
  }

  let { onclick, children }: Props = $props();
</script>

<button type="button" class="facet" {onclick}>{@render children()}</button>

<style>
  .facet {
    min-height: var(--control-sm);
    padding: 0 var(--space-3);
    border: 1px solid var(--border);
    border-radius: var(--radius-full);
    background: var(--surface-raised);
    color: var(--text);
    font: inherit;
    font-size: var(--text-sm);
    cursor: pointer;
  }
</style>
