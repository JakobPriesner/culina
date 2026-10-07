<script lang="ts">
  import type { Snippet } from 'svelte';

  import IntakeRuntime from '$features/import/IntakeRuntime.svelte';
  import NowCookingBar from '$features/cooking/NowCookingBar.svelte';

  /**
   * The slot above the bottom bar, for whatever is under way: an import, a
   * recipe being cooked, and a page's own bar. Reserved whether or not
   * anything is in it, so a bar appearing never pushes the page.
   */
  interface Props {
    children?: Snippet;
    /** How tall the dock is, for the shell's bottom inset. */
    height?: number;
  }

  let { children, height = $bindable(0) }: Props = $props();
</script>

<div class="dock" bind:clientHeight={height}>
  <IntakeRuntime />
  <NowCookingBar />
  {@render children?.()}
</div>

<style>
  .dock {
    min-width: 0;
    grid-area: dock;
    position: sticky;
    bottom: var(--bar-inset);
    z-index: var(--z-sticky);
  }

  @media print {
    .dock {
      display: none !important;
    }
  }
</style>
