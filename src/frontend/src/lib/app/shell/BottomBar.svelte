<script lang="ts">
  import { page } from '$app/state';

  import { session } from '$features/auth/session.svelte';

  import Navigation from '../Navigation.svelte';
  import { barAfterScroll, shown, type BarScroll } from './barOnScroll';

  /** The destinations along the bottom of a compact screen; absent on a wide one. */
  interface Props {
    /** How tall the bar is, zero where it is not shown. */
    height?: number;
    /** Slid below the screen edge while a page is read downwards, so a recipe gets the screen; see `barAfterScroll`. */
    hidden?: boolean;
  }

  let { height = $bindable(0), hidden = $bindable(false) }: Props = $props();

  let scroll: BarScroll = shown;

  function follow(next: BarScroll) {
    scroll = next;
    hidden = next.hidden;
  }

  // No bar without a household, and nothing for the page to keep clear of.
  $effect(() => {
    if (!session.activeHouseholdId) height = 0;
  });

  // A new page starts with the way out in view.
  $effect(() => {
    void page.url.pathname;
    follow(shown);
  });

  let frame = 0;

  /** When the reader last moved the page themselves; a fling keeps scrolling a while after the finger lifts. */
  let touched = -Infinity;
  const lingers = 1000;
  const scrollKeys = new Set(['ArrowDown', 'ArrowUp', 'PageDown', 'PageUp', 'Home', 'End', ' ']);

  function reader() {
    touched = performance.now();
  }

  function onkeydown(event: KeyboardEvent) {
    if (scrollKeys.has(event.key)) reader();
  }

  function read() {
    frame = 0;
    const root = document.documentElement;
    const atEnd = window.scrollY + window.innerHeight >= root.scrollHeight - 2;
    const byReader = performance.now() - touched < lingers;
    const next = barAfterScroll(scroll, window.scrollY, atEnd, byReader);

    follow(next);
  }

  function onscroll() {
    if (!frame) frame = requestAnimationFrame(read);
  }
</script>

<svelte:window {onscroll} onwheel={reader} ontouchstart={reader} ontouchmove={reader} {onkeydown} />

{#if session.activeHouseholdId}
  <div
    class="bar"
    class:hidden
    bind:clientHeight={height}
    onfocusin={() => follow({ hidden: false, anchor: window.scrollY })}
  >
    <Navigation placement="bottom" />
  </div>
{/if}

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
    transition: transform var(--duration-base) var(--ease-out);
  }

  /* Slid below the screen edge rather than removed, so the page does not reflow under the reader. */
  .hidden {
    transform: translateY(100%);
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
