<script lang="ts">
  import type { Snippet } from 'svelte';

  import Page from '$shell/Page.svelte';

  /**
   * Woche: one page with two views, the plan and the list (docs/woche-design.md). Both sit in this one frame, so
   * flipping Plan | Liste keeps the switch and the heading where they were.
   */
  interface Props {
    children: Snippet;
  }

  let { children }: Props = $props();
</script>

<Page>{@render children()}</Page>
