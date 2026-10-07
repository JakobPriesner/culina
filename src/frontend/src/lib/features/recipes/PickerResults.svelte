<script lang="ts">
  import { m } from '$shell/i18n';

  import { metaLineFor } from './recipeMeta';
  import type { RecipeSummary } from './types';

  interface Props {
    recipes: readonly RecipeSummary[];
    taken: ReadonlySet<string>;
    onpick: (recipe: RecipeSummary) => void;
    /** With it, every row is a toggle. */
    onremove?: (recipe: RecipeSummary) => void;
  }

  let { recipes, taken, onpick, onremove }: Props = $props();
</script>

<ul class="results">
  {#each recipes as recipe (recipe.id)}
    {@const isTaken = taken.has(recipe.id)}
    <li>
      <button
        type="button"
        class:toggle={onremove !== undefined}
        aria-pressed={onremove ? isTaken : undefined}
        onclick={() => (onremove && isTaken ? onremove(recipe) : onpick(recipe))}
      >
        {#if onremove}
          <span class="tick" class:on={isTaken} aria-hidden="true">
            {#if isTaken}
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3">
                <path d="m5 12 5 5 9-10" stroke-linecap="round" stroke-linejoin="round" />
              </svg>
            {/if}
          </span>
        {/if}
        <span class="title">{recipe.title}</span>
        {#if isTaken}
          <span class="meta taken">{m['picker.taken']()}</span>
        {:else}
          <span class="meta">{metaLineFor(recipe)}</span>
        {/if}
      </button>
    </li>
  {/each}
</ul>

<style>
  /* Only the list scrolls so the search field stays put; `data-fills-dialog` on the picker makes the sheet hand over its height instead of adding a second bar. */
  .results {
    display: flex;
    flex-direction: column;
    min-height: 0;
    flex: 1;
    margin: 0;
    /* `scrollbar-gutter` reserves the bar width where bars take layout space (and stops row shift); the padding does the same for overlay bars, which take none. */
    padding: 0 var(--space-2) 0 0;
    scrollbar-gutter: stable;
    list-style: none;
    overflow-y: auto;
    overscroll-behavior: contain;
  }

  .results button {
    display: flex;
    align-items: baseline;
    flex-wrap: wrap;
    min-height: var(--control-sm);
    justify-content: space-between;
    gap: var(--space-4);
    width: 100%;
    padding: var(--space-3);
    border: 0;
    border-radius: var(--radius-sm);
    background: none;
    color: inherit;
    font: inherit;
    text-align: start;
    cursor: pointer;
  }

  .results button:hover {
    background: var(--surface-hover);
  }

  .meta {
    color: var(--text-muted);
    font-size: var(--text-sm);
    white-space: nowrap;
  }

  .taken {
    color: var(--text-success);
    font-weight: var(--weight-semibold);
  }

  /* The leading mark lets a column of toggles be scanned for what is already taken. */
  .results .toggle {
    align-items: center;
    justify-content: flex-start;
    gap: var(--space-3);
  }

  .toggle .title {
    flex: 1 1 auto;
  }

  .tick {
    display: inline-grid;
    flex: 0 0 auto;
    place-items: center;
    width: 1.25rem;
    height: 1.25rem;
    border: 2px solid var(--border-strong);
    border-radius: var(--radius-full);
  }

  .tick.on {
    border-color: var(--accent);
    background: var(--accent);
    color: var(--text-on-accent);
  }

  .tick svg {
    width: 0.8rem;
    height: 0.8rem;
  }
</style>
