<script lang="ts">
  import { page } from '$app/state';

  import { destinations } from './navigation';
  import { m } from './i18n';
  import NavIcon from './NavIcon.svelte';

  /**
   * One navigation, two placements.
   *
   * `top` is the navbar across the head of the page, where a pointer already
   * is. `bottom` is the bar along the foot of a phone, where a thumb already
   * is. Same three items, same order, same code — mobile by prioritisation,
   * not by compression.
   */
  interface Props {
    placement: 'top' | 'bottom';
  }

  let { placement }: Props = $props();

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
      <span class="icon"><NavIcon icon={destination.icon} current={active} /></span>
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
    width: var(--space-6);
    height: var(--space-6);
  }

  /* The navbar: icon and label side by side, with room to breathe. */
  .top {
    gap: var(--space-1);
  }

  .top .destination {
    gap: var(--space-2);
    min-height: var(--control-sm);
    padding: var(--space-2) var(--space-3);
    border-radius: var(--radius-md);
    font-weight: var(--weight-medium);
  }

  .top .destination:hover {
    background: var(--surface-hover);
  }

  .top .destination.active {
    background: var(--surface-selected);
    color: var(--text);
  }

  /* The bar: stacked, and each item is a third of the width, because the
     target has to be found without looking. */
  .bottom .destination {
    flex: 1;
    flex-direction: column;
    justify-content: center;
    gap: var(--space-1);
    min-height: var(--control-md);
    padding: var(--space-2);
  }

  .bottom .destination.active {
    color: var(--accent);
  }

  .bottom .label {
    font-size: var(--text-xs);
    font-weight: var(--weight-medium);
  }
</style>
