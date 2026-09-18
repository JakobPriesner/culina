<script lang="ts">
  import type { Snippet } from 'svelte';

  import { FilterChip, SearchField } from '$ds';
  import { tags } from '$features/cookbooks/stores/tags.svelte';
  import { m } from '$shell/i18n';

  import { effectiveSort, type RecipeQuery, type SortContext } from '../stores/libraryView.svelte';
  import { savedSearches, type SavedSearch } from '../stores/savedSearches.svelte';
  import FilterSheet from './FilterSheet.svelte';
  import SavedSearchSheet from './SavedSearchSheet.svelte';
  import { sortLabel, timeLabel } from './labels';

  /**
   * One toolbar, for every list of recipes.
   *
   * The library and a cookbook page were two search boxes with two debounces
   * and two ideas of what "filtered" meant, kept in step by hand. They are one
   * component now, so a shelf can be sorted and narrowed exactly as the library
   * can, and neither can gain a filter the other quietly lacks.
   *
   * The debounce lives here rather than in each page for the same reason: two
   * boxes that waited different lengths would feel like two different apps.
   */
  interface Props {
    id: string;
    householdId: string;
    view: RecipeQuery;
    context: SortContext;
    searchLabel: string;
    searchPlaceholder: string;
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
    onpromote?: (search: SavedSearch) => void;
  }

  let {
    id,
    householdId,
    view,
    context,
    searchLabel,
    searchPlaceholder,
    savable = false,
    summary,
    onpromote
  }: Props = $props();

  /** What is in the box, which runs ahead of what has been applied. */
  let typed = $state(view.query);

  let debounce: ReturnType<typeof setTimeout> | undefined;

  let filtering = $state(false);
  let saving = $state(false);

  const order = $derived(effectiveSort(view.sort, context));

  /** The tag's own word, so a chip reads "Vegetarisch" rather than its slug. */
  const named = $derived(new Map(tags.items.map((tag) => [tag.slug, tag.name] as const)));

  $effect(() => {
    if (savable && householdId) {
      void savedSearches.load(householdId);
    }
  });

  // The box follows the query when something else sets it — applying a saved
  // search, or clearing everything — without fighting what is being typed.
  $effect(() => {
    if (view.query !== typed) {
      clearTimeout(debounce);
      typed = view.query;
    }
  });

  $effect(() => () => clearTimeout(debounce));

  function type(value: string) {
    typed = value;
    clearTimeout(debounce);

    // Long enough that a word is finished, short enough that it feels live.
    debounce = setTimeout(() => (view.query = value), 250);
  }

  function clearSearch() {
    typed = '';
    clearTimeout(debounce);
    view.query = '';
  }

  function apply(search: SavedSearch) {
    view.assign(search);
  }

  /** Whether the toolbar is showing exactly what this saved search asks for. */
  function showing(search: SavedSearch): boolean {
    return (
      search.query === view.query &&
      search.maxMinutes === view.maxMinutes &&
      search.sort === view.sort &&
      search.tags.length === view.tags.length &&
      search.tags.every((slug) => view.tags.includes(slug))
    );
  }
</script>

<div class="toolbar">
  <div class="tools">
    <div class="search">
      <SearchField
        {id}
        value={typed}
        label={searchLabel}
        placeholder={searchPlaceholder}
        clearLabel={m['recipes.list.clearSearch']()}
        oninput={type}
        onclear={clearSearch}
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

    <!-- The shortcut the library has always had, now one value of the time
         filter rather than a separate flag that could disagree with it. -->
    <FilterChip shape="rounded" selected={view.quick} onclick={() => (view.quick = !view.quick)}>
      {#snippet icon()}
        <svg
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.8"
          aria-hidden="true"
        >
          <circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" stroke-linecap="round" />
        </svg>
      {/snippet}
      {m['recipes.filter.quick']()}
    </FilterChip>

    {#if summary}
      <div class="summary">{@render summary()}</div>
    {/if}
  </div>

  {#if savable && savedSearches.items.length > 0}
    <div class="saved" role="group" aria-label={m['saved.title']()}>
      {#each savedSearches.items as search (search.id)}
        <FilterChip selected={showing(search)} onclick={() => apply(search)}>
          {search.name}
        </FilterChip>
      {/each}
    </div>
  {/if}

  <!-- What is applied, each removable where it stands. Chips rather than a
       sentence, and the same vocabulary the search overlay will use, so the
       two arrive as one idea rather than two. -->
  {#if view.tags.length > 0 || view.maxMinutes !== null || view.sort !== null}
    <ul class="applied" aria-label={m['filters.applied']()}>
      {#each view.tags as slug (slug)}
        <li>
          <button
            type="button"
            class="chip"
            aria-label={m['filters.chip.remove']({ name: named.get(slug) ?? slug })}
            onclick={() => view.toggleTag(slug)}
          >
            {named.get(slug) ?? slug}
            <span aria-hidden="true">×</span>
          </button>
        </li>
      {/each}

      {#if view.maxMinutes !== null}
        <li>
          <button
            type="button"
            class="chip"
            aria-label={m['filters.chip.remove']({ name: timeLabel(view.maxMinutes) })}
            onclick={() => (view.maxMinutes = null)}
          >
            {timeLabel(view.maxMinutes)}
            <span aria-hidden="true">×</span>
          </button>
        </li>
      {/if}

      {#if view.sort !== null}
        <li>
          <button
            type="button"
            class="chip"
            aria-label={m['filters.chip.remove']({ name: sortLabel(order) })}
            onclick={() => (view.sort = null)}
          >
            {m['filters.chip.sort']({ name: sortLabel(order) })}
            <span aria-hidden="true">×</span>
          </button>
        </li>
      {/if}
    </ul>
  {/if}
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

  .search {
    flex: 1 1 14rem;
    max-width: 28rem;
    min-width: 0;
  }

  .summary {
    margin-inline-start: auto;
  }

  /* One line that scrolls rather than wraps: saved searches that wrapped would
     push the results below the fold on a phone, which is the one screen where
     the point of them is not having to set the filters again. */
  .saved {
    display: flex;
    gap: var(--space-2);
    overflow-x: auto;
    scrollbar-width: none;
  }

  .saved::-webkit-scrollbar {
    display: none;
  }

  .applied {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .chip {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
    min-height: var(--control-sm);
    padding: 0 var(--space-3);
    border: 1px solid var(--border);
    border-radius: var(--radius-full);
    background: var(--surface-sunken);
    color: var(--text);
    font: inherit;
    font-size: var(--text-sm);
    cursor: pointer;
  }

  .chip:hover {
    border-color: var(--border-strong);
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
