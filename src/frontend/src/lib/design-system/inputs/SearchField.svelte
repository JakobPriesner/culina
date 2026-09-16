<script lang="ts">
  /**
   * Search, with a way out of it.
   *
   * The clear button only exists while there is something to clear: a control
   * that is always there but usually does nothing is one more thing to read
   * past. Typing is debounced by the caller, not here — this component does not
   * decide how expensive a search is.
   */
  import IconButton from '../actions/IconButton.svelte';

  interface Props {
    id: string;
    shape?: 'default' | 'pill';
    value: string;
    placeholder: string;
    /** Announced for the field itself, which has no visible label in a toolbar. */
    label: string;
    clearLabel: string;
    disabled?: boolean;
    describedBy?: string | undefined;
    oninput?: (value: string) => void;
    onclear?: () => void;
  }

  let {
    id,
    shape = 'default',
    value = $bindable(),
    placeholder,
    label,
    clearLabel,
    disabled = false,
    describedBy,
    oninput,
    onclear
  }: Props = $props();

  let element = $state<HTMLInputElement>();

  function clear() {
    value = '';
    oninput?.('');
    onclear?.();
    // Focus goes back where the typing was, so clearing does not end the task.
    element?.focus();
  }
</script>

<div class="search">
  <span class="icon" aria-hidden="true">
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
      <circle cx="10.5" cy="10.5" r="6.5" />
      <path d="m15.5 15.5 4 4" stroke-linecap="round" />
    </svg>
  </span>

  <input
    bind:this={element}
    class="ds-control input"
    class:pill={shape === 'pill'}
    {id}
    type="search"
    enterkeyhint="search"
    onkeydown={(event) => {
      if (event.key === 'Escape' && value && !disabled) {
        event.preventDefault();
        event.stopPropagation();
        clear();
      }
    }}
    {placeholder}
    {disabled}
    bind:value
    aria-label={label}
    aria-describedby={describedBy}
    oninput={(event) => oninput?.(event.currentTarget.value)}
  />

  {#if value}
    <span class="clear">
      <IconButton label={clearLabel} size="sm" {disabled} onclick={clear}>
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="m6 6 12 12M18 6 6 18" stroke-linecap="round" />
        </svg>
      </IconButton>
    </span>
  {/if}
</div>

<style>
  .search {
    position: relative;
    display: flex;
    align-items: center;
    min-width: 0;
  }

  .input {
    padding-inline-start: var(--space-12);
    padding-inline-end: calc(var(--control-sm) + var(--space-2));
    border-radius: var(--radius-md);
  }

  .input.pill {
    border-radius: var(--radius-full);
    background: var(--surface-sunken);
  }

  /* The browser's own clear affordance would sit next to ours. */
  .input::-webkit-search-cancel-button {
    appearance: none;
  }

  .icon {
    position: absolute;
    inset-inline-start: var(--space-4);
    width: var(--space-4);
    height: var(--space-4);
    color: var(--text-subtle);
    pointer-events: none;
  }

  .search:focus-within .icon {
    color: var(--accent);
  }

  .icon :global(svg) {
    display: block;
    width: 100%;
    height: 100%;
  }

  .clear {
    position: absolute;
    inset-inline-end: var(--space-1);
  }
</style>
