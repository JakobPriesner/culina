<script lang="ts">
  import { selectionIndicator } from '$ds/containment/selectionIndicator';
  import { page } from '$app/state';
  import { session } from '$features/auth/session.svelte';
  import { barDestinations } from './navigation';
  import { m } from './i18n';
  import NavIcon from './NavIcon.svelte';
  import { woche } from './woche.svelte';

  /**
   * The same direct destinations: a top bar on desktop and a bottom bar on phones. `compact` recedes the bottom bar to
   * its icons while a page is read downwards, as an iOS tab bar minimises; every destination stays one tap away.
   */
  let { placement, compact = false }: { placement: 'top' | 'bottom'; compact?: boolean } = $props();
  const current = $derived(page.url.pathname);
  const destinations = $derived(barDestinations(woche.view));
  const selected = $derived(destinations.find((destination) => destination.match(current))?.href);
</script>

<!-- Every destination is a household's: without one (just signed up, on /welcome) they would only lead back there. -->
{#if session.activeHouseholdId}
  <nav
    use:selectionIndicator={{ selected }}
    class="nav {placement}"
    class:compact
    aria-label={m['nav.label']()}
  >
    {#each destinations as destination (destination.icon)}
      {@const active = destination.match(current)}
      <a
        class="destination"
        data-selection={destination.href}
        class:active
        href={destination.href}
        aria-current={active ? 'page' : undefined}
      >
        <span class="icon" aria-hidden="true"
          ><NavIcon icon={destination.icon} current={active} /></span
        >
        <span class="label">{destination.label()}</span>
      </a>
    {/each}
  </nav>
{/if}

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
    backdrop-filter: var(--glass-filter);
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

  /*
   * A floating glass dock keeps the four destinations in thumb reach, concentric with the screen's corner it sits near
   * (in practice a capsule), with one moving lens for the current page. The lens is neutral and the current
   * destination's icon and label take the accent, as an iOS tab bar marks it: colour in navigation stays sparing.
   */
  .bottom {
    --dock-padding: 0.375rem;
    --dock-radius: calc(var(--radius-screen) - var(--screen-inset));

    display: grid;
    grid-template-columns: repeat(4, minmax(0, 1fr));
    gap: var(--space-1);
    max-width: 32rem;
    margin-inline: auto;
    padding: var(--dock-padding);
    border-radius: var(--dock-radius);
    background: var(--surface-nav-glass);
    backdrop-filter: var(--glass-filter);
    box-shadow: var(--shadow-glass);
    pointer-events: auto;
  }

  .bottom .destination {
    min-width: 0;
    min-height: 3.5rem;
    padding: var(--space-1);
    border-radius: calc(var(--dock-radius) - var(--dock-padding));
    text-align: center;
    flex-direction: column;
    justify-content: center;
    gap: var(--space-1);
    transition:
      color var(--duration-fast) var(--ease-out),
      background-color var(--duration-fast) var(--ease-out),
      min-height var(--duration-base) var(--ease-out);
  }

  .bottom .icon {
    width: var(--space-6);
    height: var(--space-6);
  }

  /* The accent reads 4.5:1 or more on the raised lens in every theme and mode (themes.spec.ts). */
  .bottom .destination.active {
    color: var(--accent);
  }

  .bottom:not([data-indicator-ready]) .destination.active,
  .bottom :global(.selection-indicator) {
    border-radius: calc(var(--dock-radius) - var(--dock-padding));
    background: var(--surface-raised);
    box-shadow: var(--shadow-card);
  }

  .bottom .destination:not(.active):hover {
    background: var(--surface-hover);
  }

  .bottom .destination:active {
    background: var(--surface-selected);
  }

  /* Compact: icons only, at the 44px floor; the labels stay as the links' names. */
  .bottom.compact .destination {
    min-height: var(--control-sm);
    gap: 0;
  }

  .bottom.compact .label {
    position: absolute;
    width: 1px;
    height: 1px;
    overflow: hidden;
    clip-path: inset(50%);
    white-space: nowrap;
  }

  .bottom .label {
    font-size: var(--text-xs);
    font-weight: var(--weight-medium);
    line-height: 1.25;
    /* Whole words only ("Kochbüche/r" read as broken); the narrow-screen sizes below make every label fit. */
    overflow-wrap: normal;
  }

  .bottom .destination.active .label {
    font-weight: var(--weight-semibold);
  }

  /*
   * The floating dock leaves a 320px phone 60px for a label, and "Kochbücher" needs about 70 at the usual size. A tighter
   * dock inside (its 12px margin to the screen edge stays) and the size of a system tab bar's labels (11px) keep every
   * label whole; mid-word "Kochbüc/her" read as broken.
   */
  @media (width < 25rem) {
    .bottom {
      --dock-padding: 2px;

      gap: 2px;
    }

    .bottom .destination {
      padding-inline: 2px;
    }

    .bottom .label {
      font-size: 0.6875rem;
    }
  }
</style>
