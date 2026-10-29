<script lang="ts">
  import EdgeEffect from '$ds/display/EdgeEffect.svelte';

  import { page } from '$app/state';

  import { session } from '$features/auth/session.svelte';

  import Navigation from '../Navigation.svelte';
  import { barAfterScroll, shown, type BarScroll } from './barOnScroll';
  import SearchButton from './SearchButton.svelte';

  /**
   * The destinations along the bottom of a compact screen, with search beside them as its own circle, where iOS puts
   * it on an iPhone; absent on a wide screen, whose header holds both.
   */
  interface Props {
    /** Whether search is offered here; the shell decides, as for the header. */
    searchable: boolean;
    /** How tall the bar is, zero where it is not shown. */
    height?: number;
  }

  let { searchable, height = $bindable(0) }: Props = $props();

  /** Receded to icons while a page is read downwards, so a recipe gets the screen; see `barAfterScroll`. */
  let compact = $state(false);

  let scroll: BarScroll = shown;

  function follow(next: BarScroll) {
    scroll = next;
    compact = next.compact;
  }

  // No bar without a household, and nothing for the page to keep clear of.
  $effect(() => {
    if (!session.activeHouseholdId) height = 0;
  });

  // A new page starts with the bar in full.
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
    bind:clientHeight={height}
    onfocusin={() => follow({ compact: false, anchor: window.scrollY })}
  >
    <EdgeEffect edge="bottom" />
    <div class="row">
      <Navigation placement="bottom" {compact} />
      {#if searchable}<div class="find"><SearchButton /></div>{/if}
    </div>
  </div>
{/if}

<style>
  .bar {
    grid-area: bar;
    position: sticky;
    bottom: 0;
    z-index: var(--z-shell);
    /*
     * The measured frame includes the gap below the dock, so other docks clear it. The dock sits into the home
     * indicator's strip as an iOS tab bar does, never closer to the edge than a floating surface keeps.
     */
    padding-top: var(--space-2);
    padding-bottom: max(
      var(--screen-inset),
      calc(env(safe-area-inset-bottom, 0px) - var(--space-3))
    );
    padding-inline: max(var(--space-3), env(safe-area-inset-left, 0px))
      max(var(--space-3), env(safe-area-inset-right, 0px));
    pointer-events: none;
  }

  /* Destinations and search on one line, search a separate circle at the trailing end. */
  .row {
    position: relative;
    display: flex;
    align-items: stretch;
    justify-content: center;
    gap: var(--space-2);
    max-width: 36rem;
    margin-inline: auto;
  }

  .row > :global(nav) {
    flex: 1;
    min-width: 0;
    margin-inline: 0;
  }

  /* The same glass as the dock, as tall as it, and round. */
  .find {
    display: flex;
    flex-shrink: 0;
    aspect-ratio: 1;
    border-radius: var(--radius-full);
    background: var(--surface-nav-glass);
    backdrop-filter: var(--glass-filter);
    box-shadow: var(--shadow-glass);
    pointer-events: auto;
  }

  .find :global(.search) {
    width: 100%;
    height: 100%;
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
