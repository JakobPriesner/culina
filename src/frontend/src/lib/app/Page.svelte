<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * The container every page inside the shell sits in.
   *
   * One rule, so the heading on a page lines up with the brand in the navbar
   * above it. Two pages that each choose their own width are two pages that
   * shift sideways as you move between them.
   */
  interface Props {
    children: Snippet;
    /** `reading` narrows to a comfortable measure for prose and lists. */
    width?: 'wide' | 'reading';
  }

  let { children, width = 'wide' }: Props = $props();
</script>

<div class="page {width}">{@render children()}</div>

<style>
  .page {
    max-width: var(--layout-wide);
    margin-inline: auto;
    padding: var(--space-8) var(--layout-gutter) var(--space-24);
  }

  .reading {
    /* Still starts on the shell's gutter; only the text stops earlier. */
    max-width: calc(var(--measure) + var(--layout-gutter) * 2);
    margin-inline: 0 auto;
  }

  @media (min-width: 48rem) {
    .reading {
      margin-inline: auto;
    }
  }
</style>
