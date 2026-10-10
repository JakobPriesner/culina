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

  /** The frame every signed-in page sits in; the dock reserves space for the cooking bar. */
  interface Props {
    children: Snippet;
    dock?: Snippet;
  }

  let { children, dock }: Props = $props();

  /**
   * Viewport bottom already taken by the dock and bottom bar, which cover page content stuck to the viewport bottom.
   * Measured: the bottom bar is display:none on wide screens and reports zero.
   */
  let dockHeight = $state(0);
  let barHeight = $state(0);

  /**
   * Height of the floating header, for pages that put content on its line; measured because it follows the wordmark's font metrics.
   * Unset until measured so the token's figure holds the place in SSR rather than a zero.
   */
  let headerHeight = $state<number>();

  const focusedCooking = $derived(page.route.id === '/(app)/recipes/[recipeId]/cook');

  const readingRecipe = $derived(page.route.id === '/(app)/recipes/[recipeId]');

  const searchable = $derived(
    offersSearch(page.url.pathname) && session.activeHouseholdId !== null
  );

  let viewportHeight = $state(0);

  /**
   * Header, dock and bar would take half the screen: all grow with enlarged text and no media query can see it
   * (320x568 at 200%: 521px of chrome around 47px of recipe).
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
  class:reading-recipe={readingRecipe}
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
    /* Skip-link target: focusable but no ring. */
    outline: none;
    scroll-margin-top: var(--space-24);
  }

  /* Guided cooking has its own way back, and needs the space on compact screens. */
  @media (width < 64rem) {
    .focused-cooking .content {
      padding-top: env(safe-area-inset-top, 0px);
    }
  }

  /* A recipe on a phone has no header above it, and its sticky back bar brings its own top padding. */
  @media (width < 52rem) {
    .reading-recipe .content :global(.page) {
      padding-block-start: 0;
    }
  }

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
