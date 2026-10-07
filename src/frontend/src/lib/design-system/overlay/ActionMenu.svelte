<script lang="ts">
  import type { Snippet } from 'svelte';

  import Popover from './Popover.svelte';

  /**
   * A menu of choices opened from a control: a `Popover` that closes itself
   * when one of its choices is pressed.
   *
   * The browser closes a popover when a click lands outside it and not when it
   * lands on one of the choices, which is right for a panel of checkboxes and
   * wrong for a menu: every choice here opens a sheet, moves something or
   * leaves the page, and a menu still hanging over the result is a menu nobody
   * dismissed. So the menu closes first, and the choice's own handler runs
   * after.
   *
   * The rows are written by the caller as plain `<button>`s and `<a>`s, and are
   * drawn here: `.item` for a row, `.item.danger` for the destructive one,
   * `.mark` for the fixed-width cell before a label that holds a tick or an
   * icon, `.heading` for a line of context above the rows and `.separator`
   * (an `<hr>`) between groups. Only a press on a button or a link closes it,
   * so a heading can be read and selected.
   */
  interface Props {
    /** The control that opens it. Receives the attributes that pair the two. */
    trigger: Snippet<[{ popovertarget: string }]>;
    children: Snippet;
    /** The least width the rows are given, as a CSS length. */
    minWidth?: string;
    /** The most width the rows are given, as a CSS length. */
    maxWidth?: string;
    /** Whether a long label wraps. Off, a row is as wide as its words. */
    wrap?: boolean;
  }

  let { trigger, children, minWidth = '12rem', maxWidth, wrap = false }: Props = $props();

  /** Hides the popover the pressed choice is in, before its own handler runs. */
  function closeOnChoice(event: MouseEvent) {
    const menu = event.currentTarget as HTMLElement;
    const choice = event.target instanceof Element ? event.target.closest('button, a') : null;

    if (!choice || !menu.contains(choice)) {
      return;
    }

    const panel = menu.closest('[popover]');

    if (panel instanceof HTMLElement && typeof panel.hidePopover === 'function') {
      panel.hidePopover();
    }
  }
</script>

<Popover placement="bottom-end" {trigger}>
  <!-- Not a control itself: it only watches presses on the real buttons and
       links inside it, in the capture phase so that it runs before them. -->
  <div
    class="menu"
    class:wrap
    style:min-width={minWidth}
    style:max-width={maxWidth}
    onclickcapture={closeOnChoice}
  >
    {@render children()}
  </div>
</Popover>

<style>
  .menu {
    display: flex;
    flex-direction: column;
    /* Not struck through with a line it is in: it is a menu, not part of the
       thing it belongs to. */
    text-decoration: none;
  }

  /*
   * The rows are the caller's markup, so they are reached with `:global`; the
   * `.menu` in front keeps every rule here from touching anything else.
   *
   * The full width each, so the icons line up down the left and the words down
   * beside them — which is what makes a list of three things scannable rather
   * than three separate controls that happen to be in one box.
   */
  .menu :global(.item) {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    min-height: var(--control-sm);
    padding: var(--space-2) var(--space-3);
    border: none;
    border-radius: var(--radius-md);
    background: none;
    color: var(--text);
    font: inherit;
    font-size: var(--text-sm);
    text-align: start;
    text-decoration: none;
    white-space: nowrap;
    cursor: pointer;
  }

  .menu.wrap :global(.item) {
    white-space: normal;
  }

  .menu :global(.item:hover) {
    background: var(--surface-hover);
  }

  .menu :global(.item svg) {
    flex: none;
    width: var(--space-4);
    height: var(--space-4);
  }

  .menu :global(.item.danger) {
    color: var(--text-danger);
  }

  .menu :global(.mark) {
    display: grid;
    flex: none;
    place-items: center;
    width: var(--space-4);
    height: var(--space-4);
    color: var(--accent);
  }

  .menu :global(.mark svg) {
    width: 100%;
    height: 100%;
  }

  .menu :global(.heading) {
    padding: var(--space-2) var(--space-3) var(--space-1);
    color: var(--text-muted);
    font-size: var(--text-xs);
  }

  .menu :global(.separator) {
    margin: var(--space-1) var(--space-3);
    border: none;
    border-top: 1px solid var(--border);
  }
</style>
