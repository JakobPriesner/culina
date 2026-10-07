<script lang="ts">
  import type { Held } from './weekDrag.svelte';

  interface Props {
    held: Held;
    /** Where the pointer is. */
    at: { x: number; y: number };
  }

  let { held, at }: Props = $props();
</script>

<!-- The card under the pointer. A copy rather than the card itself, so the day
     it came from keeps its shape and the gaps stay where they were aimed at. -->
<div
  class="carried"
  aria-hidden="true"
  style:inline-size="{held.width}px"
  style:translate="{at.x}px {at.y}px"
>
  <span class="carried-title">{held.title}</span>
</div>

<style>
  /* Under the pointer, and out of everything's way: it is not in the document
     flow, it does not take the pointer, and it is not in the accessibility
     tree — the card it was copied from is still all three of those. */
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
    /* Held just above and left of the fingertip, so a thumb does not cover the
       thing it is carrying. */
    margin: calc(-1 * var(--space-6)) 0 0 calc(-1 * var(--space-4));
  }

  .carried-title {
    display: block;
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
  }
</style>
