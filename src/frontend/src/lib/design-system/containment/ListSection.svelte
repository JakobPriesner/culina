<script lang="ts">
  import type { Snippet } from 'svelte';

  import SectionLabel from '../display/SectionLabel.svelte';

  /**
   * A grouped list, the way a health record draws one (docs/food-log-redesign.md §4.4): one raised card whose first
   * row is its heading, with an optional value and an optional action at its end, then its rows with hairlines between
   * them. Rows are `ListRow`s. A section with nothing in it is its heading row alone, one row high: the heading's action
   * says "add", and the card keeps it in line with the full sections beside it. The whole heading row is the action's
   * target: a tap on the title does what its button does.
   */
  interface Props {
    title: string;
    /** A value that belongs to the whole section, at the end of its heading: a subtotal. */
    trailing?: Snippet;
    /** The section's own verb, as a small control at the end of its heading. */
    action?: Snippet;
    /** The rows; leave out for an empty section. */
    children?: Snippet;
    level?: 'h2' | 'h3';
  }

  let { title, trailing, action, children, level = 'h2' }: Props = $props();

  const id = $props.id();
</script>

<section class="section" aria-labelledby={id}>
  <header class="head">
    <SectionLabel as={level} tone="muted" {id}>{title}</SectionLabel>
    <span class="end">
      {#if trailing}<span class="trailing">{@render trailing()}</span>{/if}
      {#if action}{@render action()}{/if}
    </span>
  </header>
  {#if children}
    <ul class="group">
      {@render children()}
    </ul>
  {/if}
</section>

<style>
  .section {
    overflow: hidden;
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
    --field-surface: var(--field-surface-inset);
  }

  .head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-4);
    min-block-size: var(--control-lg);
    position: relative;
    padding: var(--space-1) var(--space-2) var(--space-1) var(--space-4);
  }

  /* The action's button reaches across its heading row; the title and the subtotal are text, nothing under it. */
  .end > :global(button)::after {
    content: '';
    position: absolute;
    inset: 0;
  }

  /*
   * A pressed button's scale would make it the overlay's containing block, shrinking the overlay to the button: a press
   * on the title would then end off it, and the click would never reach the button.
   */
  .end > :global(button:active:not(:disabled)) {
    transform: none;
  }

  .end {
    display: flex;
    align-items: center;
    gap: var(--space-2);
  }

  .trailing {
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-variant-numeric: tabular-nums;
  }

  /* The heading's own row, parted from the rows by a hairline across the card. */
  .group {
    margin: 0;
    padding: 0;
    border-block-start: 1px solid var(--border);
    list-style: none;
  }
</style>
