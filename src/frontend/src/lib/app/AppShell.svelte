<script lang="ts">
  import type { Snippet } from 'svelte';

  import { page } from '$app/state';
  import { Toaster } from '$ds';

  import { session } from '$features/auth/session.svelte';
  import KitchenRuntime from '$features/cooking/KitchenRuntime.svelte';

  import { m } from './i18n';
  import { offersSearch } from './navigation';
  import AppHeader from './shell/AppHeader.svelte';
  import BottomBar from './shell/BottomBar.svelte';
  import SearchHost from './shell/SearchHost.svelte';
  import ShellDock from './shell/ShellDock.svelte';
  import SkipLink from './shell/SkipLink.svelte';

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
  let headerHeight = $state<number>();

  const focusedCooking = $derived(page.route.id === '/(app)/recipes/[recipeId]/cook');

  const searchable = $derived(
    offersSearch(page.url.pathname) && session.activeHouseholdId !== null
  );

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
    viewportHeight > 0 && (headerHeight ?? 0) + dockHeight + barHeight > viewportHeight / 2
  );
</script>

<KitchenRuntime />

<svelte:window bind:innerHeight={viewportHeight} />

<div
  class="shell"
  class:crowded
  class:focused-cooking={focusedCooking}
  style:--bar-inset="{barHeight}px"
  style:--bottom-inset="{dockHeight + barHeight}px"
  style:--header-inset={headerHeight === undefined ? null : `${headerHeight}px`}
>
  <SkipLink />

  <AppHeader {searchable} bind:height={headerHeight} />

  <main class="content" id="content" tabindex="-1">
    {@render children()}
  </main>

  <ShellDock bind:height={dockHeight}>{@render dock?.()}</ShellDock>

  <BottomBar bind:height={barHeight} />

  <Toaster label={m['app.notifications']()} dismissLabel={m['app.dismiss']()} />

  <SearchHost {searchable} />
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

  .content {
    grid-area: content;
    min-width: 0;
    view-transition-name: page-content;
    /* Focusable as a skip-link target, but never with a ring of its own. */
    outline: none;
    scroll-margin-top: var(--space-24);
  }

  /* Guided cooking has its own way back to the recipe, and on compact screens
     the instructions need the space the header and bar would take. */
  @media (width < 64rem) {
    .focused-cooking .content {
      padding-top: env(safe-area-inset-top, 0px);
    }
  }

  /* On paper there is no app: only what is in the middle of the screen. */
  @media print {
    .shell,
    .content {
      display: block;
      min-height: 0;
      margin: 0;
      padding: 0;
    }
  }
</style>
