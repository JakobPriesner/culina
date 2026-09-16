<script lang="ts">
  import { page } from '$app/state';
  import { m } from './i18n';
  import { libraryDestinations } from './navigation';
  import NavIcon from './NavIcon.svelte';

  let { embedded = false, onnavigate }: { embedded?: boolean; onnavigate?: () => void } = $props();
</script>

<nav class="library" class:embedded aria-label={m['library.label']()}>
  {#if !embedded}<p class="legend">{m['library.label']()}</p>{/if}
  <div class="destinations">
    {#each libraryDestinations as destination (destination.href)}
      {@const active = destination.match(page.url.pathname)}
      <a
        class="section"
        class:active
        class:separate={destination.separate}
        href={destination.href}
        aria-current={active ? 'page' : undefined}
        onclick={onnavigate}
      >
        <span class="icon" aria-hidden="true"
          ><NavIcon icon={destination.icon} current={active} /></span
        >
        {destination.label()}
      </a>
    {/each}
  </div>
</nav>

<style>
  .library {
    padding: var(--space-1);
  }
  .library.embedded {
    padding: var(--space-2);
  }
  .section.separate {
    margin-top: var(--space-4);
  }
  .section.separate::after {
    content: '';
    position: absolute;
    inset-inline: var(--space-2);
    top: calc(-1 * var(--space-2));
    border-top: 1px solid var(--border);
  }
  .embedded .section {
    min-height: var(--control-lg);
    padding: var(--space-3);
    font-size: var(--text-base);
  }
  .embedded .icon {
    width: var(--space-6);
    height: var(--space-6);
  }
  .legend {
    padding-inline: var(--space-2);
    margin-bottom: var(--space-3);
    color: var(--text-muted);
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
  }
  .destinations {
    display: grid;
    gap: var(--space-1);
  }
  .section {
    position: relative;
    display: flex;
    align-items: center;
    gap: var(--space-2);
    min-height: var(--control-sm);
    padding: var(--space-2);
    border-radius: var(--radius-md);
    color: var(--text-muted);
    font-size: var(--text-sm);
    text-decoration: none;
    transition:
      color var(--duration-fast) var(--ease-out),
      background-color var(--duration-fast) var(--ease-out);
  }
  .section:hover {
    background: var(--surface-hover);
    color: var(--text);
  }
  .section.active {
    background: var(--surface-accent-subtle);
    color: var(--accent);
    font-weight: var(--weight-semibold);
  }
  .section.active::before {
    content: '';
    position: absolute;
    inset-inline-start: 0;
    width: 2px;
    height: var(--space-4);
    border-radius: var(--radius-full);
    background: var(--accent);
  }
  .icon {
    width: var(--space-4);
    height: var(--space-4);
    flex-shrink: 0;
  }
</style>
