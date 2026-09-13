<script lang="ts">
  import IconButton from '../actions/IconButton.svelte';

  /**
   * A small whole number, changed by pressing rather than typing.
   *
   * Built for servings: the common change is one up or one down, and doing that
   * with a keyboard on a phone in a kitchen is the worst version of an easy
   * thing. The value stays a real input, so it can still be typed when someone
   * wants eighteen.
   */
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
     * Replaces what a tap on plus or minus arrives at.
     *
     * For a value that is displayed rounded — a recipe scaled to an amount
     * somebody has reads as 7½ servings while its amounts are computed from
     * 7.4 — stepping from the number on screen would produce 6½ and 8½. The
     * owner of the value decides where a tap lands; typing still goes through
     * `onchange`.
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
    // Clamped here rather than trusted from the input, because typing 500 into
    // a servings box should not produce a shopping list for a wedding.
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
    /* Sized by its contents even inside a stretching column: a servings control
       as wide as the form would look like a text field. */
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
    /* The spinners duplicate the two buttons either side of them, and they are
       far too small to hit. */
    appearance: textfield;
  }

  .value::-webkit-outer-spin-button,
  .value::-webkit-inner-spin-button {
    appearance: none;
    margin: 0;
  }
</style>
