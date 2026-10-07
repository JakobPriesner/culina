<script lang="ts">
  import type { Snippet } from 'svelte';

  import { FilterChip, SearchField } from '$ds';
  import { tags } from '$features/cookbooks/stores/tags.svelte';
  import { m } from '$shell/i18n';

  import { effectiveSort, type RecipeQuery, type SortContext } from '../stores/libraryView.svelte';
  import type { SavedSearch } from '../stores/savedSearches.svelte';
  import SearchChips from '../search/SearchChips.svelte';
  import SearchNotice from '../search/SearchNotice.svelte';
  import type { Interpretation } from '../types';
  import AppliedFilters from './AppliedFilters.svelte';
  import FilterSheet from './FilterSheet.svelte';
  import { createQueryBox } from './queryBox.svelte';
  import SavedSearchChips from './SavedSearchChips.svelte';
  import SavedSearchSheet from './SavedSearchSheet.svelte';

  /**
   * One toolbar, for every list of recipes.
   *
   * The library and a cookbook page were two search boxes with two debounces
   * and two ideas of what "filtered" meant, kept in step by hand. They are one
   * component now, so a shelf can be sorted and narrowed exactly as the library
   * can, and neither can gain a filter the other quietly lacks.
   *
   * The debounce is shared for the same reason: see `createQueryBox`.
   */
  interface Props {
    id: string;
    householdId: string;
    view: RecipeQuery;
    context: SortContext;
    searchLabel: string;
    searchPlaceholder: string;
    keyShortcuts?: string;
    /**
     * Whether a search here can be saved.
     *
     * False inside a cookbook: what would be saved is the shelf's own question
     * plus a filter over it, and reapplying that from the library would find
     * something else entirely.
     */
    savable?: boolean;
    /** The count, the order note — whatever the page says about its own list. */
    summary?: Snippet;
    /**
     * What the server read the applied query to mean, and how many it found:
     * the chips and notices under the box. Absent where nothing searches.
     */
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

    <!-- The panel's trigger carries how many filters are on, because a panel
         that has to be opened to find out whether it is doing anything is one
         people open over and over. -->
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

  /*
   * The widest thing in the toolbar, and deliberately so.
   *
   * Searching is what a library of a hundred recipes is actually used with;
   * the panel beside it is opened once a week. It took a third of the row when
   * a "Up to 30 minutes" shortcut sat next to it — one of four ceilings, given
   * a control of its own at the top level because it happened to be the one
   * the app shipped with first. That went into the panel with the other three,
   * and the box took the room.
   *
   * Capped rather than left to grow: a search field running the full width of
   * a 1400px monitor stops reading as a field and starts reading as a band.
   */
  .search {
    flex: 1 1 20rem;
    max-width: 42rem;
    min-width: 0;
  }

  .summary {
    margin-inline-start: auto;
  }

  @media (max-width: 40rem) {
    .search {
      flex-basis: 100%;
      max-width: none;
    }

    .summary {
      width: 100%;
      margin-inline-start: 0;
    }
  }
</style>
