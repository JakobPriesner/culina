<script lang="ts">
  import type { Snippet } from 'svelte';
  import type { HTMLAttributes } from 'svelte/elements';

  /** A rail and a column, the second-level screen shape; shared with the skeleton so nothing moves when it lands. */
  interface Props extends HTMLAttributes<HTMLDivElement> {
    rail: Snippet;
    children: Snippet;
  }

  let { rail, children, ...rest }: Props = $props();
</script>

<div class="layout" {...rest}>
  {@render rail()}

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
  }
</style>
