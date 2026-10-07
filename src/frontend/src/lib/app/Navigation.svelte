<script lang="ts">
  import { selectionIndicator } from '$ds/containment/selectionIndicator';
  import { page } from '$app/state';
  import { destinations } from './navigation';
  import { m } from './i18n';
  import NavIcon from './NavIcon.svelte';

  /** The same direct destinations: a top bar on desktop and a bottom bar on phones. */
  let { placement }: { placement: 'top' | 'bottom' } = $props();
  const current = $derived(page.url.pathname);
  const selected = $derived(destinations.find((destination) => destination.match(current))?.href);
</script>

<nav use:selectionIndicator={{ selected }} class="nav {placement}" aria-label={m['nav.label']()}>
  {#each destinations as destination (destination.href)}
    {@const active = destination.match(current)}
    <a
      class="destination"
      data-selection={placement === 'top' ? destination.href : undefined}
      class:active
      href={destination.href}
      aria-current={active ? 'page' : undefined}
    >
      <span
        class="icon"
        data-selection={placement === 'bottom' ? destination.href : undefined}
        aria-hidden="true"><NavIcon icon={destination.icon} current={active} /></span
      >
      <span class="label">{destination.label()}</span>
    </a>
  {/each}
</nav>

<style>
  .nav {
    position: relative;
    isolation: isolate;
    display: flex;
  }

  .destination {
    position: relative;
    z-index: 1;
    display: flex;
    align-items: center;
    border: none;
    background: transparent;
    font: inherit;
    cursor: pointer;
    color: var(--text-muted);
    text-decoration: none;
    transition:
      color var(--duration-fast) var(--ease-out),
      background-color var(--duration-fast) var(--ease-out),
      box-shadow var(--duration-fast) var(--ease-out);
  }

  .destination:hover {
    color: var(--accent-hover);
  }

  .icon {
    display: block;
    flex-shrink: 0;
    width: var(--space-6);
    height: var(--space-6);
  }

  .top {
    padding: var(--space-1);
    border-radius: var(--radius-full);
    background: var(--surface-nav-glass);
    backdrop-filter: blur(20px) saturate(180%);
    -webkit-backdrop-filter: blur(20px) saturate(180%);
    box-shadow: var(--shadow-glass);
    gap: var(--space-1);
  }

  .top .destination {
    gap: var(--space-2);
    min-height: var(--control-sm);
    padding: var(--space-2) var(--space-3);
    border-radius: var(--radius-full);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
  }

  .top:not([data-indicator-ready]) .destination.active {
    background: var(--surface-raised);
    color: var(--text);
    box-shadow: var(--shadow-card);
  }

  .top .destination.active {
    color: var(--text);
  }

  .top .destination:not(.active):hover {
    background: var(--surface-selected);
    box-shadow: var(--shadow-card);
  }

  .bottom {
    padding-block: var(--space-1);
  }

  .bottom .destination {
    flex: 1 1 auto;
    min-width: 0;
    text-align: center;
    flex-direction: column;
    justify-content: center;
    gap: var(--space-1);
    min-height: var(--control-lg);
    padding: var(--space-2) var(--space-1);
  }

  /* A raised pill behind the icon marks the current destination without relying on colour. */
  .bottom .icon {
    box-sizing: border-box;
    width: calc(var(--space-6) + 2 * var(--space-3));
    max-width: 100%;
    height: calc(var(--space-6) + 2 * var(--space-1));
    padding: var(--space-1);
    border-radius: var(--radius-full);
    transition:
      background-color var(--duration-fast) var(--ease-out),
      box-shadow var(--duration-fast) var(--ease-out);
  }

  .bottom .destination.active {
    color: var(--accent);
  }

  .bottom:not([data-indicator-ready]) .destination.active .icon {
    background: var(--surface-raised);
    box-shadow: var(--shadow-card);
  }

  .bottom .destination:not(.active):hover .icon {
    background: var(--surface-selected);
    box-shadow: var(--shadow-card);
  }

  .bottom .label {
    font-size: var(--text-xs);
    font-weight: var(--weight-medium);
  }

  /*
   * Narrowest phones: German labels need about 325 of 320px ("Einstellungen" alone is 85px), and a wrapped label moves the bar and the page.
   * The 4px per side is dropped; the icon pill gives the inset anyway.
   */
  @media (width < 24rem) {
    .bottom .destination {
      padding-inline: 0;
    }
  }
</style>
