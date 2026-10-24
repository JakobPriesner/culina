<script lang="ts">
  import type { Snippet } from 'svelte';

  import SectionLabel from '../display/SectionLabel.svelte';

  /**
   * A grouped list, the way a settings page or a health record draws one (docs/food-log-redesign.md §4.4): a small
   * heading with an optional value and an optional action at its end, then its rows on one raised surface with
   * hairlines between them. Rows are `ListRow`s. A section with nothing in it is its heading alone: an empty box would
   * say "missing", the heading's action says "add".
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
  .head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-4);
    min-block-size: var(--control-sm);
    padding: 0 var(--space-2) var(--space-1) var(--space-4);
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

  .group {
    margin: 0;
    padding: 0;
    overflow: hidden;
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
    list-style: none;
  }
</style>
