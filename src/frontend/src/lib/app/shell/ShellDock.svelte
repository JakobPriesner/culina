<script lang="ts">
  import type { Snippet } from 'svelte';

  import IntakeRuntime from '$features/import/IntakeRuntime.svelte';
  import NowCookingBar from '$features/cooking/NowCookingBar.svelte';

  /**
   * The slot above the bottom bar for what is under way; always reserved, so a bar appearing never
   * pushes the page.
   */
  interface Props {
    children?: Snippet;
    /** How tall the dock is, for the shell's bottom inset. */
    height?: number;
    /** Slid away with the phone's bottom bar, so one layer leaves and returns, never a pill left floating alone. */
    away?: boolean;
    /** Keyboard focus reached the dock while it was away: bring it, and the bar, back. */
    onfocusin?: () => void;
  }

  let { children, height = $bindable(0), away = false, onfocusin }: Props = $props();
</script>

<div class="dock" class:away bind:clientHeight={height} {onfocusin}>
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
    /* Follows the bottom bar as it slides away and back. */
    transition:
      bottom var(--duration-base) var(--ease-out),
      transform var(--duration-base) var(--ease-out);
  }

  /* Below the screen edge, past the home indicator, rather than removed, so the page does not reflow. */
  .away {
    transform: translateY(calc(100% + env(safe-area-inset-bottom, 0px)));
  }

  @media print {
    .dock {
      display: none !important;
    }
  }
</style>
