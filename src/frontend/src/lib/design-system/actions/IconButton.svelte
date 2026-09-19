<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * An action with no visible label.
   *
   * `label` is required, not optional: an icon-only control without an
   * accessible name is a button that a screen reader announces as "button", and
   * there is no way for the reader to find out what it does.
   */
  interface Props {
    /** The icon. Always decorative — `label` is what is announced. */
    children: Snippet;
    label: string;
    size?: 'sm' | 'md' | 'lg';
    /** Draws the control's edge. Off by default: most sit inside a toolbar. */
    bordered?: boolean;
    disabled?: boolean;
    /** Marks a toggle's state, so it is announced as pressed rather than as new. */
    pressed?: boolean;
    type?: 'button' | 'submit';
    /**
     * The popover this button opens, from `Popover`'s trigger snippet.
     *
     * The same one attribute `Button` takes, and for the same reason: the
     * browser handles the toggling, the light dismiss and the `aria-expanded`
     * from it. An overflow menu is an icon, so it needs this end too.
     */
    popovertarget?: string;
    onclick?: (event: MouseEvent) => void;
  }

  let {
    children,
    label,
    size = 'md',
    bordered = false,
    disabled = false,
    pressed,
    type = 'button',
    popovertarget,
    onclick
  }: Props = $props();
</script>

<button
  class="icon-button {size}"
  class:bordered
  {type}
  {disabled}
  {popovertarget}
  aria-label={label}
  aria-pressed={pressed === undefined ? undefined : pressed}
  title={label}
  {onclick}
>
  <span class="icon" aria-hidden="true">{@render children()}</span>
</button>

<style>
  .icon-button {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    padding: 0;
    border: 1px solid transparent;
    border-radius: var(--radius-full);
    background: transparent;
    color: var(--text-muted);
    cursor: pointer;
    transition:
      background-color var(--duration-fast) var(--ease-out),
      color var(--duration-fast) var(--ease-out);
  }

  .icon-button:hover:not(:disabled) {
    background: var(--surface-hover);
    color: var(--text);
  }

  .icon-button[aria-pressed='true'] {
    background: var(--surface-selected);
    color: var(--text);
  }

  .icon-button:disabled {
    cursor: not-allowed;
    opacity: 0.55;
  }

  .bordered {
    border-color: var(--border-strong);
    background: var(--surface-raised);
  }

  /* Both sizes clear 44px: the icon shrinks, the target does not. */
  .sm {
    width: var(--control-sm);
    height: var(--control-sm);
  }

  .sm .icon {
    width: var(--space-4);
    height: var(--space-4);
  }

  .md {
    width: var(--control-md);
    height: var(--control-md);
  }

  .md .icon {
    width: var(--space-6);
    height: var(--space-6);
  }

  /* For the one place a hand is wet and the eye is on a pan. */
  .lg {
    width: var(--control-lg);
    height: var(--control-lg);
  }

  .lg .icon {
    width: var(--space-6);
    height: var(--space-6);
  }

  .icon {
    display: block;
  }

  .icon :global(svg) {
    display: block;
    width: 100%;
    height: 100%;
  }
</style>
