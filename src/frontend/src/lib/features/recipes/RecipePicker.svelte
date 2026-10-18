<script lang="ts">
  import type { Snippet } from 'svelte';

  import { FilterChip, SearchField, Sheet } from '$ds';
  import { tags } from '$features/cookbooks/stores/tags.svelte';
  import { m } from '$shell/i18n';

  import SavedSearchChips from './filters/SavedSearchChips.svelte';
  import { explain } from '$shell/explain';
  import { toaster } from '$shell/toaster.svelte';
  import { foldText } from './foldText';
  import { summaryOf } from './mappers';
  import { recipes } from './stores/recipes.svelte';
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
    /** Offers a search with no recipe of that name as a new, title-only recipe, picked at once (the week plans one before it is written). */
    stubs?: boolean;
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
    stubs = false,
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

  /** The words typed, when no recipe is called exactly that: what a new recipe would be named. */
  const stubTitle = $derived.by(() => {
    const title = search.query.trim();

    return stubs &&
      title.length > 1 &&
      !search.shown.some((one) => foldText(one.title.trim()) === foldText(title))
      ? title
      : null;
  });

  let creating = $state(false);

  async function createStub(title: string) {
    creating = true;

    const created = await recipes.create(householdId, title);

    creating = false;

    if ('code' in created) {
      toaster.show({ message: () => explain(created), tone: 'danger' });

      return;
    }

    onpick(summaryOf(created));
  }

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

    {#if stubTitle}
      <button
        type="button"
        class="stub"
        disabled={creating}
        onclick={() => void createStub(stubTitle)}
      >
        <span class="stub-title">+ {m['picker.createStub']({ title: stubTitle })}</span>
        <span class="stub-hint">{m['picker.createStub.hint']()}</span>
      </button>
    {/if}

    {#if search.nothing}
      {#if !stubTitle}<p class="nothing">{m['picker.nothing']()}</p>{/if}
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

  .stub {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    padding: var(--space-3);
    border: 1px dashed var(--border-strong, var(--border));
    border-radius: var(--radius-md);
    background: none;
    color: var(--text);
    font: inherit;
    text-align: start;
    cursor: pointer;
  }

  .stub-title {
    font-weight: var(--weight-medium);
  }

  .stub-hint {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .nothing {
    color: var(--text-muted);
    padding: var(--space-3);
  }
</style>
