<script lang="ts">
  import { FilterChip, SearchField, VisuallyHidden } from '$ds';
  import type { TagInUse } from '$features/cookbooks/stores/tags.svelte';
  import { m } from '$shell/i18n';

  /**
   * Choosing some of a kitchen's tags: one control for the library filters and cookbook rules, so both answer the same question.
   * Chosen tags come first and stay in view, then the most-used few until someone asks for the rest or types; the stable order lets a hand learn where a tag is.
   */
  interface Props {
    tags: readonly TagInUse[];
    selected: readonly string[];
    ontoggle: (slug: string, on: boolean) => void;
    /** What to say when the kitchen has no tags at all. */
    empty: string;
  }

  let { tags, selected, ontoggle, empty }: Props = $props();

  const collapsedCount = 8;

  const id = $props.id();

  let query = $state('');
  let expanded = $state(false);

  /** Case and accents ignored, so "creme" finds "Crème". */
  const fold = (text: string) => text.toLocaleLowerCase().normalize('NFD').replace(/\p{M}/gu, '');

  /** The count as a screen reader hears it, after the name it belongs to. */
  const usedBy = (tag: TagInUse) => ` (${m['cookbooks.rules.usedBy']({ count: tag.recipeCount })})`;

  const ordered = $derived(
    [...tags].sort(
      (left, right) => right.recipeCount - left.recipeCount || left.name.localeCompare(right.name)
    )
  );

  const chosen = $derived(ordered.filter((tag) => selected.includes(tag.slug)));

  const searching = $derived(query.trim().length > 0);

  const others = $derived(
    ordered.filter(
      (tag) =>
        !selected.includes(tag.slug) && (!searching || fold(tag.name).includes(fold(query.trim())))
    )
  );

  const offered = $derived(searching || expanded ? others : others.slice(0, collapsedCount));

  const collapsible = $derived(!searching && tags.length - chosen.length > collapsedCount);
</script>

{#if tags.length === 0}
  <p class="quiet">{empty}</p>
{:else}
  <div class="chooser">
    {#if tags.length > collapsedCount}
      <SearchField
        id="{id}-search"
        label={m['tags.search']()}
        clearLabel={m['tags.search.clear']()}
        placeholder={m['tags.search']()}
        bind:value={query}
      />
    {/if}

    <ul class="chips" id="{id}-chips">
      {#each [...chosen, ...offered] as tag (tag.slug)}
        {@const on = selected.includes(tag.slug)}
        <li>
          <FilterChip selected={on} onclick={() => ontoggle(tag.slug, !on)}>
            {tag.name}<span class="count" aria-hidden="true">{tag.recipeCount}</span><VisuallyHidden
              >{usedBy(tag)}</VisuallyHidden
            >
          </FilterChip>
        </li>
      {/each}
    </ul>

    {#if searching && others.length === 0}
      <p class="quiet" role="status">{m['tags.nothing']()}</p>
    {/if}

    {#if collapsible}
      <button
        type="button"
        class="more"
        aria-expanded={expanded}
        aria-controls="{id}-chips"
        onclick={() => (expanded = !expanded)}
      >
        {expanded ? m['tags.fewer']() : m['tags.all']({ count: tags.length })}
      </button>
    {/if}
  </div>
{/if}

<style>
  .chooser {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-3);
  }

  .chooser > :global(:first-child) {
    align-self: stretch;
  }

  .chips {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .count {
    margin-inline-start: var(--space-1);
    color: var(--text-subtle);
    font-weight: var(--weight-regular);
  }

  .more {
    padding: var(--space-1) 0;
    border: 0;
    background: none;
    color: var(--text);
    font: inherit;
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
    text-decoration: underline;
    text-underline-offset: 0.2em;
    cursor: pointer;
  }

  .quiet {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
