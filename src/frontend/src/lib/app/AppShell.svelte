<script lang="ts">
  import type { Snippet } from 'svelte';

  import { navigating, page } from '$app/state';
  import { resolve } from '$app/paths';
  import { Toaster } from '$ds';

  import type { Component } from 'svelte';

  import { session } from '$features/auth/session.svelte';
  import NowCookingBar from '$features/cooking/NowCookingBar.svelte';
  import { searchOverlay } from '$features/recipes/search/overlayState.svelte';

  import Brand from './Brand.svelte';
  import { connection } from './connection.svelte';
  import { m } from './i18n';
  import { offersNewRecipe, offersSearch } from './navigation';
  import Navigation from './Navigation.svelte';
  import NewRecipeLink from './NewRecipeLink.svelte';

  /**
   * The frame every signed-in page sits in.
   *
   * One set of destinations moves from the top on desktop to the bottom on
   * compact screens. The dock reserves space for the cooking bar.
   */
  interface Props {
    children: Snippet;
    /** Reserved for the bar that appears while a recipe is being cooked. */
    dock?: Snippet;
  }

  let { children, dock }: Props = $props();

  /**
   * How much of the viewport bottom is already spoken for.
   *
   * The dock and the bottom bar are their own grid rows, so they never cover
   * page content that simply flows. They do cover anything a page sticks to the
   * bottom of the viewport — an action footer, say — and a "Start cooking"
   * button sliced in half by the cooking bar is exactly the kind of detail that
   * makes an app feel unfinished. Pages read this instead of guessing.
   *
   * Measured rather than declared: the bottom bar is display:none on a wide
   * screen and reports zero, so one expression covers both layouts.
   */
  let dockHeight = $state(0);
  let barHeight = $state(0);

  /**
   * How tall the floating header is, for the few pages that put something of
   * their own on its line.
   *
   * On a tablet the header is the brand and nothing else — the destinations are
   * down on the bottom bar — so its right half is empty, and a page with its
   * own navigation can use it. Lining up with the brand means knowing where the
   * brand's line is, and the brand's height is its wordmark's, which is a font
   * metric: measured for the same reason the bar below is.
   *
   * Left unset until it has been measured, so the token's own figure holds the
   * place through the server's render rather than a zero that would put those
   * pills half off the top edge until the page hydrates.
   */
  let headerHeight = $state(0);

  /** See `offersNewRecipe`: only where a new recipe would belong to what is on screen. */
  const creating = $derived(offersNewRecipe(page.url.pathname));
  const searchable = $derived(
    offersSearch(page.url.pathname) && session.activeHouseholdId !== null
  );

  /**
   * The search overlay, fetched the first time it is opened.
   *
   * Nobody pays for it on first load: it is a few kilobytes that only matter
   * once somebody reaches for search, and by then a moment's import is
   * hidden behind the sheet rising.
   */
  let Overlay = $state<Component<{
    open: boolean;
    householdId: string;
    onclose: () => void;
  }> | null>(null);

  $effect(() => {
    if (searchOverlay.open && Overlay === null) {
      void import('$features/recipes/search/SearchOverlay.svelte').then((loaded) => {
        Overlay = loaded.default;
      });
    }
  });

  /**
   * ⌘K / Ctrl-K anywhere, and "/" wherever nothing is being typed — the habit
   * people already have from every other app with a search.
   */
  function shortcut(event: KeyboardEvent) {
    if (!searchable || searchOverlay.open) {
      return;
    }

    const target = event.target as HTMLElement | null;
    const typing = target?.closest('input, textarea, select, [contenteditable]') !== null;

    if ((event.key === 'k' && (event.metaKey || event.ctrlKey)) || (event.key === '/' && !typing)) {
      event.preventDefault();
      searchOverlay.show();
    }
  }
  /**
   * How tall the window is, so the shell can tell when it is mostly furniture.
   */
  let viewportHeight = $state(0);

  /**
   * True when the header, the dock and the bar would take half the screen.
   *
   * Every one of them is sized by its contents, so all three grow when somebody
   * enlarges text — and none of them knows about the others. On a 320x568
   * phone at 200% the header alone is 208px, the dock 120 and the navigation
   * 193: 521 pixels of chrome around 47 pixels of recipe. No media query can
   * see this, because text scale is not a thing a media query is told about;
   * the shell already measures all three, so it is the one place that can.
   */
  const crowded = $derived(
    viewportHeight > 0 && headerHeight + dockHeight + barHeight > viewportHeight / 2
  );
</script>

<svelte:window bind:innerHeight={viewportHeight} onkeydown={shortcut} />

<div
  class="shell"
  class:crowded
  style:--bar-inset="{barHeight}px"
  style:--bottom-inset="{dockHeight + barHeight}px"
  style:--header-inset={headerHeight ? `${headerHeight}px` : null}
>
  <!-- First in the tab order and invisible until focused: without it, reaching
       the page content by keyboard means tabbing through the navigation on
       every single page. -->
  <a class="skip" href="#content">{m['nav.skip']()}</a>

  <header class="header" bind:clientHeight={headerHeight}>
    {#if navigating.to}
      <span class="progress" role="progressbar" aria-label={m['app.navigating']()}></span>
    {/if}

    <div class="header-inner">
      <a class="brand" href={resolve('/(app)')} aria-label={m['app.name']()}><Brand /></a>

      <div class="wide-only"><Navigation placement="top" /></div>

      {#if creating || searchable}
        <div class="library-controls">
          {#if searchable}
            <button
              type="button"
              class="search"
              aria-label={m['search.open']()}
              title="{m['search.open']()} (⌘K)"
              aria-keyshortcuts="Meta+K Control+K /"
              onclick={() => searchOverlay.show()}
            >
              <svg
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="1.8"
                aria-hidden="true"
              >
                <circle cx="10.5" cy="10.5" r="6.5" />
                <path d="m15.5 15.5 4 4" stroke-linecap="round" />
              </svg>
            </button>
          {/if}
          {#if creating}
            <NewRecipeLink />
          {/if}
        </div>
      {/if}
      {#if !connection.online}
        <div class="status"><p class="offline">{m['connection.offline']()}</p></div>
      {/if}
    </div>
  </header>

  <main class="content" id="content" tabindex="-1">
    {@render children()}
  </main>

  <!-- The slot is reserved whether or not anything is in it, so the bar
       appearing never pushes the page. -->
  <div class="dock" bind:clientHeight={dockHeight}>
    <NowCookingBar />
    {@render dock?.()}
  </div>

  <div class="bar narrow-only" bind:clientHeight={barHeight}>
    <Navigation placement="bottom" />
  </div>

  <Toaster label={m['app.notifications']()} dismissLabel={m['app.dismiss']()} />

  {#if Overlay && session.activeHouseholdId}
    <Overlay
      open={searchOverlay.open}
      householdId={session.activeHouseholdId}
      onclose={() => searchOverlay.hide()}
    />
  {/if}
</div>

<style>
  .shell {
    display: grid;
    min-height: 100dvh;
    grid-template-columns: minmax(0, 1fr);
    grid-template-rows: auto 1fr auto auto;
    grid-template-areas:
      'header'
      'content'
      'dock'
      'bar';
  }

  .header {
    grid-area: header;
    position: sticky;
    top: 0;
    z-index: var(--z-sticky);
    pointer-events: none;
  }

  /* The page fading out under the pills instead of being cut in half at the
     top edge. A gradient and not a blur: what passes under here is a centred
     column on a flat background, so a full-width backdrop-filter would spend
     every scroll frame blurring the gutters on either side of it. */
  .header::before {
    content: '';
    position: absolute;
    inset-block-start: 0;
    inset-inline: 0;
    height: calc(100% + var(--space-8));
    background: linear-gradient(to bottom, var(--surface) 35%, transparent);
  }

  /* Three floating groups share one row: brand, destinations, and — on the
     library alone — the way to write a new recipe. The third column is declared
     whether or not anything is in it, so the destinations stay centred on every
     page rather than sliding across as the page changes. */
  .header-inner {
    /* Positioned, so the pills paint above the scrim rather than under it. */
    position: relative;
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto;
    align-items: center;
    gap: var(--space-4);
    max-width: var(--layout-wide);
    margin-inline: auto;
    min-height: 4.5rem;
    padding-block: var(--space-3);
    padding-inline: var(--layout-gutter-start) var(--layout-gutter-end);
  }

  .library-controls {
    grid-column: 2;
    display: flex;
    align-items: center;
    justify-content: flex-end;
    flex-wrap: wrap;
    gap: var(--space-2);
    min-width: 0;
    pointer-events: auto;
  }

  /* Connection feedback gets its own small badge without moving the controls. */
  .status {
    grid-column: 1 / -1;
    display: flex;
    justify-content: flex-end;
    min-width: 0;
  }

  .offline {
    pointer-events: auto;
    margin: 0;
    padding: var(--space-1) var(--space-3);
    border-radius: var(--radius-full);
    background: var(--warning-subtle);
    color: var(--text);
    font-size: var(--text-xs);
    text-align: end;
  }

  /*
   * Stays, and stays on every page.
   *
   * It is the way home and it is the only thing on screen that says which app
   * this is — which matters more here than in most places, because Culina is
   * self-hosted and lives at whatever address somebody gave it. The slot could
   * carry the page's own title once the title has scrolled away instead, and
   * that is worth building one day; it is not worth replacing the only fixed
   * point in the app with.
   *
   * What it did not have was the behaviour of the link it is: no hover, no
   * pressed state, nothing to tell a pointer that this is a control rather than
   * a logo printed in the corner. It borrows the navigation's own, because it
   * sits in the same row of pills and does the same kind of thing.
   */
  .brand {
    grid-column: 1;
    justify-self: start;
    text-decoration: none;
    padding: var(--space-2) var(--space-3);
    margin-inline-start: calc(-1 * var(--space-3));
    border-radius: var(--radius-full);
    background: var(--surface-nav-glass);
    backdrop-filter: blur(16px);
    pointer-events: auto;
    transition: background-color var(--duration-fast) var(--ease-out);
  }

  .brand:hover {
    background: var(--surface-selected);
  }

  /* The same pill as the brand, because it sits in the same row and is the
     same kind of thing: a way to somewhere, always there. */
  .search {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
    min-width: var(--control-sm);
    min-height: var(--control-sm);
    padding: var(--space-2);
    border: 0;
    border-radius: var(--radius-full);
    background: var(--surface-nav-glass);
    backdrop-filter: blur(16px);
    color: var(--text);
    cursor: pointer;
    transition: background-color var(--duration-fast) var(--ease-out);
  }

  .search:hover {
    background: var(--surface-selected);
  }

  .search svg {
    width: var(--space-4);
    height: var(--space-4);
  }

  .brand:active {
    background: var(--surface-hover);
  }

  .content {
    grid-area: content;
    min-width: 0;
    /* Focusable as a skip-link target, but never with a ring of its own. */
    outline: none;
    scroll-margin-top: var(--space-24);
  }

  .dock {
    min-width: 0;
    grid-area: dock;
    position: sticky;
    bottom: var(--bar-inset);
    z-index: var(--z-sticky);
  }

  .bar {
    grid-area: bar;
    position: sticky;
    bottom: 0;
    z-index: var(--z-sticky);
    border-top: 1px solid var(--border);
    background: var(--surface-raised);
    /* Clear of the home indicator. */
    padding-bottom: env(safe-area-inset-bottom, 0);
    padding-inline: env(safe-area-inset-left, 0px) env(safe-area-inset-right, 0px);
  }

  /* A thin line across the top while a route resolves. Not a spinner: this is
     usually over before it is noticed, and a spinner that flashes is worse than
     nothing. */
  .progress {
    position: absolute;
    inset-block-start: 0;
    inset-inline: 0;
    height: 2px;
    background: var(--accent);
    transform-origin: left center;
    animation: advance 1.2s var(--ease-out) forwards;
  }

  @keyframes advance {
    from {
      transform: scaleX(0.05);
    }
    to {
      transform: scaleX(0.9);
    }
  }

  .skip {
    position: absolute;
    inset-block-start: var(--space-2);
    inset-inline-start: var(--space-2);
    z-index: var(--z-overlay);
    padding: var(--space-2) var(--space-4);
    border-radius: var(--radius-md);
    background: var(--surface-overlay);
    color: var(--text);
    box-shadow: var(--shadow-overlay);
    transform: translateY(-200%);
  }

  .skip:focus {
    transform: none;
  }

  /* The narrow header keeps the brand and recipe creation. Expanded navigation
     takes the middle slot between them on desktop. */
  .wide-only {
    pointer-events: auto;
    grid-column: 2;
    display: none;
  }

  @media (width < 40rem) {
    .header-inner {
      gap: var(--space-2);
    }
    .library-controls {
      flex-wrap: nowrap;
    }
  }

  /* Expanded navigation needs room for the brand, labels and recipe creation. */
  @media (min-width: 64rem) {
    .header-inner {
      grid-template-columns: minmax(0, 1fr) auto minmax(0, 1fr);
    }

    .library-controls {
      grid-column: 3;
    }

    .wide-only {
      display: block;
    }

    .narrow-only {
      display: none;
    }
  }

  /* In landscape or with a keyboard open, give the content its height back. */
  @media screen and (max-height: 32rem) {
    .header {
      position: relative;
      top: auto;
    }

    /* Not sticky here, so nothing ever passes under it. */
    .header::before {
      display: none;
    }
  }

  /* And the same answer when it is text rather than the window that has taken
     the room: the header scrolls away with the page instead of floating over
     what is left of it. The bars at the bottom stay — they are how somebody
     gets anywhere — and giving back the header's share is enough to read and
     type in what remains. */
  .shell.crowded .header {
    position: relative;
    top: auto;
  }

  .shell.crowded .header::before {
    display: none;
  }

  @media (prefers-reduced-motion: reduce) {
    .progress {
      animation: none;
      transform: scaleX(0.5);
    }
  }

  /*
   * On paper there is no app: no bar to skip to, no navigation to use, no
   * connection to have lost. Only what is in the middle of the screen.
   */
  @media print {
    .skip,
    .header,
    .dock,
    .bar {
      display: none !important;
    }

    .shell,
    .content {
      display: block;
      min-height: 0;
      margin: 0;
      padding: 0;
    }
  }
</style>
