<script lang="ts">
  import type { Snippet } from 'svelte';
  import { selectionIndicator } from './selectionIndicator';

  /**
   * Two or three views of the same thing.
   *
   * Follows the tab pattern properly: arrows move between tabs, Home and End
   * jump to the ends, and only the selected tab is in the tab order — so
   * tabbing out of the strip lands in the panel rather than walking through
   * every tab. A row of buttons looks the same and behaves nothing like this.
   */
  export interface Tab {
    readonly id: string;
    readonly label: string;
  }

  interface Props {
    tabs: readonly Tab[];
    selected: string;
    /** Rendered for the selected tab only. */
    children: Snippet<[string]>;
    /** Names the strip, since the tabs alone do not say what they switch. */
    label: string;
    onselect?: (id: string) => void;
  }

  let { tabs, selected = $bindable(), children, label, onselect }: Props = $props();

  const id = $props.id();

  let strip = $state<HTMLDivElement>();

  const tabId = (tab: string) => `${id}-tab-${tab}`;
  const panelId = (tab: string) => `${id}-panel-${tab}`;

  function select(next: string) {
    selected = next;
    onselect?.(next);
  }

  function move(event: KeyboardEvent) {
    const offsets: Record<string, number> = { ArrowRight: 1, ArrowLeft: -1 };
    const index = tabs.findIndex((tab) => tab.id === selected);

    let target: number | undefined;

    if (event.key in offsets) {
      // Wraps, because reaching the end of three tabs and stopping is a
      // dead end nobody expects.
      target = (index + offsets[event.key]! + tabs.length) % tabs.length;
    } else if (event.key === 'Home') {
      target = 0;
    } else if (event.key === 'End') {
      target = tabs.length - 1;
    }

    if (target === undefined) {
      return;
    }

    event.preventDefault();

    const next = tabs[target]!;

    select(next.id);
    strip?.querySelector<HTMLElement>(`#${CSS.escape(tabId(next.id))}`)?.focus();
  }
</script>

<div class="tabs">
  <div
    bind:this={strip}
    use:selectionIndicator={{ selected, underline: true }}
    class="strip"
    role="tablist"
    aria-label={label}
  >
    {#each tabs as tab (tab.id)}
      <button
        id={tabId(tab.id)}
        class="tab"
        data-selection={tab.id}
        type="button"
        role="tab"
        aria-selected={selected === tab.id}
        aria-controls={panelId(tab.id)}
        tabindex={selected === tab.id ? 0 : -1}
        onclick={() => select(tab.id)}
        onkeydown={move}
      >
        {tab.label}
      </button>
    {/each}
  </div>

  {#key selected}
    <div id={panelId(selected)} class="panel" role="tabpanel" aria-labelledby={tabId(selected)}>
      {@render children(selected)}
    </div>
  {/key}
</div>

<style>
  .strip {
    position: relative;
    display: flex;
    gap: var(--space-1);
    border-bottom: 1px solid var(--border);
  }

  .tab {
    position: relative;
    z-index: 1;
    padding: var(--space-3) var(--space-4);
    min-height: var(--control-sm);
    border: none;
    border-bottom: 2px solid transparent;
    background: transparent;
    color: var(--text-muted);
    font: inherit;
    font-weight: var(--weight-medium);
    cursor: pointer;
    /* Only the marker moves; labels and keyboard focus stay in place. */
    transition:
      color var(--duration-fast) var(--ease-out),
      border-color var(--duration-fast) var(--ease-out);
  }

  .tab:hover {
    color: var(--text);
  }

  .tab[aria-selected='true'] {
    color: var(--text);
  }

  .strip:not([data-indicator-ready]) .tab[aria-selected='true'] {
    border-bottom-color: var(--accent);
  }

  @keyframes panel-in {
    from {
      opacity: 0;
      transform: translateY(var(--space-1));
    }
  }

  .panel {
    padding-block: var(--space-4);
    animation: panel-in var(--duration-base) var(--ease-out);
  }
  @media (prefers-reduced-motion: reduce) {
    .panel {
      animation: none;
    }
  }
</style>
