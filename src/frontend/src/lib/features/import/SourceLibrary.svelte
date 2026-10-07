<script lang="ts">
  import { Badge, Button, Checkbox, ErrorState, SearchField, Skeleton } from '$ds';

  import { whenVisible } from '$shell/whenVisible';

  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';

  import { sources } from './stores/sources.svelte';
  import type { ConnectedSource } from './types';

  /**
   * A connected library drawn in this app's own type and rows. Recipes already here are shown as
   * taken, not hidden, so "I have it" stays distinguishable from "it did not come through".
   */
  interface Props {
    source: ConnectedSource;
    onimport: (externalIds: string[]) => void;
  }

  let { source, onimport }: Props = $props();

  let query = $state('');
  let chosen = $state<string[]>([]);

  let debounce: ReturnType<typeof setTimeout> | undefined;

  const available = $derived(sources.recipes.filter((recipe) => recipe.alreadyHere === null));

  /** Ticked only when everything has been read and chosen: with `hasMore`, a full page is not "all". */
  const allChosen = $derived(
    available.length > 0 && chosen.length === available.length && !sources.hasMore
  );

  const someChosen = $derived(chosen.length > 0 && !allChosen);

  const isChosen = (externalId: string) => chosen.includes(externalId);

  function toggle(externalId: string, on: boolean) {
    chosen = on ? [...chosen, externalId] : chosen.filter((one) => one !== externalId);
  }

  /** Selects everything, fetching the rest first: "everything loaded" would be honest but useless. */
  async function toggleAll(on: boolean) {
    if (!on) {
      chosen = [];

      return;
    }

    if (sources.hasMore) {
      await sources.loadEverything();
    }

    // Read after loading: `available` is derived from what has arrived.
    chosen = available.map((recipe) => recipe.externalId);
  }

  $effect(() => () => clearTimeout(debounce));

  /** Debounced: each search reads somebody else's server, and one per keystroke would queue words nobody finished. */
  function type(next: string) {
    query = next;
    clearTimeout(debounce);

    // Long enough for a finished word, short enough to feel live.
    debounce = setTimeout(() => search(next), 250);
  }

  function search(next: string) {
    clearTimeout(debounce);
    query = next;
    chosen = [];
    void sources.browse(source, next);
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
    oninput={type}
    onclear={() => search('')}
  />

  {#if sources.browseStatus === 'loading'}
    <ul class="list">
      {#each ['65%', '50%', '80%', '40%', '70%', '55%'] as width, row (row)}
        <li class="row">
          <div class="row-skeleton">
            <Skeleton width="var(--space-6)" height="var(--space-6)" shape="text" />
            <Skeleton {width} height="1.125rem" />
          </div>
        </li>
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
          disabled={sources.loadingAll}
          label={m['import.library.selectAll']()}
          onchange={(on) => void toggleAll(on)}
        />

        {#if sources.loadingAll}
          <!-- Polite: reports progress without interrupting reading. -->
          <p class="loadingAll" role="status">{m['import.library.loadingAll']()}</p>
        {/if}
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

    {#if sources.hasMore && !sources.moreFailed}
      <!-- Skeleton rows standing for the coming rows; reaching them fetches them, so there is no
           button and no false "finished" look. -->
      <ul class="list" aria-hidden="true">
        {#each ['60%', '75%', '50%'] as width, row (row)}
          <li class="row" {@attach whenVisible(() => void sources.more())}>
            <div class="row-skeleton">
              <Skeleton width="var(--space-6)" height="var(--space-6)" shape="text" />
              <Skeleton {width} height="1.125rem" />
            </div>
          </li>
        {/each}
      </ul>
    {:else if sources.moreFailed}
      <p class="none" role="status">{m['import.library.moreFailed']()}</p>
    {/if}
  {/if}
</section>

{#if chosen.length > 0}
  <!-- Sticky: choosing happens by scrolling, and the action must stay within a thumb's reach. -->
  <div class="bar">
    <p class="chosen" role="status">{m['import.library.chosen']({ count: chosen.length })}</p>

    {#if sources.importError}
      <!-- The selection is still on screen, so the refusal sits beside the button. -->
      <p class="refused" role="alert">{explain(sources.importError)}</p>
    {/if}

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
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    gap: var(--space-2) var(--space-3);
    padding-bottom: var(--space-2);
    border-bottom: 1px solid var(--border);
  }

  .loadingAll {
    color: var(--text-muted);
    font-size: var(--text-sm);
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

  .row-skeleton {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    width: 100%;
  }

  .row + .row {
    border-top: 1px solid var(--border);
  }

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

  .refused {
    color: var(--text-danger);
    font-size: var(--text-sm);
  }

  .chosen {
    font-size: var(--text-sm);
    font-variant-numeric: tabular-nums;
  }
</style>
