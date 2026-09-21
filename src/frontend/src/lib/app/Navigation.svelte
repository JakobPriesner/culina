<script lang="ts">
  import { page } from '$app/state';
  import { destinations } from './navigation';
  import { m } from './i18n';
  import NavIcon from './NavIcon.svelte';

  /** The same direct destinations: a top bar on desktop and a bottom bar on phones. */
  let { placement }: { placement: 'top' | 'bottom' } = $props();
  const current = $derived(page.url.pathname);
</script>

<nav class="nav {placement}" aria-label={m['nav.label']()}>
  {#each destinations as destination (destination.href)}
    {@const active = destination.match(current)}
    <a
      class="destination"
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

<style>
  .nav {
    display: flex;
  }

  .destination {
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
      background-color var(--duration-fast) var(--ease-out);
  }

  .destination:hover {
    color: var(--text);
  }

  .icon {
    display: block;
    flex-shrink: 0;
    width: var(--space-6);
    height: var(--space-6);
  }

  /* The navbar: icon and label side by side, with room to breathe. */
  .top {
    padding: var(--space-1);
    border: 1px solid var(--border);
    border-radius: var(--radius-full);
    background: var(--surface-nav-glass);
    backdrop-filter: blur(16px);
    box-shadow: var(--shadow-card);
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

  .top .destination:hover {
    background: var(--surface-hover);
  }

  .top .destination.active {
    background: var(--surface-raised);
    color: var(--text);
    box-shadow: var(--shadow-card);
  }

  /* Share spare width while allowing translated labels their natural width. */
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

  /* A raised pill behind the icon marks the current destination without
     relying on colour alone or enclosing the full stacked item. */
  .bottom .icon {
    box-sizing: border-box;
    width: calc(var(--space-6) + 2 * var(--space-3));
    max-width: 100%;
    height: calc(var(--space-6) + 2 * var(--space-1));
    padding: var(--space-1);
    border-radius: var(--radius-full);
    transition: background-color var(--duration-fast) var(--ease-out);
  }

  .bottom .destination.active {
    color: var(--accent);
  }

  .bottom .destination.active .icon {
    background: var(--surface-raised);
    box-shadow: var(--shadow-card);
  }

  .bottom .label {
    font-size: var(--text-xs);
    font-weight: var(--weight-medium);
  }

  /*
    Five labels sharing the narrowest screen anyone uses.

    German is the constraint: "Einstellungen" wants 75px where an equal fifth of
    320px gives about 64px, and "Kochbücher" wants 67px. Measured at 320px, all
    three long labels were landing a fraction of a pixel short of their own text
    — 46.1 against 46, 66.8 against 67, 74.8 against 75 — and wrapping to two
    lines for want of almost nothing.

    So they are given almost nothing: half the side padding, below the width
    where it is needed. That is 20px back across the row, which leaves about a
    tenth of the space spare rather than a rounding error, so the next
    translation that is a character longer than today's does not put it back.
    The type stays at --text-xs; a nav label is small enough already, and this
    row is read at arm's length with wet hands.
  */
  @media (width < 23rem) {
    .bottom .destination {
      padding-inline: calc(var(--space-1) / 2);
    }

    /*
      And a step down in size, because the padding alone was winning by about
      7% — which a scrollbar taking 15px of the 320, or any translation a
      character longer, takes straight back. Together they leave roughly a
      third of the row spare, which is a margin rather than a coincidence.

      One step, and only here: 11px is still a legible label, and this is the
      narrowest phone anyone opens the app on rather than the common case.
    */
    .bottom .label {
      font-size: 0.6875rem;
    }
  }
</style>
