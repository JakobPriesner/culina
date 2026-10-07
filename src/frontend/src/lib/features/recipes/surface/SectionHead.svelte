<script lang="ts">
  import type { Snippet } from 'svelte';

  // One component for both headings so they align across columns with or without a control.
  interface Props {
    title: string;
    children?: Snippet;
  }

  let { title, children }: Props = $props();
</script>

<div class="section-head">
  <h2 class="section">{title}</h2>

  {@render children?.()}
</div>

<style>
  .section-head {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2);
    /* Capped so the control sits in the same place at any section width. */
    max-width: var(--side-column);
    /* Equal height so a control doesn't push its heading below the one in the next column. */
    min-height: var(--control-lg);
    margin-bottom: var(--space-3);
  }

  .section {
    font-family: var(--font-editorial);
    font-size: var(--text-2xl);
    font-weight: var(--weight-regular);
    letter-spacing: -0.025em;
    color: var(--text);
    margin-bottom: 0;
  }

  @media screen and (width < 64rem) {
    .section-head {
      max-width: none;
    }
  }

  @media print {
    .section-head {
      min-height: 0;
    }
  }
</style>
