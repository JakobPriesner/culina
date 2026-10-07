<script lang="ts">
  import IconButton from '../actions/IconButton.svelte';

  /** A small whole number changed by pressing; stays a real input so it can still be typed. */
  interface Props {
    id: string;
    value: number;
    label: string;
    decreaseLabel: string;
    increaseLabel: string;
    min?: number;
    max?: number;
    step?: number;
    disabled?: boolean;
    describedBy?: string | undefined;
    onchange?: (value: number) => void;
    /**
     * Replaces where a tap lands, for a value displayed rounded (7½ shown, 7.4 held); typing still
     * goes through `onchange`.
     */
    onstep?: (direction: 1 | -1) => void;
  }

  let {
    id,
    value = $bindable(),
    label,
    decreaseLabel,
    increaseLabel,
    min = 1,
    max = 99,
    step = 1,
    disabled = false,
    describedBy,
    onchange,
    onstep
  }: Props = $props();

  const atMin = $derived(value <= min);
  const atMax = $derived(value >= max);

  function set(next: number) {
    // Clamped here, so typing 500 doesn't produce a shopping list for a wedding.
    const clamped = Math.min(max, Math.max(min, Math.round(next)));

    if (clamped !== value) {
      value = clamped;
      onchange?.(clamped);
    }
  }
</script>

<div class="stepper" class:disabled>
  <IconButton
    label={decreaseLabel}
    size="sm"
    disabled={disabled || atMin}
    onclick={() => (onstep ? onstep(-1) : set(value - step))}
  >
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
      <path d="M5 12h14" stroke-linecap="round" />
    </svg>
  </IconButton>

  <input
    class="value"
    {id}
    type="number"
    inputmode="numeric"
    {min}
    {max}
    {step}
    {disabled}
    aria-label={label}
    aria-describedby={describedBy}
    value={String(value)}
    onchange={(event) => set(Number(event.currentTarget.value))}
  />

  <IconButton
    label={increaseLabel}
    size="sm"
    disabled={disabled || atMax}
    onclick={() => (onstep ? onstep(1) : set(value + step))}
  >
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
      <path d="M12 5v14M5 12h14" stroke-linecap="round" />
    </svg>
  </IconButton>
</div>

<style>
  .stepper {
    display: inline-flex;
    /* Sized by its contents inside a stretching column, or it would look like a text field. */
    align-self: flex-start;
    align-items: center;
    gap: var(--space-1);
    border: 1px solid var(--border-strong);
    border-radius: var(--radius-full);
    background: var(--surface-raised);
  }

  .stepper.disabled {
    opacity: 0.55;
  }

  .value {
    width: 3ch;
    border: none;
    background: transparent;
    color: var(--text);
    font: inherit;
    font-variant-numeric: tabular-nums;
    font-weight: var(--weight-semibold);
    text-align: center;
    /* The spinners duplicate the buttons and are too small to hit. */
    appearance: textfield;
  }

  .value::-webkit-outer-spin-button,
  .value::-webkit-inner-spin-button {
    appearance: none;
    margin: 0;
  }
</style>
