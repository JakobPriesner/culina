<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * The one action element.
   *
   * A `<button>` when it does something and an `<a>` when it goes somewhere —
   * never a styled `div`, because a div cannot be reached by keyboard, cannot
   * be activated by space, and tells a screen reader nothing.
   */
  export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';
  export type ButtonSize = 'sm' | 'md' | 'lg';

  interface Props {
    children: Snippet;
    /** Rendered before the label. Decorative: the label carries the meaning. */
    icon?: Snippet;
    variant?: ButtonVariant;
    size?: ButtonSize;
    /** Fills its container — for a form's submit, or a sheet's confirm. */
    full?: boolean;
    type?: 'button' | 'submit' | 'reset';
    disabled?: boolean;
    /** Shows progress without losing the label or changing width. */
    loading?: boolean;
    /** Turns the button into a link. Navigation belongs in an anchor. */
    href?: string;
    /** Announced in place of the label when the label is only an icon. */
    label?: string;
    onclick?: (event: MouseEvent) => void;
  }

  let {
    children,
    icon,
    variant = 'secondary',
    size = 'md',
    full = false,
    type = 'button',
    disabled = false,
    loading = false,
    href,
    label,
    onclick
  }: Props = $props();

  // A button that is working is not available, but it must still be readable by
  // assistive technology — `aria-disabled` rather than `disabled`, which would
  // remove it from the tab order mid-interaction and move focus somewhere
  // unexpected.
  const inert = $derived(disabled || loading);
</script>

{#snippet body()}
  {#if icon}
    <span class="icon" aria-hidden="true">{@render icon()}</span>
  {/if}
  <span class="label">{@render children()}</span>
  {#if loading}
    <span class="spinner" aria-hidden="true"></span>
  {/if}
{/snippet}

{#if href}
  <a
    class="button {variant} {size}"
    class:full
    class:loading
    href={inert ? undefined : href}
    aria-disabled={inert ? 'true' : undefined}
    aria-label={label}
    aria-busy={loading ? 'true' : undefined}
  >
    {@render body()}
  </a>
{:else}
  <button
    class="button {variant} {size}"
    class:full
    class:loading
    {type}
    disabled={disabled && !loading}
    aria-disabled={inert ? 'true' : undefined}
    aria-label={label}
    aria-busy={loading ? 'true' : undefined}
    onclick={(event) => {
      if (inert) {
        event.preventDefault();

        return;
      }

      onclick?.(event);
    }}
  >
    {@render body()}
  </button>
{/if}

<style>
  .button {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    gap: var(--space-2);
    border: 1px solid transparent;
    border-radius: var(--radius-md);
    font: inherit;
    font-weight: var(--weight-medium);
    text-decoration: none;
    white-space: nowrap;
    cursor: pointer;
    transition:
      background-color var(--duration-fast) var(--ease-out),
      border-color var(--duration-fast) var(--ease-out),
      color var(--duration-fast) var(--ease-out);
  }

  .sm {
    height: var(--control-sm);
    padding-inline: var(--space-3);
    font-size: var(--text-sm);
  }

  .md {
    height: var(--control-md);
    padding-inline: var(--space-4);
  }

  .lg {
    height: var(--control-lg);
    padding-inline: var(--space-6);
    font-size: var(--text-lg);
  }

  .full {
    display: flex;
    width: 100%;
  }

  .primary {
    background: var(--accent);
    color: var(--accent-contrast);
  }

  .primary:hover {
    background: var(--accent-hover);
  }

  .secondary {
    border-color: var(--border-strong);
    background: var(--surface-raised);
    color: var(--text);
  }

  .secondary:hover {
    background: var(--surface-hover);
  }

  .ghost {
    background: transparent;
    color: var(--text-muted);
  }

  .ghost:hover {
    background: var(--surface-hover);
    color: var(--text);
  }

  .danger {
    background: var(--danger);
    color: var(--accent-contrast);
  }

  .danger:hover {
    background: var(--danger-hover);
  }

  .button[aria-disabled='true'],
  .button:disabled {
    cursor: not-allowed;
    opacity: 0.55;
  }

  .icon {
    display: inline-flex;
    width: var(--space-4);
    height: var(--space-4);
  }

  /* The label stays put while the spinner appears beside it: a button that
     swaps its text for a spinner changes width, and everything after it moves. */
  .loading .label {
    opacity: 0.7;
  }

  .spinner {
    width: var(--space-4);
    height: var(--space-4);
    border: 2px solid currentcolor;
    border-top-color: transparent;
    border-radius: var(--radius-full);
    animation: spin 700ms linear infinite;
  }

  @keyframes spin {
    to {
      transform: rotate(1turn);
    }
  }

  /* Without motion the ring would sit there as a broken circle, so it becomes a
     steady dot instead — present, but not pretending to move. */
  @media (prefers-reduced-motion: reduce) {
    .spinner {
      border-top-color: currentcolor;
      opacity: 0.5;
    }
  }
</style>
