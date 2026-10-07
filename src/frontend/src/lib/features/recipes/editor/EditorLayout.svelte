<script lang="ts">
  import type { Snippet } from 'svelte';
  import type { HTMLAttributes } from 'svelte/elements';

  /**
   * A rail and a column, which is the shape every other second-level screen in
   * this app has: the settings categories sit exactly here.
   *
   * Shared by the editor and its skeleton, so what is coming and what arrives
   * have the same shape and nothing moves when it lands.
   */
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
    /*
     * Capped well short of the page. A recipe step is a sentence and an
     * ingredient is three words; set across a 1400px monitor they are unreadable
     * and the fields are absurd. The rail takes the width the form gives up.
     */
    max-width: 48rem;
  }

  @media (min-width: 64rem) {
    .layout {
      grid-template-columns: 14rem minmax(0, 1fr);
      gap: var(--space-12);
    }
  }
</style>
