<script lang="ts">
  import type { Snippet } from 'svelte';

  import { navigating } from '$app/state';
  import { resolve } from '$app/paths';
  import { Toaster } from '$ds';

  import NowCookingBar from '$features/cooking/NowCookingBar.svelte';

  import Brand from './Brand.svelte';
  import { m } from './i18n';
  import Navigation from './Navigation.svelte';

  /**
   * The frame every signed-in page sits in.
   *
   * A navbar across the top, and on a phone the same three destinations along
   * the bottom as well, where a thumb reaches. The navigation is rendered once
   * and never re-created, so moving between sections does not rebuild it — and
   * the slot above the bottom bar is reserved whether or not anything is in it,
   * so the cooking bar appearing later never pushes the page.
   */
  interface Props {
    children: Snippet;
    /** Reserved for the bar that appears while a recipe is being cooked. */
    dock?: Snippet;
  }

  let { children, dock }: Props = $props();
</script>

<div class="shell">
  <!-- First in the tab order and invisible until focused: without it, reaching
       the page content by keyboard means tabbing through the navigation on
       every single page. -->
  <a class="skip" href="#content">{m['nav.skip']()}</a>

  <header class="header">
    {#if navigating.to}
      <span class="progress" role="progressbar" aria-label={m['app.navigating']()}></span>
    {/if}

    <div class="header-inner">
      <a class="brand" href={resolve('/(app)')} aria-label={m['app.name']()}><Brand /></a>

      <div class="wide-only"><Navigation placement="top" /></div>
    </div>
  </header>

  <main class="content" id="content" tabindex="-1">{@render children()}</main>

  <!-- The slot is reserved whether or not anything is in it, so the bar
       appearing never pushes the page. -->
  <div class="dock">
    <NowCookingBar />
    {@render dock?.()}
  </div>

  <div class="bar narrow-only"><Navigation placement="bottom" /></div>

  <Toaster label={m['app.notifications']()} dismissLabel={m['app.dismiss']()} />
</div>

<style>
  .shell {
    display: grid;
    min-height: 100dvh;
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
    border-bottom: 1px solid var(--border);
    background: var(--surface-raised);
  }

  /* The same rhythm as the signed-out header, so the app does not change shape
     the moment someone signs in. */
  .header-inner {
    display: flex;
    align-items: center;
    gap: var(--space-8);
    max-width: var(--layout-wide);
    margin-inline: auto;
    padding: var(--space-3) var(--layout-gutter);
  }

  .brand {
    text-decoration: none;
  }

  .content {
    grid-area: content;
    min-width: 0;
    /* Focusable as a skip-link target, but never with a ring of its own. */
    outline: none;
  }

  .dock {
    grid-area: dock;
    position: sticky;
    bottom: 0;
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

  .wide-only {
    display: none;
  }

  @media (min-width: 48rem) {
    .wide-only {
      display: block;
    }

    .narrow-only {
      display: none;
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .progress {
      animation: none;
      transform: scaleX(0.5);
    }
  }
</style>
