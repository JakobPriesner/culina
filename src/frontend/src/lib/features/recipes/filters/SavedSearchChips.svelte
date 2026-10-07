<script lang="ts">
  import { FilterChip } from '$ds';
  import { m } from '$shell/i18n';

  import type { RecipeQuery } from '../stores/libraryView.svelte';
  import { savedSearches, type SavedSearch } from '../stores/savedSearches.svelte';

  /** The household's saved searches, one tap each to apply. */
  interface Props {
    householdId: string;
    view: RecipeQuery;
    onapply: (search: SavedSearch) => void;
  }

  let { householdId, view, onapply }: Props = $props();

  $effect(() => {
    if (householdId) {
      void savedSearches.load(householdId);
    }
  });

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

{#if savedSearches.items.length > 0}
  <div class="saved" role="group" aria-label={m['saved.title']()}>
    {#each savedSearches.items as search (search.id)}
      <FilterChip selected={showing(search)} onclick={() => onapply(search)}>
        {search.name}
      </FilterChip>
    {/each}
  </div>
{/if}

<style>
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
</style>
