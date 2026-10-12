<script lang="ts">
  import { m } from './i18n';
  import type { Mode } from './appearance';
  import ModeIcon from './ModeIcon.svelte';
  import { preferences } from './preferences.svelte';

  /**
   * Light, dark or device, all three visible at once: on settings the question is "which is it", which the header's cycling `ThemeToggle` answers only after two presses.
   * Native radios under the segments, so arrow keys move between them and the choice is announced against the legend.
   * `compact` draws each segment as its icon, for the account menu, which has no room for "Use device setting".
   */
  let { compact = false }: { compact?: boolean } = $props();

  // Per instance: the account menu's copy is in the page too while settings shows its own, and one shared name would make them one group.
  const name = $props.id();

  const options: readonly { value: Mode; label: () => string }[] = [
    { value: 'light', label: m['appearance.light'] },
    { value: 'dark', label: m['appearance.dark'] },
    { value: 'system', label: m['appearance.system'] }
  ];

  const current = $derived(preferences.mode);
</script>

<div class="track" class:compact>
  {#each options as option (option.value)}
    <label
      class="segment"
      class:chosen={current === option.value}
      title={compact ? option.label() : undefined}
    >
      <!-- Clipped rather than `display: none`, which would drop the radio from the tab order and arrow-key group. -->
      <input
        class="ds-clipped"
        type="radio"
        {name}
        value={option.value}
        checked={current === option.value}
        onchange={() => preferences.setMode(option.value)}
      />
      {#if compact}
        <span class="icon"><ModeIcon mode={option.value} /></span>
        <span class="ds-clipped">{option.label()}</span>
      {:else}
        <span>{option.label()}</span>
      {/if}
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

  .compact {
    flex-wrap: nowrap;
  }

  .compact .segment {
    min-width: var(--control-sm);
    padding-inline: var(--space-2);
  }

  .icon {
    display: block;
    width: calc(var(--space-4) + var(--space-1));
    height: calc(var(--space-4) + var(--space-1));
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
