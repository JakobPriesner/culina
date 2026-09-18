<script lang="ts">
  import { resolve } from '$app/paths';
  import { Button, Card, Divider, ErrorState } from '$ds';

  import { session } from '$features/auth/session.svelte';
  import ConnectSource from '$features/import/ConnectSource.svelte';
  import ImportProgress from '$features/import/ImportProgress.svelte';
  import SourceLibrary from '$features/import/SourceLibrary.svelte';
  import { sources } from '$features/import/stores/sources.svelte';
  import type { ConnectedSource } from '$features/import/types';
  import { explain } from '$shell/explain';
  import { formatDate, m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';

  /**
   * Bringing a whole library over.
   *
   * A page of its own rather than a panel in settings, because for the ten
   * minutes it takes this is the only thing somebody is doing — and because it
   * is a place you leave. Once the recipes are here there is nothing to come
   * back for except the next batch, and the way back is the cookbook it made,
   * not this screen.
   *
   * Three states, in the order they happen: connect, choose, watch. Never two
   * at once. The thing that ties them together is that the last one ends with a
   * link out of here and into the ordinary library, which is where the recipes
   * now live.
   */
  const householdId = $derived(session.activeHouseholdId);

  let connectingAnother = $state(false);

  $effect(() => {
    if (householdId) {
      void sources.list(householdId);
    }
  });

  function look(source: ConnectedSource) {
    connectingAnother = false;
    void sources.browse(source);
  }

  function connected(source: ConnectedSource) {
    look(source);
  }

  function bringOver(externalIds: string[]) {
    const source = sources.open;

    if (source) {
      void sources.import(source.sourceId, externalIds);
    }
  }

  function again() {
    sources.forgetRun();

    const source = sources.open;

    if (source) {
      // Re-read, so what has just arrived shows as already here. Otherwise the
      // second pass offers the same recipes back and the count would be a lie.
      void sources.browse(source);
    }
  }

  const showingConnect = $derived(
    connectingAnother || (sources.status === 'ready' && sources.items.length === 0)
  );
</script>

<svelte:head><title>{m['import.title']()}</title></svelte:head>

<Page width="reading">
  <div class="stack">
    {#if sources.run}
      <ImportProgress run={sources.run} ondone={again} onlook={() => sources.reconnect()} />
    {:else if sources.open}
      <div>
        <Button variant="ghost" onclick={() => sources.closeLibrary()}>
          {m['import.backToApps']()}
        </Button>
      </div>

      <SourceLibrary source={sources.open} onimport={bringOver} />
    {:else}
      <header class="intro">
        <h1 class="heading">{m['import.title']()}</h1>
        <p class="lead">{m['import.lead']()}</p>
      </header>

      {#if sources.status === 'failed'}
        <!-- Without this the page is a heading and a footer link: neither the
             list nor the connect form shows, and there is nothing to press. -->
        <ErrorState
          title={m['import.sourcesFailed']()}
          body={sources.error ? explain(sources.error) : m['import.sourcesFailed']()}
          requestIdLabel={m['error.reference']()}
          requestId={sources.error?.requestId}
        >
          {#snippet action()}
            <Button onclick={() => householdId && void sources.relist(householdId)}>
              {m['error.retry']()}
            </Button>
          {/snippet}
        </ErrorState>
      {/if}

      {#if sources.items.length > 0}
        <ul class="apps">
          {#each sources.items as source (source.sourceId)}
            <li>
              <Card>
                <div class="app">
                  <div class="named">
                    <p class="label">{source.label}</p>
                    <p class="address">{source.address}</p>
                    <p class="when">
                      {source.lastUsedAt
                        ? m['import.source.lastUsed']({
                            when: formatDate(new Date(source.lastUsedAt), { dateStyle: 'long' })
                          })
                        : m['import.source.neverUsed']()}
                    </p>
                  </div>

                  <div class="appActions">
                    <Button variant="primary" onclick={() => look(source)}>
                      {m['import.source.browse']()}
                    </Button>

                    <Button
                      variant="ghost"
                      onclick={() => void sources.disconnect(source.sourceId)}
                    >
                      {m['import.source.disconnect']()}
                    </Button>
                  </div>
                </div>
              </Card>
            </li>
          {/each}
        </ul>
      {/if}

      {#if showingConnect}
        {#if sources.items.length > 0}<Divider />{/if}

        <section class="adding" aria-labelledby="adding-heading">
          <h2 id="adding-heading" class="subheading">{m['import.source.title']()}</h2>
          <p class="hint">{m['import.source.hint']()}</p>

          {#if householdId}
            <ConnectSource {householdId} onconnected={connected} />
          {/if}
        </section>
      {:else if sources.status === 'ready'}
        <div>
          <Button onclick={() => (connectingAnother = true)}>
            {m['import.source.another']()}
          </Button>
        </div>
      {/if}

      <p class="elsewhere">
        {m['import.oneAtATime']()}
        <a href={resolve('/(app)/recipes/new')}>{m['import.oneAtATimeLink']()}</a>
      </p>
    {/if}
  </div>
</Page>

<style>
  .stack {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
  }

  .intro {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }

  .heading {
    font-family: var(--font-editorial);
    font-size: var(--text-2xl);
    font-weight: var(--weight-regular);
  }

  .lead {
    color: var(--text-muted);
  }

  .subheading {
    font-size: var(--text-lg);
    font-weight: var(--weight-medium);
  }

  .hint {
    margin-top: var(--space-1);
    margin-bottom: var(--space-4);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .apps {
    list-style: none;
    margin: 0;
    padding: 0;
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .app {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-3);
  }

  .named {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    min-width: 0;
  }

  .label {
    font-size: var(--text-lg);
  }

  /* Addresses are long and have no spaces to break at. */
  .address {
    color: var(--text-muted);
    font-size: var(--text-sm);
    overflow-wrap: anywhere;
  }

  .when {
    color: var(--text-subtle);
    font-size: var(--text-xs);
  }

  .appActions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  .adding {
    display: flex;
    flex-direction: column;
  }

  .elsewhere {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
