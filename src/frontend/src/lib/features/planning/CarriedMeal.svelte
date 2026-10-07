<script lang="ts">
  import type { Held } from './weekDrag.svelte';

  interface Props {
    held: Held;
    at: { x: number; y: number };
  }

  let { held, at }: Props = $props();
</script>

<!-- A copy, so the source day keeps its shape. -->
<div
  class="carried"
  aria-hidden="true"
  style:inline-size="{held.width}px"
  style:translate="{at.x}px {at.y}px"
>
  <span class="carried-title">{held.title}</span>
</div>

<style>
  .carried {
    position: fixed;
    top: 0;
    left: 0;
    z-index: var(--z-overlay);
    padding: var(--space-2);
    border-radius: var(--radius-sm);
    background: var(--surface-raised);
    box-shadow: var(--shadow-overlay);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    pointer-events: none;
    /* Offset above-left of the fingertip so a thumb does not cover it. */
    margin: calc(-1 * var(--space-6)) 0 0 calc(-1 * var(--space-4));
  }

  .carried-title {
    display: block;
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
  }
</style>
