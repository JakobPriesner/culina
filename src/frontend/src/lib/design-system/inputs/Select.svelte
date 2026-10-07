<script lang="ts">
  /** A choice from a short, known list; the native element on purpose (familiar, works on phones, no keyboard bugs). */
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
    /** Sized to its longest option, for a select that is the whole control beside its label (a two-word choice should not look like a text field). */
    inline?: boolean;
    onchange?: (value: string) => void;
  }

  let {
    id,
    value = $bindable(),
    options,
    describedBy,
    invalid = false,
    disabled = false,
    inline = false,
    onchange
  }: Props = $props();
</script>

<select
  class="ds-control select"
  class:inline
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

  .select.inline {
    width: auto;
  }
</style>
