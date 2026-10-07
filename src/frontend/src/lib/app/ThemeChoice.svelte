<script lang="ts">
  import { m } from './i18n';
  import type { Mode } from './appearance';
  import { preferences } from './preferences.svelte';

  /**
   * Light, dark or device, all three visible at once: on settings the question is "which is it", which the header's cycling `ThemeToggle` answers only after two presses.
   * Native radios under the segments, so arrow keys move between them and the choice is announced against the legend.
   */
  const options: readonly { value: Mode; label: () => string }[] = [
    { value: 'light', label: m['appearance.light'] },
    { value: 'dark', label: m['appearance.dark'] },
    { value: 'system', label: m['appearance.system'] }
  ];

  const current = $derived(preferences.mode);
</script>

<div class="track">
  {#each options as option (option.value)}
    <label class="segment" class:chosen={current === option.value}>
      <!-- Clipped rather than `display: none`, which would drop the radio from the tab order and arrow-key group. -->
      <input
        class="ds-clipped"
        type="radio"
        name="appearance-mode"
        value={option.value}
        checked={current === option.value}
        onchange={() => preferences.setMode(option.value)}
      />
      <span>{option.label()}</span>
    </label>
  {/each}
</div>

<style>
  .track {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-1);
    padding: var(--space-1);
    border-radius: var(--radius-full);
    background: var(--surface-sunken);
  }

  .segment {
    display: flex;
    align-items: center;
    justify-content: center;
    /* 44px each, not for all three: each segment is its own target. */
    min-height: var(--control-sm);
    padding-inline: var(--space-4);
    border-radius: var(--radius-full);
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    white-space: nowrap;
    cursor: pointer;
    transition:
      background-color var(--duration-fast) var(--ease-out),
      color var(--duration-fast) var(--ease-out);
  }

  .segment:hover:not(.chosen) {
    color: var(--text);
  }

  /* The chosen segment is raised, so colour is never the only signal. */
  .chosen {
    background: var(--surface-raised);
    box-shadow: var(--shadow-card);
    color: var(--text);
    font-weight: var(--weight-semibold);
  }

  .segment:has(input:focus-visible) {
    outline: 2px solid var(--border-focus);
    outline-offset: 2px;
  }
</style>
