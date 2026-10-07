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
   * Chooses one recipe; shared by the week plan and the shopping list so search behaves alike.
   * The title is the caller's because the question differs.
   */
  interface Props {
    open: boolean;
    householdId: string;
    title: string;
    /** Anything else the caller needs answered, shown above the results. */
    controls?: Snippet;
    /** Actions that end the picking, kept below the results. */
    footer?: Snippet;
    /** Recipes the caller already took, by id; for a sheet kept open across picks, where rows are the only place to see it. */
    taken?: readonly string[];
    /** Search only inside one cookbook, when the caller is looking in one. */
    cookbookId?: string;
    /** The meal being picked for, when known: turns the blank state into suggestions instead of "everything, newest first". */
    suggestFor?: 'breakfast' | 'lunch' | 'dinner';
    onpick: (recipe: RecipeSummary) => void;
    /**
     * Takes a taken recipe back; makes every row a toggle. Without it a taken row is an ordinary
     * pick (a recipe can be planned twice).
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
