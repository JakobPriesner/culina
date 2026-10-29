<script lang="ts">
  /** A setting that takes effect immediately: a switch, not a checkbox, as there is no Save. */
  interface Props {
    checked: boolean;
    label: string;
    description?: string;
    disabled?: boolean;
    /** For a row that already prints the label and explanation: they still name and describe the switch, without a second visible copy. */
    hideLabel?: boolean;
    onchange?: (checked: boolean) => void;
  }

  let {
    checked = $bindable(),
    label,
    description,
    disabled = false,
    hideLabel = false,
    onchange
  }: Props = $props();

  const id = $props.id();
  const labelId = `${id}-label`;
  const descriptionId = `${id}-description`;
</script>

<div class="row" class:disabled>
  <span class="text" class:ds-clipped={hideLabel}>
    <!-- Not a <label for>: a role="switch" button is not labelable, so the association is explicit.
         -->
    <span class="label" id={labelId}>{label}</span>
    {#if description}
      <span class="description" id={descriptionId}>{description}</span>
    {/if}
  </span>

  <button
    {id}
    class="switch"
    type="button"
    role="switch"
    aria-checked={checked}
    aria-labelledby={labelId}
    aria-describedby={description ? descriptionId : undefined}
    {disabled}
    onclick={() => {
      checked = !checked;
      onchange?.(checked);
    }}
  >
    <span class="knob"></span>
  </button>
</div>

<style>
  .row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-4);
    min-height: var(--control-sm);
  }

  .row.disabled {
    color: var(--text-subtle);
  }

  .text {
    display: flex;
    flex-direction: column;
    min-width: 0;
  }

  .label {
    font-weight: var(--weight-medium);
  }

  .description {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .switch {
    position: relative;
    flex: none;
    width: var(--space-12);
    height: var(--space-8);
    padding: 0;
    border: none;
    border-radius: var(--radius-full);
    background: var(--border-strong);
    cursor: pointer;
    transition: background-color var(--duration-base) var(--ease-out);
  }

  .switch[aria-checked='true'] {
    background: var(--accent);
  }

  .switch:disabled {
    cursor: not-allowed;
    opacity: 0.55;
  }

  .knob {
    position: absolute;
    inset-block-start: var(--space-1);
    inset-inline-start: var(--space-1);
    width: var(--space-6);
    height: var(--space-6);
    border-radius: var(--radius-full);
    background: var(--surface-raised);
    /* The knob slides because the movement says which side is on. */
    transition:
      transform var(--duration-base) var(--ease-spatial),
      width var(--duration-fast) var(--ease-out),
      box-shadow var(--duration-fast) var(--ease-out);
  }

  .switch[aria-checked='true'] .knob {
    transform: translateX(var(--space-4));
  }

  /*
   * Held, the knob stretches toward where it would go and lifts, as an iOS switch's does under a finger: the control
   * answers the touch before it changes. Checked, it stretches back from the trailing side.
   */
  .switch:active:not(:disabled) .knob {
    width: calc(var(--space-6) + var(--space-2));
    box-shadow: var(--shadow-card);
  }

  .switch[aria-checked='true']:active:not(:disabled) .knob {
    transform: translateX(calc(var(--space-4) - var(--space-2)));
  }
</style>
