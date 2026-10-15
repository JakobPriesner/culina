<script lang="ts">
  import { Image } from '$ds';
  import { m } from '$shell/i18n';

  import { imageUrl } from './recipeImage';
  import { metaLineFor } from './recipeMeta';
  import type { RecipeSummary } from './types';

  interface Props {
    recipes: readonly RecipeSummary[];
    taken: ReadonlySet<string>;
    /** What to say of a taken recipe in place of "Added", by id: when a pick is not the receipt of what was just done. */
    notes?: ReadonlyMap<string, string>;
    onpick: (recipe: RecipeSummary) => void;
    /** With it, every row is a toggle. */
    onremove?: (recipe: RecipeSummary) => void;
  }

  let { recipes, taken, notes, onpick, onremove }: Props = $props();
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
        <!-- A photo finds a dish faster than its name; the row keeps one shape, its facts always under the title. -->
        <span class="thumb">
          <Image
            src={recipe.imageId ? imageUrl(recipe.id, 400, recipe.imageId) : undefined}
            alt=""
            ratio={1}
            loading="lazy"
          />
        </span>
        <span class="words">
          <span class="title">{recipe.title}</span>
          {#if isTaken && notes?.has(recipe.id)}
            <span class="meta note">{notes.get(recipe.id)}</span>
          {:else if isTaken}
            <span class="meta taken">{m['picker.taken']()}</span>
          {:else if metaLineFor(recipe)}
            <span class="meta">{metaLineFor(recipe)}</span>
          {/if}
        </span>
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
    align-items: center;
    min-height: var(--control-sm);
    gap: var(--space-3);
    width: 100%;
    padding: var(--space-2) var(--space-3);
    border: 0;
    border-radius: var(--radius-sm);
    background: none;
    color: inherit;
    font: inherit;
    text-align: start;
    cursor: pointer;
  }

  @media (hover: hover) {
    .results button:hover {
      background: var(--surface-hover);
    }
  }

  .thumb {
    flex: 0 0 2.75rem;
  }

  .words {
    display: flex;
    flex: 1 1 auto;
    flex-direction: column;
    min-width: 0;
  }

  .meta {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .note {
    font-weight: var(--weight-medium);
    white-space: normal;
  }

  .taken {
    color: var(--text-success);
    font-weight: var(--weight-semibold);
  }

  /* The leading mark lets a column of toggles be scanned for what is already taken. */

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
