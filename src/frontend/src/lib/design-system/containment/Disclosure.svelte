<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * Something that is usually not needed, available when it is.
   *
   * Native `<details>`: it is open to the browser's find-in-page, it works
   * before any script has run, and it is one element instead of a button, a
   * region, `aria-expanded`, `aria-controls` and a keyboard handler.
   */
  interface Props {
    summary: string;
    children: Snippet;
    open?: boolean;
    ontoggle?: (open: boolean) => void;
  }

  let { summary, children, open = $bindable(false), ontoggle }: Props = $props();
</script>

<details class="disclosure" bind:open ontoggle={(event) => ontoggle?.(event.currentTarget.open)}>
  <summary class="summary">
    <span class="chevron" aria-hidden="true">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <path d="m9 6 6 6-6 6" stroke-linecap="round" stroke-linejoin="round" />
      </svg>
    </span>
    {summary}
  </summary>

  <div class="content">{@render children()}</div>
</details>

<style>
  .summary {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    min-height: var(--control-sm);
    font-weight: var(--weight-medium);
    cursor: pointer;
    list-style: none;
  }

  /* Replaced by the chevron, which points the way it will move. */
  .summary::-webkit-details-marker {
    display: none;
  }

  .chevron {
    display: block;
    width: var(--space-4);
    height: var(--space-4);
    color: var(--text-subtle);
    transition: transform var(--duration-fast) var(--ease-out);
  }

  .chevron :global(svg) {
    display: block;
    width: 100%;
    height: 100%;
  }

  .disclosure[open] .chevron {
    transform: rotate(90deg);
  }

  .content {
    padding-block: var(--space-2);
  }
</style>
