<script lang="ts">
  import { selectionIndicator } from '$ds/containment/selectionIndicator';
  import { page } from '$app/state';
  import { session } from '$features/auth/session.svelte';
  import { barDestinations } from './navigation';
  import { m } from './i18n';
  import NavIcon from './NavIcon.svelte';
  import { woche } from './woche.svelte';

  /** The same direct destinations: a top bar on desktop and a bottom bar on phones. */
  let { placement }: { placement: 'top' | 'bottom' } = $props();
  const current = $derived(page.url.pathname);
  const destinations = $derived(barDestinations(woche.view));
  const selected = $derived(destinations.find((destination) => destination.match(current))?.href);
</script>

<!-- Every destination is a household's: without one (just signed up, on /welcome) they would only lead back there. -->
{#if session.activeHouseholdId}
  <nav use:selectionIndicator={{ selected }} class="nav {placement}" aria-label={m['nav.label']()}>
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

  /* An inset dock keeps the four destinations in thumb reach, with one moving tile for the current page. */
  .bottom {
    display: grid;
    grid-template-columns: repeat(4, minmax(0, 1fr));
    gap: var(--space-1);
    max-width: 32rem;
    margin-inline: auto;
    padding: 0.375rem;
    border: 1px solid var(--border);
    border-radius: 1.75rem;
    background: var(--surface-raised);
    box-shadow: var(--shadow-dock);
    pointer-events: auto;
  }

  .bottom .destination {
    min-width: 0;
    min-height: 3rem;
    padding: var(--space-1);
    border-radius: 1.25rem;
    text-align: center;
    flex-direction: column;
    justify-content: center;
    gap: var(--space-1);
  }

  .bottom .icon {
    width: var(--space-6);
    height: var(--space-6);
  }

  .bottom .destination.active {
    color: var(--accent);
  }

  .bottom:not([data-indicator-ready]) .destination.active,
  .bottom :global(.selection-indicator) {
    border-radius: 1.25rem;
    background: var(--surface-accent-subtle);
    box-shadow: none;
  }

  .bottom .destination:not(.active):hover {
    background: var(--surface-hover);
  }

  .bottom .destination:active {
    background: var(--surface-selected);
  }

  /* The dock shows icons only; the label stays the link's name for screen readers. */
  .bottom .label {
    position: absolute;
    width: 1px;
    height: 1px;
    padding: 0;
    overflow: hidden;
    clip-path: inset(50%);
    white-space: nowrap;
  }
</style>
