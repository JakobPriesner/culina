<script lang="ts">
  import { FilterChip } from '$ds';
  import { m } from '$shell/i18n';

  import type { RecipeQuery } from '../stores/libraryView.svelte';
  import { savedSearches, type SavedSearch } from '../stores/savedSearches.svelte';

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
  /* Scroll instead of wrapping so results stay above the fold on phones. */
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
