<script lang="ts">
  import type { Snippet } from 'svelte';

  import Popover from './Popover.svelte';

  /**
   * A `Popover` that closes itself when a choice is pressed, before the choice's handler runs.
   * Callers supply `<button>`/`<a>` rows styled via `.item`, `.item.danger`, `.mark`, `.heading`, `.separator`.
   */
  interface Props {
    /** The control that opens it; receives the attributes that pair the two. */
    trigger: Snippet<[{ popovertarget: string }]>;
    children: Snippet;
    minWidth?: string;
    maxWidth?: string;
    wrap?: boolean;
  }

  let { trigger, children, minWidth = '12rem', maxWidth, wrap = false }: Props = $props();

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
  <!-- Not a control: it watches presses on inner buttons/links in the capture phase so it runs first. -->
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
    /* Do not inherit a surrounding line-through. */
    text-decoration: none;
  }

  /* The rows are the caller's markup, hence `:global`; the `.menu` prefix scopes the rules. */
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
