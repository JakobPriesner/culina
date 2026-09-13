<script lang="ts">
  /**
   * Several lines of text, which grows with what is typed into it.
   *
   * A fixed-height box for a recipe step means writing into a letterbox, and a
   * scrollbar inside a form field hides the beginning of what you just wrote.
   */
  interface Props {
    id: string;
    value: string;
    placeholder?: string;
    describedBy?: string | undefined;
    invalid?: boolean;
    disabled?: boolean;
    /** How tall it starts. It never gets shorter than this. */
    rows?: number;
    maxlength?: number;
    /**
     * The element itself, for the rare caller that has to drive the cursor.
     *
     * Writing a mention into a step means replacing the half-typed name the
     * cursor sits in, and only the textarea knows where that is.
     */
    element?: HTMLTextAreaElement;
    /**
     * The accessible name, for the places where there is no visible label.
     *
     * A step in the editor is one of them: the number beside it is the label a
     * sighted person reads, and a `<label>` repeating it would be a second copy
     * of the same word on screen.
     */
    label?: string;
    oninput?: (value: string) => void;
    /** Set when this field is the text half of a combobox. */
    combobox?: {
      /** Whether the list of suggestions is showing. */
      readonly expanded: boolean;
      /** The id of that list. */
      readonly controls: string;
      /** The id of the suggestion arrow keys have landed on. */
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
    rows = 3,
    maxlength,
    oninput,
    combobox,
    element = $bindable()
  }: Props = $props();

  // Measured rather than guessed from the character count: a pasted paragraph
  // and a list of short lines take different amounts of room.
  function grow() {
    if (!element) {
      return;
    }

    element.style.height = 'auto';
    element.style.height = `${element.scrollHeight}px`;
  }

  $effect(grow);
</script>

<textarea
  class="ds-control area"
  bind:this={element}
  {id}
  {placeholder}
  {disabled}
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
