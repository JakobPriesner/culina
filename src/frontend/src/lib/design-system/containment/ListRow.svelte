<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * One row of a `ListSection`: an optional control at its start (a check, a circle), what it is, and a value at its
   * end in one right-aligned column of figures. With `onclick` the row itself opens what it is; the leading control
   * stays a control of its own. `tone="action"` is the section's own verb, its last row ("＋ Eintragen").
   */
  interface Props {
    children: Snippet;
    leading?: Snippet;
    trailing?: Snippet;
    onclick?: () => void;
    tone?: 'plain' | 'action';
    /** A name for the row's button when its text alone does not say what pressing it does. */
    label?: string;
  }

  let { children, leading, trailing, onclick, tone = 'plain', label }: Props = $props();
</script>

{#snippet body()}
  <span class="main">{@render children()}</span>
  {#if trailing}<span class="trailing">{@render trailing()}</span>{/if}
{/snippet}

<li class="row {tone}" class:led={leading !== undefined}>
  {#if leading}<span class="leading">{@render leading()}</span>{/if}
  {#if onclick}
    <button class="body" type="button" aria-label={label} {onclick}>{@render body()}</button>
  {:else}
    <div class="body">{@render body()}</div>
  {/if}
</li>

<style>
  .row {
    display: flex;
    align-items: center;
  }

  .row.led {
    padding-inline-start: var(--space-2);
  }

  .leading {
    display: flex;
    align-self: stretch;
    align-items: center;
  }

  /* Edge to edge, so the whole row is the target and lights up as one; a row after a leading control starts after it. */
  .body {
    display: flex;
    flex: 1;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-4);
    min-inline-size: 0;
    min-block-size: var(--control-lg);
    padding: var(--space-2) var(--space-4);
    border: 0;
    background-color: transparent;
    color: var(--text);
    font: inherit;
    text-align: start;
  }

  .led .body {
    padding-inline-start: 0;
  }

  /*
   * The hairline starts where the text starts, as grouped lists draw it: drawn as an image, so the row's light on
   * hover (its background colour) still fills it edge to edge.
   */
  .row:not(:first-child) .body {
    background-image: linear-gradient(var(--border), var(--border));
    background-position: right top;
    background-repeat: no-repeat;
    background-size: calc(100% - var(--space-4)) 1px;
  }

  .row.led:not(:first-child) .body {
    background-size: 100% 1px;
  }

  button.body {
    cursor: pointer;
    transition: background-color var(--duration-fast) var(--ease-out);
  }

  button.body:focus-visible {
    outline: 2px solid var(--border-focus);
    outline-offset: -2px;
  }

  @media (hover: hover) {
    button.body:hover {
      background-color: var(--surface-hover);
    }
  }

  button.body:active {
    background-color: var(--surface-selected);
  }

  /* Long compounds hyphenate by the page's language; only a word wider than the row breaks anywhere else. */
  .main {
    display: flex;
    flex-direction: column;
    min-inline-size: 0;
    hyphens: auto;
    overflow-wrap: break-word;
  }

  .trailing {
    flex-shrink: 0;
    color: var(--text-muted);
    font-variant-numeric: tabular-nums;
    text-align: end;
  }

  .action .body {
    color: var(--accent);
    font-weight: var(--weight-medium);
  }
</style>
