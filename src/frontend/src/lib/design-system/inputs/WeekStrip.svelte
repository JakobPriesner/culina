<script lang="ts">
  import IconButton from '../actions/IconButton.svelte';

  /**
   * Seven days to choose from, the way a calendar shows a week (docs/food-log-redesign.md §4.1): weekday over number,
   * the chosen day in a filled circle, today's number in the accent, and a small neutral dot under a day that has
   * something in it. Never a colour for a day being "good" or "bad". Arrows move by a week.
   */
  export interface StripDay {
    readonly id: string;
    /** Short weekday, as the column heads a calendar: "Mo", "Di". */
    readonly weekday: string;
    readonly number: string;
    /** The whole day for a screen reader: "Samstag, 24. Oktober". */
    readonly label: string;
    /** Something is in this day. */
    readonly marked?: boolean;
    /** Not to be chosen, such as a day still to come. */
    readonly disabled?: boolean;
  }

  interface Props {
    days: readonly StripDay[];
    selected: string;
    today?: string;
    /** Names the strip: "Woche vom 19. Oktober". */
    label: string;
    onselect: (id: string) => void;
    previousLabel: string;
    nextLabel: string;
    onprevious: () => void;
    onnext: () => void;
    nextDisabled?: boolean;
  }

  let {
    days,
    selected,
    today,
    label,
    onselect,
    previousLabel,
    nextLabel,
    onprevious,
    onnext,
    nextDisabled = false
  }: Props = $props();
</script>

<div class="strip" role="group" aria-label={label}>
  <IconButton label={previousLabel} size="sm" onclick={onprevious}>
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true"
      ><path d="m15 6-6 6 6 6" stroke-linecap="round" stroke-linejoin="round" /></svg
    >
  </IconButton>

  <ol class="days">
    {#each days as day (day.id)}
      <li>
        <button
          type="button"
          class="day"
          class:selected={day.id === selected}
          class:today={day.id === today}
          aria-pressed={day.id === selected}
          aria-current={day.id === today ? 'date' : undefined}
          aria-label={day.label}
          disabled={day.disabled}
          onclick={() => onselect(day.id)}
        >
          <span class="weekday" aria-hidden="true">{day.weekday}</span>
          <span class="number" aria-hidden="true">{day.number}</span>
          <span class="dot" class:marked={day.marked} aria-hidden="true"></span>
        </button>
      </li>
    {/each}
  </ol>

  <IconButton label={nextLabel} size="sm" disabled={nextDisabled} onclick={onnext}>
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true"
      ><path d="m9 6 6 6-6 6" stroke-linecap="round" stroke-linejoin="round" /></svg
    >
  </IconButton>
</div>

<style>
  .strip {
    display: flex;
    align-items: center;
    gap: var(--space-1);
  }

  .days {
    display: grid;
    flex: 1;
    grid-template-columns: repeat(7, minmax(0, 1fr));
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .day {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.125rem;
    inline-size: 100%;
    padding: var(--space-1) 0;
    border: 0;
    border-radius: var(--radius-md);
    background: none;
    color: var(--text);
    font: inherit;
    cursor: pointer;
  }

  .day:disabled {
    color: var(--text-subtle);
    cursor: default;
  }

  .day:focus-visible {
    outline: 2px solid var(--border-focus);
    outline-offset: 1px;
  }

  .weekday {
    color: var(--text-muted);
    font-size: var(--text-xs);
  }

  /* Calendar's marks: the chosen day filled, today's number in the accent. */
  .number {
    display: grid;
    place-items: center;
    inline-size: 2rem;
    block-size: 2rem;
    border-radius: var(--radius-full);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    font-variant-numeric: tabular-nums;
    transition: background-color var(--duration-fast) var(--ease-out);
  }

  .today .number {
    color: var(--accent);
    font-weight: var(--weight-semibold);
  }

  .selected .number {
    background: var(--text);
    color: var(--surface);
  }

  .selected.today .number {
    background: var(--accent);
    color: var(--accent-contrast);
  }

  @media (hover: hover) {
    .day:not(:disabled, .selected):hover .number {
      background: var(--surface-hover);
    }
  }

  .dot {
    inline-size: 0.3rem;
    block-size: 0.3rem;
    border-radius: var(--radius-full);
  }

  .marked {
    background: var(--text-subtle);
  }
</style>
