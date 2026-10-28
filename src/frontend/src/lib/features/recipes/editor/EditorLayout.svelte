<script lang="ts">
  import type { Snippet } from 'svelte';
  import type { HTMLAttributes } from 'svelte/elements';

  /** A rail and a column, the second-level screen shape; shared with the skeleton so nothing moves when it lands. */
  interface Props extends HTMLAttributes<HTMLDivElement> {
    rail: Snippet;
    /** What the recipe was made from, kept in view while it is fixed: above the form, or beside it where there is room. */
    source?: Snippet;
    children: Snippet;
  }

  let { rail, source, children, ...rest }: Props = $props();
</script>

<div class="layout" class:sourced={Boolean(source)} {...rest}>
  <div class="rail-area">{@render rail()}</div>

  {#if source}
    <div class="source">{@render source()}</div>
  {/if}

  <div class="form">
    {@render children()}
  </div>
</div>

<style>
  .layout {
    display: grid;
    gap: var(--space-6);
    align-items: start;
    min-width: 0;
  }

  /* The rail itself is the grid item, so it stays sticky against the whole height of the page. */
  .rail-area {
    display: contents;
  }

  .source {
    min-width: 0;
  }

  .form {
    display: flex;
    flex-direction: column;
    gap: var(--layout-section-gap);
    min-width: 0;
    /* Capped short of the page: steps and ingredients are unreadable across a wide monitor. */
    max-width: 48rem;
  }

  @media (min-width: 64rem) {
    .layout {
      grid-template-columns: 14rem minmax(0, 1fr);
      gap: var(--space-12);
    }

    .sourced {
      grid-template-areas:
        'rail source'
        'rail form';
      row-gap: var(--space-6);
    }

    .sourced .rail-area > :global(*) {
      grid-area: rail;
    }

    .sourced .source {
      grid-area: source;
      max-width: 48rem;
    }

    .sourced .form {
      grid-area: form;
    }
  }

  @media (min-width: 90rem) {
    .sourced {
      grid-template-columns: 14rem minmax(0, 48rem) minmax(16rem, 1fr);
      grid-template-areas: 'rail form source';
    }

    .sourced .source {
      position: sticky;
      top: var(--pinned-top);
    }
  }
</style>
