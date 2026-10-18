<script lang="ts">
  import type { Snippet } from 'svelte';

  import { FilterChip, SearchField, Sheet } from '$ds';
  import { tags } from '$features/cookbooks/stores/tags.svelte';
  import { m } from '$shell/i18n';

  import SavedSearchChips from './filters/SavedSearchChips.svelte';
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
    /** Said of a taken recipe in place of "Added", by id, for a caller whose picks are not receipts (a recipe can be planned again). */
    notes?: ReadonlyMap<string, string>;
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
    notes,
    cookbookId,
    suggestFor,
    onpick,
    onremove,
    onclose
  }: Props = $props();

  const takenIds = $derived(new Set(taken));

  const id = $props.id();

  /** A weeknight's usual question first: how long it may take. */
  const quickMinutes = 30;

  /** The kitchen's most used tags, as quick chips; the long tail is what search is for. */
  const quickTags = $derived(
    [...tags.items].sort((a, b) => b.recipeCount - a.recipeCount).slice(0, 4)
  );

  $effect(() => {
    if (open) {
      void tags.load(householdId);
    }
  });

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

    <!-- Where the decision is made: saved searches and a few quick filters, as on the library's toolbar. -->
    <div class="quick" role="group" aria-label={m['picker.quick']()}>
      <SavedSearchChips {householdId} view={search.filter} onapply={search.applySaved} />
      <div class="chips">
        <FilterChip
          selected={search.filter.maxMinutes === quickMinutes}
          onclick={() => search.toggleMinutes(quickMinutes)}
        >
          {m['picker.quick.minutes']({ count: quickMinutes })}
        </FilterChip>
        {#each quickTags as tag (tag.slug)}
          <FilterChip
            selected={search.filter.tags.includes(tag.slug)}
            onclick={() => search.filter.toggleTag(tag.slug)}
          >
            {tag.name}
          </FilterChip>
        {/each}
      </div>
    </div>

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
      <PickerResults recipes={search.shown} taken={takenIds} {notes} {onpick} {onremove} />
    {/if}
  </div>
</Sheet>

<style>
  .picker {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .quick {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    min-width: 0;
  }

  /* One row that scrolls, like the saved searches above it: results stay in view on a phone. */
  .chips {
    display: flex;
    gap: var(--space-2);
    overflow-x: auto;
    scrollbar-width: none;
  }

  /* Whole words: a chip keeps its width and the row scrolls, never "vegetari-sch". */
  .chips > :global(*) {
    flex: none;
    white-space: nowrap;
    overflow-wrap: normal;
  }

  .nothing {
    color: var(--text-muted);
    padding: var(--space-3);
  }
</style>
