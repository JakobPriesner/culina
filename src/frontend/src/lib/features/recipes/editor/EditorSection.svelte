<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * One part of a recipe, while it is being written.
   *
   * The headings are the same headings the recipe is read under — editorial
   * face, title size — and that is the whole point of this component existing.
   * The editor used to caption its parts in small letterspaced capitals, which
   * made every section heading *quieter* than the field labels underneath it:
   * a page where the labels shout and the structure whispers is a page with no
   * structure at all.
   *
   * The count beside the heading is not decoration. "Ingredients" over a list
   * you have to measure by eye is a heading; "Ingredients 14" is an answer to
   * the question somebody scrolling a long recipe actually has.
   */
  interface Props {
    children: Snippet;
    /** Anchors the rail's link and the skip target. */
    id: string;
    title: string;
    /** One sentence. What the section is for, or what it deliberately is not. */
    description?: string;
    /** How many things are in it. Omitted where counting says nothing. */
    count?: number;
    /** Sits on the heading's own line, at its end. One control at most. */
    action?: Snippet;
  }

  let { children, id, title, description, count, action }: Props = $props();
</script>

<!-- Focusable so the rail can land on it, the way a hash link would.
     `tabindex="-1"` keeps it out of the tab order. -->
<!--
  Named by the word, not by the heading element: the count beside it is for the
  eye, and folding it into the name would make this "Ingredients 14" to a screen
  reader and to anything looking for the ingredients.
-->
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
    /* Cleared of the app's floating header, so a jump from the rail lands on
       the heading rather than just under it. */
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

  /* The reading surface's section heading, exactly. Two screens describing the
     same recipe should not disagree about what "Ingredients" looks like. */
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
