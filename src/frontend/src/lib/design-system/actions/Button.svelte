<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * The one action element.
   *
   * A `<button>` when it does something and an `<a>` when it goes somewhere —
   * never a styled `div`, because a div cannot be reached by keyboard, cannot
   * be activated by space, and tells a screen reader nothing.
   */
  /**
   * `media` is the one that is not about emphasis: it is where the button is.
   * A control lying on a photograph cannot take its colour from the page,
   * because the page is not what is behind it.
   */
  export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger' | 'media';
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
    /**
     * Saves what the link points at instead of navigating to it.
     *
     * Only meaningful with `href`, and only for something the browser would
     * otherwise try to display. The browser's own download is what gives a file
     * a name and a progress indication; a blob assembled in memory has neither.
     */
    download?: string;
    /** Announced in place of the label when the label is only an icon. */
    label?: string;
    /**
     * The popover this button opens, from `Popover`'s trigger snippet.
     *
     * Explicit rather than spread through, because this is the whole of what a
     * button needs to drive a popover: the browser handles the toggling, the
     * light dismiss and the `aria-expanded` from this one attribute.
     */
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
    text-align: center;
    letter-spacing: -0.01em;
    cursor: pointer;
    transition:
      background-color var(--duration-fast) var(--ease-out),
      border-color var(--duration-fast) var(--ease-out),
      color var(--duration-fast) var(--ease-out),
      box-shadow var(--duration-fast) var(--ease-out);
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

  /*
   * On a photograph.
   *
   * Dark glass and white text whichever mode the app is in, because what is
   * behind it is a picture rather than a surface. Blurred where the browser
   * will: it separates the label from a busy photograph without another
   * shadow, and the translucency is what keeps it from looking like a sticker.
   */
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

  /* After the rule above, which it has to beat: the app's disabled surfaces
     are page colours, and on a picture they read as a solid tile. Fading the
     glass says the same thing and stays glass. */
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

  /* Preserve the label’s layout and accessible name while progress replaces it visually. */
  .loading .label,
  .loading .icon {
    opacity: 0;
  }

  .button:active:not([aria-disabled='true']) {
    box-shadow: none;
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

  /* Without motion the ring would sit there as a broken circle, so it becomes a
     steady dot instead — present, but not pretending to move. */
  @media (prefers-reduced-motion: reduce) {
    .spinner {
      border-top-color: currentcolor;
      opacity: 0.5;
    }
  }
</style>
