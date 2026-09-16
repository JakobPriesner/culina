<script lang="ts">
  import type { Snippet } from 'svelte';

  interface Props {
    children: Snippet;
    icon?: Snippet;
    selected: boolean;
    shape?: 'pill' | 'rounded';
    disabled?: boolean;
    onclick: () => void;
  }

  let { children, icon, selected, shape = 'pill', disabled = false, onclick }: Props = $props();
</script>

<button
  class="chip {shape}"
  class:selected
  type="button"
  aria-pressed={selected}
  {disabled}
  {onclick}
>
  {#if selected}
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="1.8"
      aria-hidden="true"
    >
      <path d="m5 12 4 4L19 6" stroke-linecap="round" stroke-linejoin="round" />
    </svg>
  {:else if icon}
    <span class="icon" aria-hidden="true">{@render icon()}</span>
  {/if}
  <span>{@render children()}</span>
</button>

<style>
  .chip {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    gap: var(--space-2);
    max-width: 100%;
    min-height: var(--control-sm);
    padding: var(--space-2) var(--space-4);
    border: 1px solid var(--border-strong);
    border-radius: var(--radius-full);
    background: var(--surface-raised);
    color: var(--text-muted);
    font: inherit;
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    cursor: pointer;
    transition:
      background-color var(--duration-fast) var(--ease-out),
      border-color var(--duration-fast) var(--ease-out);
  }
  .chip.rounded {
    border-radius: var(--radius-md);
    min-height: var(--control-md);
  }
  .chip:hover:not(:disabled) {
    background: var(--surface-hover);
    color: var(--text);
  }
  .chip.selected {
    background: var(--surface-accent-subtle);
    border-color: var(--accent);
    color: var(--accent);
  }
  .chip:disabled {
    cursor: not-allowed;
  }
  .icon,
  svg {
    display: block;
    flex-shrink: 0;
    width: var(--space-4);
    height: var(--space-4);
  }
  .icon :global(svg) {
    display: block;
    width: 100%;
    height: 100%;
  }
</style>
