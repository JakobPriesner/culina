<script lang="ts">
  import type { Snippet } from 'svelte';

  import IntakeRuntime from '$features/import/IntakeRuntime.svelte';
  import NowCookingBar from '$features/cooking/NowCookingBar.svelte';

  /**
   * The slot above the bottom bar for what is under way, the bar's accessory as iOS has one; always reserved, so a bar
   * appearing never pushes the page. It stays while the bar recedes, following the bar's top.
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
    /* Follows the bottom bar's top as it recedes and returns. */
    transition: bottom var(--duration-base) var(--ease-out);
  }

  @media print {
    .dock {
      display: none !important;
    }
  }
</style>
