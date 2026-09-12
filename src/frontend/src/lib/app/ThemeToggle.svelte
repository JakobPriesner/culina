<script lang="ts">
  import { m } from './i18n';
  import { nextMode, type Mode } from './appearance';
  import { preferences } from './preferences.svelte';

  /**
   * Cycles light → dark → follow the device.
   *
   * The accessible name states the current value and what pressing it does,
   * because an icon alone cannot say which of three states you are in — and
   * "follow the device" is a state, not the absence of a choice.
   */
  const names: Record<Mode, () => string> = {
    light: m['appearance.light'],
    dark: m['appearance.dark'],
    system: m['appearance.system']
  };

  const current = $derived(preferences.mode);
  const next = $derived(nextMode(current));
</script>

<button
  type="button"
  class="toggle"
  aria-label="{m['appearance.current']({ mode: names[current]() })}. {m['appearance.switch']({
    mode: names[next]()
  })}"
  onclick={() => preferences.setMode(next)}
>
  <span class="icon" aria-hidden="true">
    {#if current === 'light'}
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
        <circle cx="12" cy="12" r="4.2" />
        <path
          d="M12 2.4v2.4M12 19.2v2.4M4.2 12H1.8M22.2 12h-2.4M6.3 6.3 4.6 4.6M19.4 19.4l-1.7-1.7M17.7 6.3l1.7-1.7M4.6 19.4l1.7-1.7"
          stroke-linecap="round"
        />
      </svg>
    {:else if current === 'dark'}
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
        <path d="M20 13.4A8.4 8.4 0 1 1 10.6 4a6.9 6.9 0 0 0 9.4 9.4Z" stroke-linejoin="round" />
      </svg>
    {:else}
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
        <rect x="2.8" y="4.5" width="18.4" height="12.5" rx="1.8" />
        <path d="M8.5 20.5h7" stroke-linecap="round" />
      </svg>
    {/if}
  </span>
</button>

<style>
  .toggle {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    /* 3rem: a target a thumb can hit while holding a phone in a kitchen. */
    width: var(--space-12);
    height: var(--space-12);
    padding: 0;
    border: none;
    border-radius: var(--radius-full);
    background: transparent;
    color: var(--text-muted);
    cursor: pointer;
    transition:
      background-color var(--duration-fast) var(--ease-out),
      color var(--duration-fast) var(--ease-out);
  }

  .toggle:hover {
    background: var(--surface-hover);
    color: var(--text);
  }

  .icon {
    display: block;
    width: var(--space-6);
    height: var(--space-6);
  }

  svg {
    display: block;
    width: 100%;
    height: 100%;
  }
</style>
