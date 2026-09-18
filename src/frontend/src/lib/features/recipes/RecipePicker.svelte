<script lang="ts">
  import type { Snippet } from 'svelte';

  import { SearchField, Sheet } from '$ds';
  import { m } from '$shell/i18n';

  import { metaLineFor } from './recipeMeta';
  import { createRecipeStore } from './stores/recipes.svelte';
  import { suggestions } from './stores/suggestions.svelte';
  import type { RecipeSummary } from './types';

  /**
   * Choosing one recipe out of all of them.
   *
   * The week plan and the shopping list both have to ask this question, and a
   * picker that existed twice would be two search boxes that debounce
   * differently and two ideas of what "nothing matched" looks like. What each
   * caller does with the recipe is its own business; finding it is not.
   *
   * The title is the caller's, because the question is not the same one —
   * "What are you cooking?" on a Tuesday, "Which recipe?" while writing a
   * shopping list — and a shared component that names the task for everybody
   * names it wrongly for somebody.
   */
  interface Props {
    open: boolean;
    /** Whose recipes are searched. */
    householdId: string;
    title: string;
    /** Anything else the caller needs answered, shown above the results. */
    controls?: Snippet;
    /** Actions that end the picking, kept below the results. */
    footer?: Snippet;
    /**
     * Recipes this caller has already taken, by id.
     *
     * Only meaningful to a caller that keeps the sheet open across several
     * picks: after four searches nobody remembers whether the risotto went on,
     * and the rows are the only place that answer can be read.
     */
    taken?: readonly string[];
    /** Search only inside one cookbook, when the caller is looking in one. */
    cookbookId?: string;
    /**
     * Which meal this is being picked for, when the caller knows.
     *
     * The week planner always does — the day and the slot are both chosen
     * before the sheet opens — and that is the richest context the product ever
     * has. With it, the blank state of this sheet stops being "everything,
     * newest first" and becomes an answer, which is often enough that typing is
     * optional. Without it the sheet behaves exactly as it always did.
     */
    suggestFor?: 'breakfast' | 'lunch' | 'dinner';
    onpick: (recipe: RecipeSummary) => void;
    onclose: () => void;
  }

  let {
    open,
    householdId,
    title,
    controls,
    footer,
    taken = [],
    cookbookId,
    suggestFor,
    onpick,
    onclose
  }: Props = $props();

  /**
   * This sheet's own list, not the app's.
   *
   * The shared store is what the page behind the sheet is drawing from, and a
   * picker that searched into it would replace that page's contents with
   * whatever was typed here — the cookbook page would empty out behind an open
   * sheet, and the collection would still be filtered after the sheet closed.
   */
  const recipes = createRecipeStore();

  /** What is in the box, which is not yet what has been searched for. */
  let typed = $state('');
  let query = $state('');

  let debounce: ReturnType<typeof setTimeout> | undefined;

  const id = $props.id();

  /**
   * Whether the sheet is answering rather than searching.
   *
   * Only before anything is typed, and only inside the whole collection: a
   * suggestion ranks the library, and a cookbook is somebody's curation of it
   * whose own order is the one they built.
   */
  const suggesting = $derived(
    suggestFor !== undefined && query.trim().length === 0 && cookbookId === undefined
  );

  /** What is already on this week, so nothing is offered twice. */
  const occasion = $derived({ slot: suggestFor, exclude: taken, limit: 5 });

  const shown = $derived(suggesting ? suggestions.for(householdId, occasion) : recipes.items);

  // Only while it is open: a closed sheet that keeps a search warm is a request
  // nobody asked for, on every page that happens to mount one.
  $effect(() => {
    if (open && !suggesting) {
      void recipes.list(householdId, { query, cookbookId });
    }
  });

  $effect(() => {
    if (open && suggesting) {
      void suggestions.ask(householdId, occasion);
    }
  });

  // Forgotten on the way out, so it opens on everything next time rather than
  // on whatever somebody was looking for last week.
  $effect(() => {
    if (!open) {
      clearTimeout(debounce);
      typed = '';
      query = '';
    }
  });

  $effect(() => () => clearTimeout(debounce));

  function type(value: string) {
    typed = value;
    clearTimeout(debounce);

    // Long enough that a word is finished, short enough that it feels live.
    debounce = setTimeout(() => (query = value), 250);
  }

  /**
   * Whether to say that nothing matched.
   *
   * Only once an answer has arrived. While a search is in flight the previous
   * results are still on screen — the store keeps them deliberately — and
   * flashing "nothing matched" between two keystrokes says the opposite of
   * what is true.
   */
  const nothing = $derived(
    suggesting
      ? suggestions.statusOf(householdId, occasion) === 'ready' && shown.length === 0
      : recipes.status === 'ready' && recipes.items.length === 0
  );
</script>

<Sheet {open} {title} {footer} closeLabel={m['picker.close']()} {onclose}>
  <div class="picker">
    <SearchField
      id="{id}-search"
      label={m['picker.search']()}
      clearLabel={m['picker.clear']()}
      placeholder={m['picker.search']()}
      value={typed}
      oninput={type}
    />

    {#if controls}
      {@render controls()}
    {/if}

    {#if nothing}
      <p class="nothing">{m['picker.nothing']()}</p>
    {:else}
      <ul class="results">
        {#each shown as recipe (recipe.id)}
          <li>
            <button type="button" onclick={() => onpick(recipe)}>
              <span class="title">{recipe.title}</span>
              <!-- Taken already, but still a button: a household that cooks the
                   same thing twice in a week wants the amounts twice, and the
                   server merges them. -->
              {#if taken.includes(recipe.id)}
                <span class="meta taken">{m['picker.taken']()}</span>
              {:else}
                <!-- The same line the card and the surface show, so a recipe
                     describes itself identically wherever it is met. -->
                <span class="meta">{metaLineFor(recipe)}</span>
              {/if}
            </button>
          </li>
        {/each}
      </ul>
    {/if}
  </div>
</Sheet>

<style>
  .picker {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .results {
    display: flex;
    flex-direction: column;
    margin: 0;
    padding: 0;
    list-style: none;
    max-height: 50dvh;
    overflow-y: auto;
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

  .nothing {
    color: var(--text-muted);
    padding: var(--space-3);
  }
</style>
