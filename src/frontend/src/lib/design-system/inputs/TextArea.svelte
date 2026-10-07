<script lang="ts">
  /** Several lines that grow with the text: a fixed-height box is a letterbox, and an inner scrollbar hides the start. */
  interface Props {
    id: string;
    value: string;
    placeholder?: string;
    describedBy?: string | undefined;
    invalid?: boolean;
    disabled?: boolean;
    /** Readable and focusable but not typeable: `disabled` would grey it out on every visit, and typing would let a late answer overwrite it. */
    readonly?: boolean;
    /** How tall it starts. It never gets shorter than this. */
    rows?: number;
    maxlength?: number;
    /** The element itself, for callers that drive the cursor (writing a mention replaces the half-typed name). */
    element?: HTMLTextAreaElement;
    /** Accessible name where no visible label exists (a step's number is the visible label). */
    label?: string;
    oninput?: (value: string) => void;
    /** Set when this field is the text half of a combobox. */
    combobox?: {
      readonly expanded: boolean;
      readonly controls: string;
      readonly active?: string | undefined;
    };
  }

  let {
    id,
    label,
    value = $bindable(),
    placeholder,
    describedBy,
    invalid = false,
    disabled = false,
    readonly = false,
    rows = 3,
    maxlength,
    oninput,
    combobox,
    element = $bindable()
  }: Props = $props();

  // Measured, not guessed from character count: a pasted paragraph and short lines need different room.
  function grow() {
    if (!element) {
      return;
    }

    element.style.height = 'auto';
    element.style.height = `${element.scrollHeight}px`;
  }

  // Also for values set from outside (a note arriving late): with overflow hidden the rest would be unreachable.
  $effect(() => {
    void value;
    grow();
  });
</script>

<textarea
  class="ds-control area"
  bind:this={element}
  {id}
  {placeholder}
  {disabled}
  {readonly}
  {rows}
  {maxlength}
  bind:value
  role={combobox ? 'combobox' : undefined}
  aria-expanded={combobox ? combobox.expanded : undefined}
  aria-controls={combobox?.controls}
  aria-activedescendant={combobox?.active}
  aria-autocomplete={combobox ? 'list' : undefined}
  aria-label={label}
  aria-describedby={describedBy}
  aria-invalid={invalid ? 'true' : undefined}
  oninput={(event) => {
    grow();
    oninput?.(event.currentTarget.value);
  }}></textarea>

<style>
  .area {
    height: auto;
    min-height: var(--control-md);
    padding-block: var(--space-2);
    line-height: var(--leading-normal);
    resize: none;
    overflow: hidden;
  }
</style>
