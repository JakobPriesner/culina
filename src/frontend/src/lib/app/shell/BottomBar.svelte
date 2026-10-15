<script lang="ts">
  import Navigation from '../Navigation.svelte';

  /** The destinations along the bottom of a compact screen; absent on a wide one. */
  interface Props {
    /** How tall the bar is, zero where it is not shown. */
    height?: number;
  }

  let { height = $bindable(0) }: Props = $props();
</script>

<div class="bar" bind:clientHeight={height}>
  <Navigation placement="bottom" />
</div>

<style>
  .bar {
    grid-area: bar;
    position: sticky;
    bottom: 0;
    z-index: var(--z-shell);
    /* The measured frame includes the floating gap and the home indicator, so other docks clear it. */
    padding-top: var(--space-2);
    padding-bottom: max(var(--space-2), env(safe-area-inset-bottom, 0px));
    padding-inline: max(var(--space-3), env(safe-area-inset-left, 0px))
      max(var(--space-3), env(safe-area-inset-right, 0px));
    pointer-events: none;
  }

  @media (width < 64rem) {
    :global(.shell.focused-cooking) .bar {
      display: none;
    }
  }

  @media (min-width: 64rem) {
    .bar {
      display: none;
    }
  }

  @media print {
    .bar {
      display: none !important;
    }
  }
</style>
