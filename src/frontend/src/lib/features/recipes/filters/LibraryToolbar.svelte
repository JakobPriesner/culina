<script lang="ts">
  import type { Snippet } from 'svelte';

  import { FilterChip, IconButton, SearchField } from '$ds';
  import { tags } from '$features/cookbooks/stores/tags.svelte';
  import { m } from '$shell/i18n';

  import { effectiveSort, type RecipeQuery, type SortContext } from '../stores/libraryView.svelte';
  import { recipeLayout } from '../stores/recipeLayout.svelte';
  import type { SavedSearch } from '../stores/savedSearches.svelte';
  import SearchChips from '../search/SearchChips.svelte';
  import SearchNotice from '../search/SearchNotice.svelte';
  import type { Interpretation } from '../types';
  import AppliedFilters from './AppliedFilters.svelte';
  import FilterSheet from './FilterSheet.svelte';
  import { createQueryBox } from './queryBox.svelte';
  import SavedSearchChips from './SavedSearchChips.svelte';
  import SavedSearchSheet from './SavedSearchSheet.svelte';

  /** One toolbar for every recipe list, so a shelf is sorted and narrowed exactly like the library and neither gains a filter the other lacks. */
  interface Props {
    id: string;
    householdId: string;
    view: RecipeQuery;
    context: SortContext;
    searchLabel: string;
    searchPlaceholder: string;
    keyShortcuts?: string;
    /** Whether a search here can be saved; false in a cookbook, where reapplying it from the library would find something else. */
    savable?: boolean;
    /** Whatever the page says about its own list (count, order note). */
    summary?: Snippet;
    /** What the server read the query to mean and how many it found: the chips and notices under the box. Absent where nothing searches. */
    interpretation?: Interpretation | null;
    total?: number;
    /** The cookbook this list is, drawn as the first chip of a search inside it. */
    scope?: string;
    /** The reader turned the server's correction of their words down. */
    onastyped?: () => void;
    onpromote?: (search: SavedSearch) => void;
  }

  let {
    id,
    householdId,
    view,
    context,
    searchLabel,
    searchPlaceholder,
    keyShortcuts,
    savable = false,
    summary,
    interpretation = null,
    total = 0,
    scope,
    onastyped,
    onpromote
  }: Props = $props();

  const box = createQueryBox(() => view);

  let filtering = $state(false);
  let saving = $state(false);

  const order = $derived(effectiveSort(view.sort, context));

  /** The tag's own word, so a chip reads "Vegetarisch" rather than its slug. */
  const named = $derived(new Map(tags.items.map((tag) => [tag.slug, tag.name] as const)));

  function apply(search: SavedSearch) {
    view.assign(search);
  }
</script>

<div class="toolbar">
  <div class="tools">
    <div class="search">
      <SearchField
        {id}
        value={box.typed}
        label={searchLabel}
        placeholder={searchPlaceholder}
        clearLabel={m['recipes.list.clearSearch']()}
        {keyShortcuts}
        oninput={box.type}
        onclear={box.clear}
      />
    </div>

    <!-- The trigger shows how many filters are on, so nobody opens the panel just to see whether it does anything. -->
    <FilterChip shape="rounded" selected={view.activeCount > 0} onclick={() => (filtering = true)}>
      {#snippet icon()}
        <svg
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.8"
          aria-hidden="true"
        >
          <path d="M4 6h16M7 12h10M10 18h4" stroke-linecap="round" />
        </svg>
      {/snippet}
      {view.activeCount > 0
        ? m['filters.actionWith']({ count: view.activeCount })
        : m['filters.action']()}
    </FilterChip>

    {#if summary}
      <div class="summary">{@render summary()}</div>
    {/if}

    <!-- Only a phone has two ways to list: rows to find one quickly, photos to browse. -->
    <div class="layout" role="group" aria-label={m['recipes.list.layout']()}>
      <IconButton
        size="sm"
        label={m['recipes.list.layoutRows']()}
        pressed={recipeLayout.current === 'rows'}
        onclick={() => recipeLayout.choose('rows')}
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
          <rect x="3" y="4.5" width="6" height="5" rx="1" />
          <rect x="3" y="14.5" width="6" height="5" rx="1" />
          <path d="M12 6h9M12 8.5h5M12 16h9M12 18.5h5" stroke-linecap="round" />
        </svg>
      </IconButton>
      <IconButton
        size="sm"
        label={m['recipes.list.layoutPhotos']()}
        pressed={recipeLayout.current === 'photos'}
        onclick={() => recipeLayout.choose('photos')}
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
          <rect x="3" y="3.5" width="18" height="13" rx="1.5" />
          <path
            d="M3 13.5l5-4 4 3 3-2 6 4.5M3 20.5h12"
            stroke-linecap="round"
            stroke-linejoin="round"
          />
        </svg>
      </IconButton>
    </div>
  </div>

  {#if interpretation}
    <SearchChips chips={interpretation.chips} {scope} onremove={box.removeChip} />
    <SearchNotice
      {interpretation}
      {total}
      query={view.query}
      offer={false}
      onastyped={() => onastyped?.()}
      onremove={box.removeChip}
    />
  {/if}

  {#if savable}
    <SavedSearchChips {householdId} {view} onapply={apply} />
  {/if}

  <AppliedFilters {view} {named} {order} />
</div>

<FilterSheet
  bind:open={filtering}
  {householdId}
  {view}
  {context}
  onsave={savable
    ? () => {
        filtering = false;
        saving = true;
      }
    : undefined}
  onclose={() => (filtering = false)}
/>

{#if savable}
  <SavedSearchSheet
    bind:open={saving}
    {householdId}
    {view}
    onapply={apply}
    onpromote={(search) => {
      saving = false;
      onpromote?.(search);
    }}
    onclose={() => (saving = false)}
  />
{/if}

<style>
  .toolbar {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    padding-block: var(--space-4);
    margin-bottom: var(--space-6);
    border-block: 1px solid var(--border);
  }

  .tools {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: var(--space-2);
    min-width: 0;
  }

  /* The widest thing in the toolbar: searching is the main use and the panel is opened rarely. Capped, since a full-width field on a big monitor reads as a band. */
  .search {
    flex: 1 1 20rem;
    max-width: 42rem;
    min-width: 0;
  }

  .summary {
    margin-inline-start: auto;
  }

  /* Wider screens show their cards whichever is chosen. */
  .layout {
    display: none;
  }

  @media (max-width: 40rem) {
    .search {
      flex-basis: 100%;
      max-width: none;
    }

    /* Beside the filter button rather than under it, so the two share one row. */
    .summary {
      flex: 1 1 0;
      margin-inline-start: 0;
    }
  }

  /* The grid's own one-column range, so the switch is there exactly when it changes something. */
  @media (width < 40rem) {
    .layout {
      display: flex;
      gap: var(--space-1);
    }
  }
</style>
