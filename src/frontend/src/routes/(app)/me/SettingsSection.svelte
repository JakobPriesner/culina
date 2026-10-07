<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * One titled part of a settings category; title and text sit outside the enclosure (type structures, a border only encloses).
   * Local to the settings routes: page furniture, not a `$ds` primitive.
   */
  interface Props {
    children: Snippet;
    /** Omitted when the section is the only one and the page header says it. */
    title?: string;
    description?: string;
    /** Drops the enclosure for already-composed content. */
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

  /* A hairline, not a card: a shadow would make each group look like a separate thing to open. */
  .body {
    min-width: 0;
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
    container-type: inline-size;
  }

  /* The divider lives with the enclosure: a row cannot know its neighbours across a component boundary. */
  .body:not(.bare) > :global(* + *) {
    border-top: 1px solid var(--border);
  }

  .bare {
    border: none;
    border-radius: 0;
    background: none;
  }
</style>
