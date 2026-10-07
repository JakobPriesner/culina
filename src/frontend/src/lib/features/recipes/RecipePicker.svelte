<script lang="ts">
  import type { Snippet } from 'svelte';

  import { SearchField, Sheet } from '$ds';
  import { m } from '$shell/i18n';

  import PickerResults from './PickerResults.svelte';
  import { createPickerSearch } from './recipePickerSearch.svelte';
  import SearchChips from './search/SearchChips.svelte';
  import SearchNotice from './search/SearchNotice.svelte';
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
    /**
     * Takes a taken recipe back, when the caller can.
     *
     * With it, every row is a toggle that says whether it is taken before
     * anybody touches it — a cookbook, where a recipe is on the shelf or not,
     * and picking it again could only look like an add that failed. Without it,
     * a taken row is still an ordinary pick: a household that cooks the same
     * thing twice in a week wants it planned twice.
     */
    onremove?: (recipe: RecipeSummary) => void;
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
    onremove,
    onclose
  }: Props = $props();

  const takenIds = $derived(new Set(taken));

  const id = $props.id();

  const search = createPickerSearch({
    open: () => open,
    householdId: () => householdId,
    cookbookId: () => cookbookId,
    suggestFor: () => suggestFor,
    taken: () => taken
  });
</script>

<Sheet {open} {title} {footer} closeLabel={m['picker.close']()} {onclose}>
  <div class="picker" data-fills-dialog>
    <SearchField
      id="{id}-search"
      label={m['picker.search']()}
      clearLabel={m['picker.clear']()}
      placeholder={m['picker.search']()}
      value={search.typed}
      oninput={search.type}
    />

    {#if search.interpretation}
      <SearchChips chips={search.interpretation.chips} onremove={search.remove} />
      <SearchNotice
        interpretation={search.interpretation}
        total={search.total}
        query={search.query}
        offer={false}
        onastyped={search.keepAsTyped}
        onremove={search.remove}
      />
    {/if}

    {#if controls}
      {@render controls()}
    {/if}

    {#if search.nothing}
      <p class="nothing">{m['picker.nothing']()}</p>
    {:else}
      <PickerResults recipes={search.shown} taken={takenIds} {onpick} {onremove} />
    {/if}
  </div>
</Sheet>

<style>
  .picker {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .nothing {
    color: var(--text-muted);
    padding: var(--space-3);
  }
</style>
