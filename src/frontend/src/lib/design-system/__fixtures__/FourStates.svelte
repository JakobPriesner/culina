<script lang="ts">
  import Button from '../actions/Button.svelte';
  import BusyRegion from '../feedback/BusyRegion.svelte';
  import EmptyState from '../feedback/EmptyState.svelte';
  import ErrorState from '../feedback/ErrorState.svelte';
  import Skeleton from '../feedback/Skeleton.svelte';

  /**
   * A list in each of the four states a list can be in. Stands in for a real
   * feature so the states can be asserted without one existing yet.
   */
  type State = 'loading' | 'empty' | 'filtered' | 'error' | 'loaded';

  interface Props {
    state?: State;
    refreshing?: boolean;
    onretry?: () => void;
    onclear?: () => void;
  }

  let { state = 'loaded', refreshing = false, onretry, onclear }: Props = $props();

  const items = ['Tomato soup', 'Bread'];
</script>

{#if state === 'loading'}
  <div aria-busy="true" aria-label="Loading recipes">
    {#each [0, 1, 2] as row (row)}
      <Skeleton width="12rem" />
    {/each}
  </div>
{:else if state === 'empty'}
  <EmptyState
    title="No recipes yet"
    body="Recipes you add show up here, with everything you need while you cook."
  >
    {#snippet action()}
      <Button variant="primary">Add a recipe</Button>
    {/snippet}
  </EmptyState>
{:else if state === 'filtered'}
  <EmptyState title="Nothing matched" body="No recipe matches that search.">
    {#snippet action()}
      <Button onclick={onclear}>Clear the filter</Button>
    {/snippet}
  </EmptyState>
{:else if state === 'error'}
  <ErrorState
    title="Could not load your recipes"
    body="The connection dropped on the way. Nothing was lost."
    requestIdLabel="Reference"
    requestId="req-4f2a"
  >
    {#snippet action()}
      <Button variant="primary" onclick={onretry}>Try again</Button>
    {/snippet}
  </ErrorState>
{:else}
  <BusyRegion busy={refreshing} label="Refreshing recipes">
    <ul>
      {#each items as item (item)}
        <li>{item}</li>
      {/each}
    </ul>
  </BusyRegion>
{/if}
