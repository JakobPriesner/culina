<script lang="ts">
  import { Badge, Button, Checkbox, ErrorState, SearchField, Skeleton } from '$ds';

  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';

  import { sources } from './stores/sources.svelte';
  import type { ConnectedSource } from './types';

  /**
   * Their library, drawn as ours.
   *
   * The one decision that makes this feel like moving in rather than like
   * running an importer: what comes back from the other app is shown in this
   * app's own type and spacing, with the same rows the recipe list uses. Nobody
   * has to look at somebody else's database to decide what to keep.
   *
   * Recipes already here are shown rather than hidden, and shown as taken. A
   * library that quietly dropped what you already had would leave you unable to
   * tell "I have it" from "it did not come through", which is the exact
   * question somebody asks on their second visit.
   */
  interface Props {
    source: ConnectedSource;
    onimport: (externalIds: string[]) => void;
  }

  let { source, onimport }: Props = $props();

  let query = $state('');
  let chosen = $state<string[]>([]);

  /** Everything that could still be brought over. */
  const available = $derived(sources.recipes.filter((recipe) => recipe.alreadyHere === null));

  const allChosen = $derived(available.length > 0 && chosen.length === available.length);

  const someChosen = $derived(chosen.length > 0 && !allChosen);

  const isChosen = (externalId: string) => chosen.includes(externalId);

  function toggle(externalId: string, on: boolean) {
    chosen = on ? [...chosen, externalId] : chosen.filter((one) => one !== externalId);
  }

  function toggleAll(on: boolean) {
    // Everything loaded, not everything there is. Selecting recipes that have
    // not been read yet would be selecting a number rather than a list, and
    // "select all 2,000" that quietly means "the 36 on screen" is worse than
    // no select-all at all.
    chosen = on ? available.map((recipe) => recipe.externalId) : [];
  }

  async function search(next: string) {
    query = next;
    chosen = [];
    await sources.browse(source, next);
  }
</script>

<section class="library" aria-labelledby="library-heading">
  <header class="head">
    <h2 id="library-heading" class="heading">
      {m['import.library.title']({ name: source.label })}
    </h2>

    {#if sources.total !== null}
      <p class="count">{m['import.library.count']({ count: sources.total })}</p>
    {/if}
  </header>

  <SearchField
    id="library-search"
    value={query}
    label={m['import.library.searchLabel']()}
    placeholder={m['import.library.searchPlaceholder']()}
    clearLabel={m['import.library.searchClear']()}
    oninput={(next) => void search(next)}
    onclear={() => void search('')}
  />

  {#if sources.browseStatus === 'loading'}
    <ul class="list">
      {#each [0, 1, 2, 3, 4, 5] as row (row)}
        <li class="row"><Skeleton height="1.25rem" /></li>
      {/each}
    </ul>
  {:else if sources.browseStatus === 'failed'}
    <ErrorState
      title={m['import.library.failed']()}
      body={sources.browseError ? explain(sources.browseError) : m['import.library.failed']()}
      requestIdLabel={m['error.reference']()}
      requestId={sources.browseError?.requestId}
    >
      {#snippet action()}
        <Button onclick={() => void sources.browse(source, query)}>{m['error.retry']()}</Button>
      {/snippet}
    </ErrorState>
  {:else if sources.recipes.length === 0}
    <p class="none">{m['import.library.empty']()}</p>
  {:else}
    {#if available.length > 0}
      <div class="all">
        <Checkbox
          checked={allChosen}
          indeterminate={someChosen}
          label={m['import.library.selectAll']()}
          onchange={toggleAll}
        />
      </div>
    {/if}

    <ul class="list">
      {#each sources.recipes as recipe (recipe.externalId)}
        <li class="row" class:taken={recipe.alreadyHere !== null}>
          {#if recipe.alreadyHere === null}
            <Checkbox
              checked={isChosen(recipe.externalId)}
              label={recipe.title}
              onchange={(on) => toggle(recipe.externalId, on)}
            />
          {:else}
            <p class="title">{recipe.title}</p>
            <Badge>{m['import.library.alreadyHere']()}</Badge>
          {/if}
        </li>
      {/each}
    </ul>

    {#if sources.hasMore}
      <div class="more">
        <Button loading={sources.loadingMore} onclick={() => void sources.more(query)}>
          {m['import.library.more']()}
        </Button>
      </div>
    {/if}
  {/if}
</section>

{#if chosen.length > 0}
  <!-- Sticky, because the choosing happens by scrolling and the action has to
       stay within a thumb's reach the whole way down. -->
  <div class="bar">
    <p class="chosen" role="status">{m['import.library.chosen']({ count: chosen.length })}</p>

    <Button variant="primary" onclick={() => onimport([...chosen])}>
      {m['import.library.bringOver']()}
    </Button>
  </div>
{/if}

<style>
  .library {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .head {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    gap: var(--space-2) var(--space-3);
  }

  .heading {
    font-family: var(--font-editorial);
    font-size: var(--text-xl);
    font-weight: var(--weight-regular);
  }

  .count {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .all {
    padding-bottom: var(--space-2);
    border-bottom: 1px solid var(--border);
  }

  .list {
    list-style: none;
    margin: 0;
    padding: 0;
    display: flex;
    flex-direction: column;
  }

  .row {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-3);
    padding-block: var(--space-2);
    min-width: 0;
  }

  .row + .row {
    border-top: 1px solid var(--border);
  }

  /* Dimmed rather than removed: "I already have it" and "it did not come
     through" are the two things somebody needs to tell apart. */
  .taken {
    color: var(--text-muted);
  }

  .title {
    min-width: 0;
    overflow-wrap: anywhere;
  }

  .none {
    color: var(--text-muted);
  }

  .more {
    display: flex;
    justify-content: center;
  }

  .bar {
    position: sticky;
    bottom: 0;
    z-index: 1;
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-3);
    margin-top: var(--space-4);
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
    box-shadow: var(--shadow-card);
  }

  .chosen {
    font-size: var(--text-sm);
    font-variant-numeric: tabular-nums;
  }
</style>
