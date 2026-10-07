<script lang="ts">
  import { SearchField } from '$ds';
  import { m } from '$shell/i18n';

  import type { PreviewFilter } from './previewLibrary.svelte';

  /** The filter buttons and the search box above the preview's collection. */
  interface Props {
    filter: PreviewFilter;
    search: string;
  }

  let { filter = $bindable(), search = $bindable() }: Props = $props();

  const filters: { id: PreviewFilter; label: string }[] = [
    { id: 'all', label: m['preview.all']() },
    { id: 'favourites', label: m['preview.favourites']() },
    { id: 'quick', label: m['preview.quick']() }
  ];
</script>

<div class="tools">
  <div class="filters" role="group" aria-label={m['preview.filters']()}>
    {#each filters as item (item.id)}<button
        class:active={filter === item.id}
        aria-pressed={filter === item.id}
        onclick={() => (filter = item.id)}>{item.label}</button
      >{/each}
  </div>
  <div class="search">
    <SearchField
      id="preview-search"
      label={m['preview.search']()}
      placeholder={m['preview.search']()}
      clearLabel={m['preview.clear']()}
      bind:value={search}
    />
  </div>
</div>

<style>
  .tools {
    margin-block: var(--space-8) var(--space-6);
    display: flex;
    flex-wrap: wrap;
    justify-content: space-between;
    align-items: center;
    gap: var(--space-4);
  }

  .filters {
    display: flex;
    gap: var(--space-2);
    flex-wrap: wrap;
  }

  .filters button {
    min-height: var(--control-sm);
    padding-inline: var(--space-4);
    border: 1px solid transparent;
    border-radius: var(--radius-full);
    background: transparent;
    color: var(--text-muted);
    font: inherit;
    font-size: var(--text-sm);
    cursor: pointer;
  }

  .filters button:hover {
    background: var(--surface-hover);
    color: var(--text);
  }

  .filters button.active {
    border-color: var(--accent);
    color: var(--accent-contrast);
    background: var(--accent);
  }

  .search {
    width: min(22rem, 100%);
  }

  @media (width < 64rem) {
    .tools {
      flex-direction: column-reverse;
      align-items: stretch;
    }

    .filters button {
      padding-inline: var(--space-3);
    }

    .search {
      width: 100%;
    }
  }
</style>
