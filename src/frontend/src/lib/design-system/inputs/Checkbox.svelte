<script lang="ts">
  /**
   * One yes-or-no, with its label.
   *
   * The whole row is the label, so the target is the text as well as the box —
   * a 16px box is not something to aim at with a thumb.
   */
  interface Props {
    checked: boolean;
    label: string;
    /** Some but not all of a set: the "select all" that is partly selected. */
    indeterminate?: boolean;
    disabled?: boolean;
    describedBy?: string | undefined;
    onchange?: (checked: boolean) => void;
  }

  let {
    checked = $bindable(),
    label,
    indeterminate = false,
    disabled = false,
    describedBy,
    onchange
  }: Props = $props();

  let element = $state<HTMLInputElement>();

  // Indeterminate has no HTML attribute; it exists only as a property.
  $effect(() => {
    if (element) {
      element.indeterminate = indeterminate;
    }
  });
</script>

<label class="row" class:disabled>
  <input
    bind:this={element}
    type="checkbox"
    bind:checked
    {disabled}
    aria-describedby={describedBy}
    onchange={(event) => onchange?.(event.currentTarget.checked)}
  />
  <span class="label">{label}</span>
</label>

<style>
  .row {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    min-height: var(--control-sm);
    cursor: pointer;
  }

  .row.disabled {
    color: var(--text-subtle);
    cursor: not-allowed;
  }

  input {
    flex: none;
    width: var(--space-6);
    height: var(--space-6);
    accent-color: var(--accent);
    cursor: inherit;
  }

  .label {
    min-width: 0;
  }
</style>
