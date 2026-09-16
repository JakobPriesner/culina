<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * One titled part of a settings category.
   *
   * The title and the sentence under it sit *outside* the enclosure, because
   * type is what structures a page here and a border only encloses. What the
   * border encloses is the set of controls: a run of rows that belong to each
   * other, which is the one thing on a settings screen whitespace alone cannot
   * say — two settings a gap apart and two settings a gap apart from a third
   * look identical.
   *
   * Local to the settings routes on purpose. It is a page's furniture rather
   * than a primitive, and moving it into `$ds` would invite every other screen
   * to become a list of boxes.
   */
  interface Props {
    children: Snippet;
    /** Omitted when the section is the only one and the page header says it. */
    title?: string;
    /** One sentence. Why the setting exists, or what it does not do. */
    description?: string;
    /**
     * Drops the enclosure, for a section whose content is already a composed
     * block rather than a run of rows.
     */
    bare?: boolean;
  }

  let { children, title, description, bare = false }: Props = $props();
</script>

<section class="section">
  {#if title || description}
    <div class="heading">
      {#if title}
        <h2>{title}</h2>
      {/if}
      {#if description}
        <p class="description">{description}</p>
      {/if}
    </div>
  {/if}

  <div class="body" class:bare>{@render children()}</div>
</section>

<style>
  .section {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    min-width: 0;
  }

  .heading {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
  }

  h2 {
    font-size: var(--text-lg);
    font-weight: var(--weight-semibold);
    line-height: var(--leading-tight);
  }

  .description {
    max-width: var(--measure);
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  /* A hairline enclosure rather than a card: a shadow would lift settings off
     the page as if each group were a separate thing to open, and there are
     three of them stacked. */
  .body {
    min-width: 0;
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
    /* The rows are the container query's subject, so they can stack on a
       narrow panel on a wide monitor. */
    container-type: inline-size;
  }

  /* The line between two rows lives with the enclosure rather than with the
     row, because whether a row has a neighbour is the enclosure's knowledge and
     a sibling selector cannot cross a component boundary. */
  .body:not(.bare) > :global(* + *) {
    border-top: 1px solid var(--border);
  }

  .bare {
    border: none;
    border-radius: 0;
    background: none;
  }
</style>
