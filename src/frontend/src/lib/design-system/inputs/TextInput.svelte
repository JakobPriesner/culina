<script lang="ts">
  import IconButton from '../actions/IconButton.svelte';

  /** A single line of text; ids come from `Field`. */
  interface Props {
    id: string;
    value: string;
    type?: 'text' | 'email' | 'password' | 'url' | 'search' | 'tel' | 'number';
    placeholder?: string;
    describedBy?: string | undefined;
    invalid?: boolean;
    disabled?: boolean;
    required?: boolean;
    /** `off` only where the browser's suggestion list would be wrong. */
    autocomplete?: HTMLInputElement['autocomplete'];
    inputmode?: 'text' | 'numeric' | 'decimal' | 'email' | 'url' | 'search';
    maxlength?: number;
    min?: number;
    max?: number;
    step?: number;
    element?: HTMLInputElement;
    /** `display` sets the editorial face at title size, for the one field a screen is about (a recipe's name). */
    size?: 'md' | 'display';
    /** Draws the field's edges only on hover and focus, for controls inside a block that does not own them. */
    quiet?: boolean;
    /** Accessible name where there is no visible label (as in `TextArea`): a visible label would print the word twice. */
    label?: string;
    /** Name of the show-password button; every `type="password"` passes one. Ignored for other types. */
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
