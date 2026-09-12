<script lang="ts">
  import type { Snippet } from 'svelte';

  import { navigating } from '$app/state';
  import { Toaster } from '$ds';

  import { m } from './i18n';
  import Navigation from './Navigation.svelte';

  /**
   * The frame every signed-in page sits in.
   *
   * The navigation is rendered once and never re-created, so moving between
   * sections does not rebuild it — and the slot above it is reserved whether or
   * not anything is in it, so the cooking bar appearing later never pushes the
   * page.
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

  {#if navigating.to}
    <span class="progress" role="progressbar" aria-label={m['app.navigating']()}></span>
  {/if}

  <div class="rail"><Navigation /></div>

  <main class="content" id="content" tabindex="-1">{@render children()}</main>

  <div class="dock">{@render dock?.()}</div>

  <div class="bar"><Navigation /></div>

  <Toaster label={m['app.notifications']()} dismissLabel={m['app.dismiss']()} />
</div>

<style>
  .shell {
    display: grid;
    min-height: 100dvh;
    grid-template-columns: 1fr;
    grid-template-rows: 1fr auto auto;
    grid-template-areas:
      'content'
      'dock'
      'bar';
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
  }

  .rail {
    display: none;
    grid-area: rail;
  }

  /* A thin line across the top while a route resolves. Not a spinner: this is
     usually over before it is noticed, and a spinner that flashes is worse than
     nothing. */
  .progress {
    position: fixed;
    inset-block-start: 0;
    inset-inline: 0;
    height: 2px;
    z-index: var(--z-overlay);
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

  @media (min-width: 48rem) {
    .shell {
      grid-template-columns: 14rem 1fr;
      grid-template-rows: 1fr auto;
      grid-template-areas:
        'rail content'
        'rail dock';
    }

    .rail {
      display: block;
      position: sticky;
      top: 0;
      align-self: start;
      height: 100dvh;
      border-right: 1px solid var(--border);
    }

    .bar {
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
