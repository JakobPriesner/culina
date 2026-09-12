<script lang="ts">
  import { page } from '$app/state';

  import { destinations } from './navigation';
  import { m } from './i18n';
  import NavIcon from './NavIcon.svelte';

  /**
   * One navigation, two shapes.
   *
   * A bar along the bottom on a phone, where a thumb reaches; a rail down the
   * left on a wide screen, where the bottom edge is nowhere near the hand.
   * Mobile by prioritisation, not by compression: the rail adds room for the
   * labels to sit beside the icons, it does not add items.
   */
  const current = $derived(page.url.pathname);
</script>

<nav class="nav" aria-label={m['nav.label']()}>
  {#each destinations as destination (destination.href)}
    {@const active = destination.match(current)}
    <a
      class="destination"
      class:active
      href={destination.href}
      aria-current={active ? 'page' : undefined}
    >
      <span class="icon"><NavIcon icon={destination.icon} current={active} /></span>
      <span class="label">{destination.label()}</span>
    </a>
  {/each}
</nav>

<style>
  .nav {
    display: flex;
    background: var(--surface-raised);
  }

  .destination {
    display: flex;
    flex: 1;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: var(--space-1);
    min-height: var(--control-md);
    padding: var(--space-2);
    color: var(--text-muted);
    text-decoration: none;
    transition: color var(--duration-fast) var(--ease-out);
  }

  .destination:hover {
    color: var(--text);
  }

  .destination.active {
    color: var(--accent);
  }

  .icon {
    display: block;
    width: var(--space-6);
    height: var(--space-6);
  }

  .label {
    font-size: var(--text-xs);
    font-weight: var(--weight-medium);
  }

  /* Bottom bar: sits above the home indicator, and a hairline rather than a
     shadow, because a shadow along the bottom of a phone reads as grime. */
  @media (max-width: 47.999rem) {
    .nav {
      border-top: 1px solid var(--border);
      padding-bottom: env(safe-area-inset-bottom, 0);
    }
  }

  /* Rail: the same three items with room to breathe. */
  @media (min-width: 48rem) {
    .nav {
      flex-direction: column;
      gap: var(--space-1);
      width: 100%;
      padding: var(--space-4) var(--space-2);
      background: transparent;
    }

    .destination {
      flex: none;
      flex-direction: row;
      justify-content: flex-start;
      gap: var(--space-3);
      padding: var(--space-3);
      border-radius: var(--radius-md);
    }

    .destination:hover {
      background: var(--surface-hover);
    }

    .destination.active {
      background: var(--surface-selected);
      color: var(--text);
    }

    .label {
      font-size: var(--text-base);
    }
  }
</style>
