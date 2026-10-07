<script lang="ts">
  import type { Snippet } from 'svelte';

  /** One part of a recipe while written, headed like the reading surface (editorial face, title size) so structure is louder than field labels; the count answers "how long is this" at a glance. */
  interface Props {
    children: Snippet;
    /** Anchors the rail's link and the skip target. */
    id: string;
    title: string;
    /** One sentence: what the section is for, or what it deliberately is not. */
    description?: string;
    /** How many things are in it; omitted where counting says nothing. */
    count?: number;
    /** Sits at the end of the heading's line; one control at most. */
    action?: Snippet;
  }

  let { children, id, title, description, count, action }: Props = $props();
</script>

<!-- Focusable so the rail can land on it like a hash link; `tabindex="-1"` keeps it out of the tab order. -->
<!-- Named by the word, not the heading element, so a screen reader does not hear "Ingredients 14". -->
<section class="section" {id} tabindex="-1" aria-label={title}>
  <div class="head">
    <h2 class="title">
      {title}{#if count !== undefined}<span class="count">{count}</span>{/if}
    </h2>

    {#if action}
      <div class="action">{@render action()}</div>
    {/if}
  </div>

  {#if description}
    <p class="description">{description}</p>
  {/if}

  <div class="body">{@render children()}</div>
</section>

<style>
  .section {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    min-width: 0;
    /* Clears the floating header so a rail jump lands on the heading. */
    scroll-margin-top: var(--space-24);
    /* A landing target, never a thing with a ring of its own. */
    outline: none;
  }

  .head {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    justify-content: space-between;
    gap: var(--space-2) var(--space-4);
    min-width: 0;
  }

  /* Exactly the reading surface's section heading, so two screens agree. */
  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-2xl);
    font-weight: var(--weight-regular);
    letter-spacing: -0.025em;
    line-height: var(--leading-tight);
    min-width: 0;
  }

  /* Tabular, so a list going from 9 to 10 does not nudge the heading. */
  .count {
    margin-inline-start: var(--space-3);
    color: var(--text-muted);
    font-variant-numeric: tabular-nums;
  }

  .description {
    max-width: var(--measure);
    margin-top: calc(var(--space-2) * -1);
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  .body {
    min-width: 0;
  }
</style>
