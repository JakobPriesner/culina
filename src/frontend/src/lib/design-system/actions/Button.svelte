<script lang="ts">
  import type { Snippet } from 'svelte';

  /** `<button>` when it acts, `<a>` when it navigates: a div has no keyboard, space activation or semantics. */
  /** `media` is for buttons lying on a photograph, which can't take their colour from the page. */
  export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger' | 'media';
  export type ButtonSize = 'sm' | 'md' | 'lg';

  interface Props {
    children: Snippet;
    /** Decorative, before the label. */
    icon?: Snippet;
    variant?: ButtonVariant;
    size?: ButtonSize;
    full?: boolean;
    type?: 'button' | 'submit' | 'reset';
    disabled?: boolean;
    /** Shows progress without losing the label or changing width. */
    loading?: boolean;
    href?: string;
    /**
     * Saves the target instead of navigating; only with `href`, for what the browser would
     * otherwise display. The browser's download gives a file a name and progress.
     */
    download?: string;
    /** Announced in place of the label when the label is only an icon. */
    label?: string;
    /** The popover this button opens (from `Popover`'s trigger); the browser handles toggling, light dismiss and `aria-expanded`. */
    popovertarget?: string;
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
    download,
    label,
    popovertarget,
    onclick
  }: Props = $props();

  // `aria-disabled`, not `disabled`: a working button stays readable and in the tab order, so focus doesn't jump.
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
    {download}
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
    {popovertarget}
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
    position: relative;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    gap: var(--space-2);
    border: 1px solid transparent;
    border-radius: var(--radius-md);
    font: inherit;
    font-weight: var(--weight-medium);
    text-decoration: none;
    min-width: 0;
    max-width: 100%;
    padding-block: var(--space-2);
    white-space: normal;
    /* Break a word only when it alone is wider than the button; the inherited 'anywhere'
       would shrink its minimum size ("Ersetze" over "n"). */
    overflow-wrap: break-word;
    text-align: center;
    letter-spacing: -0.01em;
    cursor: pointer;
    transition:
      background-color var(--duration-fast) var(--ease-out),
      border-color var(--duration-fast) var(--ease-out),
      color var(--duration-fast) var(--ease-out),
      box-shadow var(--duration-fast) var(--ease-out),
      transform var(--duration-fast) var(--ease-out);
  }

  .sm {
    min-height: var(--control-sm);
    padding-inline: var(--space-3);
    font-size: var(--text-sm);
  }

  .md {
    min-height: var(--control-md);
    padding-inline: var(--space-6);
  }

  .lg {
    min-height: var(--control-lg);
    padding-inline: var(--space-6);
    font-size: var(--text-lg);
  }

  .full {
    display: flex;
    width: 100%;
  }

  .primary,
  .danger {
    box-shadow: var(--shadow-card);
  }

  .primary {
    background: var(--accent);
    color: var(--accent-contrast);
  }

  .primary:hover:not([aria-disabled='true']) {
    background: var(--accent-hover);
  }

  .secondary {
    border-color: var(--border-strong);
    background: var(--surface-raised);
    color: var(--text);
  }

  .secondary:hover:not([aria-disabled='true']) {
    background: var(--surface-hover);
  }

  .ghost {
    background: transparent;
    color: var(--text-muted);
  }

  .ghost:hover:not([aria-disabled='true']) {
    background: var(--surface-hover);
    color: var(--text);
  }

  .danger {
    background: var(--danger);
    color: var(--accent-contrast);
  }

  .danger:hover:not([aria-disabled='true']) {
    background: var(--danger-hover);
  }

  /* On a photograph: dark glass and white text in either mode; blur separates the label from a busy picture. */
  .media {
    border-color: var(--border-on-media);
    background: var(--control-on-media);
    color: var(--text-on-media);
    backdrop-filter: blur(12px) saturate(140%);
  }

  .media:hover:not([aria-disabled='true']) {
    background: var(--control-on-media-hover);
  }

  .media:active:not([aria-disabled='true']) {
    background: var(--control-on-media-hover);
  }

  .button[aria-disabled='true'],
  .button:disabled {
    cursor: not-allowed;
    background: var(--surface-sunken);
    color: var(--text-muted);
    border-color: var(--border);
    box-shadow: none;
  }

  /* Must follow the rule above: page-coloured disabled surfaces read as a solid tile on a
     picture, so fade the glass instead. */
  .media[aria-disabled='true'],
  .media:disabled {
    background: var(--control-on-media);
    color: var(--text-on-media);
    border-color: var(--border-on-media);
    opacity: 0.45;
  }

  .label {
    min-width: 0;
  }

  .icon {
    flex-shrink: 0;
    display: inline-flex;
    width: var(--space-4);
    height: var(--space-4);
  }

  /* Keeps layout and accessible name while the spinner replaces the label visually. */
  .loading .label,
  .loading .icon {
    opacity: 0;
  }

  .button:active:not([aria-disabled='true']) {
    box-shadow: none;
    transform: scale(0.985);
  }
  .primary:active:not([aria-disabled='true']) {
    background: var(--accent-active);
  }
  .secondary:active:not([aria-disabled='true']),
  .ghost:active:not([aria-disabled='true']) {
    background: var(--surface-selected);
  }
  .icon :global(svg) {
    display: block;
    width: 100%;
    height: 100%;
  }

  .spinner {
    position: absolute;
    inset: 0;
    margin: auto;
    flex-shrink: 0;
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

  /* Without motion the ring would be a broken circle: show a steady dot. */
  @media (prefers-reduced-motion: reduce) {
    .spinner {
      border-top-color: currentcolor;
      opacity: 0.5;
    }
  }
</style>
