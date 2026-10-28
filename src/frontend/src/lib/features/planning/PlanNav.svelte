<script lang="ts">
  import { m } from '$shell/i18n';

  import type { PlanView } from './planView.svelte';

  /**
   * Back, today, forward: Calendar's order, as one capsule. Today is always there and in the same place, so a thumb
   * finds it without looking; it rests (disabled) while today is already the start of what is shown.
   */
  interface Props {
    view: PlanView;
    /** Today is where the days on screen start (or the week on screen is this one). */
    atToday: boolean;
    onstep: (direction: -1 | 1) => void;
    ontoday: () => void;
  }

  let { view, atToday, onstep, ontoday }: Props = $props();
</script>

<div class="nav" role="group" aria-label={m['plan.jump.label']()}>
  <button
    type="button"
    class="step"
    aria-label={view === 'days' ? m['plan.previousDay']() : m['plan.previousWeek']()}
    onclick={() => onstep(-1)}
  >
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="2"
      stroke-linecap="round"
      stroke-linejoin="round"
      aria-hidden="true"
    >
      <path d="m15 5-7 7 7 7" />
    </svg>
  </button>
  <button type="button" class="today" disabled={atToday} onclick={ontoday}>
    {m['plan.today']()}
  </button>
  <button
    type="button"
    class="step"
    aria-label={view === 'days' ? m['plan.nextDay']() : m['plan.nextWeek']()}
    onclick={() => onstep(1)}
  >
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="2"
      stroke-linecap="round"
      stroke-linejoin="round"
      aria-hidden="true"
    >
      <path d="m9 5 7 7-7 7" />
    </svg>
  </button>
</div>

<style>
  /* One capsule with hairline seams, the same height and edge as the segmented control beside it. */
  .nav {
    display: inline-flex;
    flex: none;
    align-items: stretch;
    padding: var(--space-1);
    border: 1px solid var(--border);
    border-radius: var(--radius-full);
    background: var(--surface-sunken);
  }

  button {
    display: inline-grid;
    place-items: center;
    min-width: var(--control-sm);
    min-height: var(--control-sm);
    padding: 0;
    border: none;
    border-radius: var(--radius-full);
    background: none;
    color: var(--text);
    font: inherit;
    font-size: var(--text-xs);
    font-weight: var(--weight-medium);
    white-space: nowrap;
    cursor: pointer;
    transition:
      background-color var(--duration-fast) var(--ease-out),
      color var(--duration-fast) var(--ease-out);
  }

  button:hover:not(:disabled) {
    background: var(--surface-hover);
  }

  button:active:not(:disabled) {
    background: var(--surface-selected);
  }

  button:focus-visible {
    outline: 2px solid var(--border-focus);
    outline-offset: 2px;
  }

  .step svg {
    width: 1.125rem;
    height: 1.125rem;
  }

  .today {
    padding-inline: var(--space-3);
  }

  /* A phone: the capsule and the dates share one row at 390px. */
  @media (max-width: 30rem) {
    .today {
      padding-inline: var(--space-2);
    }
  }

  /* Resting, not gone: the capsule keeps its shape and the label its place. */
  .today:disabled {
    color: var(--text-subtle);
    cursor: default;
  }
</style>
