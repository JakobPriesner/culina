<script lang="ts">
  /** One yes-or-no with its label; the whole row is the label, since a 16px box is no thumb target. */
  interface Props {
    checked: boolean;
    label: string;
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

  /* Drawn, not native: in dark mode the native control is a filled grey square that reads as disabled.
     `appearance: none` keeps the real input focusable, announced and toggled by Space. */
  input {
    appearance: none;
    flex: none;
    display: grid;
    place-content: center;
    width: var(--space-6);
    height: var(--space-6);
    margin: 0;
    border: 1px solid var(--border-strong);
    border-radius: var(--radius-sm);
    background: var(--surface-raised);
    cursor: inherit;
    transition:
      background-color var(--duration-fast) var(--ease-out),
      border-color var(--duration-fast) var(--ease-out);
  }

  .row:hover input:not(:disabled) {
    border-color: var(--text-subtle);
  }

  input:checked,
  input:indeterminate {
    border-color: var(--accent);
    background: var(--accent);
  }

  /* The tick: two turned sides of a square; a glyph or image would escape the theme's colour control. */
  input::before {
    content: '';
    width: var(--space-2);
    height: var(--space-4);
    border: solid var(--accent-contrast);
    border-width: 0 2px 2px 0;
    transform: translateY(-1px) rotate(45deg) scale(0);
    transition: transform var(--duration-fast) var(--ease-out);
  }

  input:checked::before {
    transform: translateY(-1px) rotate(45deg) scale(1);
  }

  input:indeterminate::before {
    width: var(--space-3);
    height: 0;
    border-width: 0 0 2px 0;
    transform: none;
  }

  input:disabled {
    background: var(--surface-sunken);
    border-color: var(--border);
  }

  input:checked:disabled,
  input:indeterminate:disabled {
    background: var(--text-subtle);
    border-color: var(--text-subtle);
  }

  .label {
    min-width: 0;
  }
</style>
