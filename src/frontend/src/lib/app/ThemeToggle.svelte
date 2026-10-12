<script lang="ts">
  import { m } from './i18n';
  import { nextMode, type Mode } from './appearance';
  import ModeIcon from './ModeIcon.svelte';
  import { preferences } from './preferences.svelte';

  /**
   * Cycles light, dark, follow the device; the accessible name states the current value and what
   * pressing does.
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
  <span class="icon"><ModeIcon mode={current} /></span>
</button>

<style>
  .toggle {
    display: inline-flex;
    align-items: center;
    justify-content: center;
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
</style>
