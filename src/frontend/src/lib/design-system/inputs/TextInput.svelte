<script lang="ts">
  import IconButton from '../actions/IconButton.svelte';

  /**
   * A single line of text.
   *
   * The ids come from `Field`, so a control is never described by nothing and
   * never claims to be valid while showing an error.
   */
  interface Props {
    id: string;
    value: string;
    type?: 'text' | 'email' | 'password' | 'url' | 'search' | 'tel' | 'number';
    placeholder?: string;
    describedBy?: string | undefined;
    invalid?: boolean;
    disabled?: boolean;
    required?: boolean;
    /** The browser's own suggestion list. `off` only where it would be wrong. */
    autocomplete?: HTMLInputElement['autocomplete'];
    inputmode?: 'text' | 'numeric' | 'decimal' | 'email' | 'url' | 'search';
    maxlength?: number;
    min?: number;
    max?: number;
    step?: number;
    /** Bound by a caller that needs to move focus here. */
    element?: HTMLInputElement;
    /**
     * How large the field is, which is a statement about what is in it.
     *
     * `display` sets it in the editorial face at title size. For the one field
     * on a screen that holds the thing the screen is about — a recipe's name —
     * and nothing else: a form where two fields are display-sized has no
     * hierarchy, it just has two shouts.
     */
    size?: 'md' | 'display';
    /**
     * Draws the field's edges only on hover and focus.
     *
     * For a control that sits inside a block it does not own, where a box at
     * rest would be one more rectangle on a page that already has enough.
     */
    quiet?: boolean;
    /**
     * The accessible name, for the places where there is no visible label.
     *
     * The same escape hatch <code>TextArea</code> has, and for the same kind of
     * reason: a step's title sits where its number used to, and a
     * <code>&lt;label&gt;</code> above it would print the word twice.
     */
    label?: string;
    /**
     * What the button that shows a password is called.
     *
     * A password field that hides what was typed, with no way to check it, is
     * how a long passphrase gets mistyped twice, so every `type="password"`
     * passes one. Ignored for every other type.
     */
    revealLabel?: string;
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
    min,
    max,
    step,
    element = $bindable(),
    label,
    size = 'md',
    quiet = false,
    revealLabel,
    oninput
  }: Props = $props();

  let revealed = $state(false);
</script>

{#snippet control(shownType: Props['type'])}
  <input
    bind:this={element}
    class="ds-control"
    class:display={size === 'display'}
    class:quiet
    {id}
    type={shownType}
    {placeholder}
    {disabled}
    {required}
    {autocomplete}
    {inputmode}
    {maxlength}
    {min}
    {max}
    {step}
    bind:value
    aria-label={label}
    aria-describedby={describedBy}
    aria-invalid={invalid ? 'true' : undefined}
    oninput={(event) => oninput?.(event.currentTarget.value)}
  />
{/snippet}

{#if type === 'password' && revealLabel}
  <div class="secret">
    {@render control(revealed ? 'text' : 'password')}

    <span class="reveal">
      <IconButton
        label={revealLabel}
        size="sm"
        pressed={revealed}
        {disabled}
        onclick={() => (revealed = !revealed)}
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
          <path d="M2.5 12s3.5-6.5 9.5-6.5 9.5 6.5 9.5 6.5-3.5 6.5-9.5 6.5S2.5 12 2.5 12Z" />
          <circle cx="12" cy="12" r="3" />
          {#if revealed}
            <path d="m4 4 16 16" stroke-linecap="round" />
          {/if}
        </svg>
      </IconButton>
    </span>
  </div>
{:else}
  {@render control(type)}
{/if}

<style>
  .secret {
    position: relative;
    display: flex;
    align-items: center;
    min-width: 0;
  }

  .secret input {
    padding-inline-end: calc(var(--control-sm) + var(--space-2));
  }

  /* Edge draws its own eye inside a password field, which would sit next to ours. */
  .secret input::-ms-reveal {
    display: none;
  }

  .reveal {
    position: absolute;
    inset-inline-end: var(--space-1);
  }
</style>
