<script lang="ts">
  /**
   * A choice from a short, known list.
   *
   * The native element on purpose: it is the control every person already
   * knows, it works on a phone without a single line of code, and a custom
   * replacement buys nothing but keyboard bugs.
   */
  export interface SelectOption {
    readonly value: string;
    readonly label: string;
    readonly disabled?: boolean;
  }

  interface Props {
    id: string;
    value: string;
    options: readonly SelectOption[];
    describedBy?: string | undefined;
    invalid?: boolean;
    disabled?: boolean;
    onchange?: (value: string) => void;
  }

  let {
    id,
    value = $bindable(),
    options,
    describedBy,
    invalid = false,
    disabled = false,
    onchange
  }: Props = $props();
</script>

<select
  class="ds-control select"
  {id}
  {disabled}
  bind:value
  aria-describedby={describedBy}
  aria-invalid={invalid ? 'true' : undefined}
  onchange={(event) => onchange?.(event.currentTarget.value)}
>
  {#each options as option (option.value)}
    <option value={option.value} disabled={option.disabled}>{option.label}</option>
  {/each}
</select>

<style>
  .select {
    /* The native arrow needs room that padding-inline alone does not leave. */
    padding-inline-end: var(--space-2);
    cursor: pointer;
  }
</style>
