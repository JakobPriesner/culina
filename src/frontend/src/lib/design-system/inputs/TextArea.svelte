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
    oninput?: (value: string) => void;
  }

  let {
    id,
    value = $bindable(),
    placeholder,
    describedBy,
    invalid = false,
    disabled = false,
    rows = 3,
    maxlength,
    oninput
  }: Props = $props();

  let element = $state<HTMLTextAreaElement>();

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
