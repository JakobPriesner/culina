<script lang="ts">
  import { page } from '$app/state';

  import { afterNavigate } from '$app/navigation';
  import { Sheet } from '$ds';
  import LibraryNav from './LibraryNav.svelte';

  import { destinations } from './navigation';
  import { m } from './i18n';
  import NavIcon from './NavIcon.svelte';

  /**
   * One navigation, two placements.
   *
   * `top` is the navbar across the head of the page, where a pointer already
   * is. `bottom` is the bar along the foot of a phone, where a thumb already
   * is. The compact bar opens the collection sidebar in a sheet, keeping
   * the same three global areas as the collection grows.
   */
  interface Props {
    placement: 'top' | 'bottom';
  }

  let { placement }: Props = $props();

  const current = $derived(page.url.pathname);
  let libraryOpen = $state(false);
  afterNavigate(() => {
    libraryOpen = false;
  });

  $effect(() => {
    if (placement !== 'bottom') return;
    const desktop = window.matchMedia('(min-width: 64rem)');
    const dismissOnDesktop = () => {
      if (desktop.matches) libraryOpen = false;
    };
    desktop.addEventListener('change', dismissOnDesktop);
    return () => desktop.removeEventListener('change', dismissOnDesktop);
  });
</script>

<nav class="nav {placement}" aria-label={m['nav.label']()}>
  {#each destinations as destination (destination.href)}
    {@const active = destination.match(current)}
    {#if placement === 'bottom' && destination.icon === 'recipes'}
      <button
        type="button"
        class="destination"
        class:active
        aria-current={active ? 'true' : undefined}
        aria-haspopup="dialog"
        aria-expanded={libraryOpen}
        onclick={() => {
          libraryOpen = true;
        }}
      >
        <span class="icon"><NavIcon icon="cookbooks" current={active} /></span>
        <span class="label">{m['nav.library']()} <span aria-hidden="true">⌃</span></span>
      </button>
    {:else}
      <a
        class="destination"
        class:active
        href={destination.href}
        aria-current={active ? 'page' : undefined}
      >
        <span class="icon"><NavIcon icon={destination.icon} current={active} /></span>
        <span class="label">{destination.label()}</span>
      </a>
    {/if}
  {/each}
</nav>

{#if placement === 'bottom'}
  <Sheet bind:open={libraryOpen} title={m['library.label']()} closeLabel={m['picker.close']()}>
    <LibraryNav
      embedded
      onnavigate={() => {
        libraryOpen = false;
      }}
    />
  </Sheet>
{/if}

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
    width: calc(var(--space-6) + 2 * var(--space-4));
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
</style>
