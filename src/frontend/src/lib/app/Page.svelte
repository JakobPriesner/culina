<script lang="ts">
  import type { Snippet } from 'svelte';

  /** The container every shell page sits in: one width rule so a page heading lines up with the navbar brand. */
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
    min-width: 0;
    padding-block: var(--layout-page-space) var(--space-24);
    padding-inline: var(--layout-gutter-start) var(--layout-gutter-end);
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
