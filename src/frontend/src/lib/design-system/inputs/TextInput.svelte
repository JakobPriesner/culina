<script lang="ts">
  /**
   * A single line of text.
   *
   * The ids come from `Field`, so a control is never described by nothing and
   * never claims to be valid while showing an error.
   */
  interface Props {
    id: string;
    value: string;
    type?: 'text' | 'email' | 'password' | 'url' | 'search' | 'tel';
    placeholder?: string;
    describedBy?: string | undefined;
    invalid?: boolean;
    disabled?: boolean;
    required?: boolean;
    /** The browser's own suggestion list. `off` only where it would be wrong. */
    autocomplete?: HTMLInputElement['autocomplete'];
    inputmode?: 'text' | 'numeric' | 'decimal' | 'email' | 'url' | 'search';
    maxlength?: number;
    /** Bound by a caller that needs to move focus here. */
    element?: HTMLInputElement;
    /**
     * The accessible name, for the places where there is no visible label.
     *
     * The same escape hatch <code>TextArea</code> has, and for the same kind of
     * reason: a step's title sits where its number used to, and a
     * <code>&lt;label&gt;</code> above it would print the word twice.
     */
    label?: string;
    oninput?: (value: string) => void;
  }

  let {
    id,
    value = $bindable(),
    type = 'text',
    placeholder,
    describedBy,
    invalid = false,
    disabled = false,
    required = false,
    autocomplete,
    inputmode,
    maxlength,
    element = $bindable(),
    label,
    oninput
  }: Props = $props();
</script>

<input
  bind:this={element}
  class="ds-control"
  {id}
  {type}
  {placeholder}
  {disabled}
  {required}
  {autocomplete}
  {inputmode}
  {maxlength}
  bind:value
  aria-label={label}
  aria-describedby={describedBy}
  aria-invalid={invalid ? 'true' : undefined}
  oninput={(event) => oninput?.(event.currentTarget.value)}
/>
