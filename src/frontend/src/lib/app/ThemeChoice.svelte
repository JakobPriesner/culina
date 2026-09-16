<script lang="ts">
  import { m } from './i18n';
  import type { Mode } from './appearance';
  import { preferences } from './preferences.svelte';

  /**
   * Light, dark, or whatever the device says — all three visible at once.
   *
   * The cycling `ThemeToggle` is the right control in the header, where there is
   * room for one icon and the choice is a passing one. It is the wrong control
   * here: on a settings screen the question is "which of these three is it", and
   * an icon that has to be pressed twice to find out is an answer nobody asked
   * for.
   *
   * Native radios under the segments, so arrow keys move between them and the
   * chosen one is announced against the row's legend. The segments are the
   * labels; nothing here is a styled `div` pretending to be a control.
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
      <!-- Clipped rather than `display: none`, which would take the radio out
           of the tab order and out of the arrow-key group with it. -->
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
    /* 44px of its own, not 44px for the three together: each segment is a
       separate thing to hit. */
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

  /* The chosen segment is raised out of the track, which says "this one" even
     where the accent is hard to tell apart — colour is never the only signal. */
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
