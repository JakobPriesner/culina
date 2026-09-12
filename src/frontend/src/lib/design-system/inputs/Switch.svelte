<script lang="ts">
  /**
   * A setting that takes effect immediately.
   *
   * A switch, not a checkbox, because there is no Save: flipping it *is* the
   * action. A checkbox that applied itself would be a lie about what happens
   * next.
   */
  interface Props {
    checked: boolean;
    label: string;
    /** One line under the label, for what the setting actually does. */
    description?: string;
    disabled?: boolean;
    onchange?: (checked: boolean) => void;
  }

  let { checked = $bindable(), label, description, disabled = false, onchange }: Props = $props();

  const id = $props.id();
  const labelId = `${id}-label`;
  const descriptionId = `${id}-description`;
</script>

<div class="row" class:disabled>
  <span class="text">
    <!--
      Not a <label for>: only form controls are labelable, and a role="switch"
      button is not one, so the association has to be made explicitly.
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
    /* The knob slides rather than jumping, because the movement is what says
       which side is on. */
    transition: transform var(--duration-base) var(--ease-spatial);
  }

  .switch[aria-checked='true'] .knob {
    transform: translateX(var(--space-4));
  }
</style>
