<script lang="ts">
  import { m } from '$shell/i18n';

  import type { Facets } from '../types';
  import SearchFacet from './SearchFacet.svelte';
  import { cuisineLabel } from './wording';

  /** The refinements that would split these results, each one a tap. */
  interface Props {
    facets: Facets;
    /** What is in the box, which a time or a cuisine is added to. */
    typed: string;
    ontag: (slug: string, name: string) => void;
    onquery: (query: string) => void;
  }

  let { facets, typed, ontag, onquery }: Props = $props();
</script>

<div class="refine" role="group" aria-label={m['search.refine']()}>
  <span class="refine-label">{m['search.refine']()}</span>
  {#each facets.tags as facet (facet.value)}
    <SearchFacet onclick={() => ontag(facet.value, facet.label ?? facet.value)}>
      {facet.label ?? facet.value} <span class="n">{facet.count}</span>
    </SearchFacet>
  {/each}
  {#each facets.times as facet (facet.value)}
    <SearchFacet
      onclick={() => onquery(`${typed.trim()} ${m['search.chip.time']({ minutes: facet.value })}`)}
    >
      {m['search.chip.time']({ minutes: facet.value })} <span class="n">{facet.count}</span>
    </SearchFacet>
  {/each}
  {#each facets.cuisines as facet (facet.value)}
    <SearchFacet onclick={() => onquery(`${typed.trim()} ${cuisineLabel(facet.value)}`)}>
      {cuisineLabel(facet.value)} <span class="n">{facet.count}</span>
    </SearchFacet>
  {/each}
</div>

<style>
  .refine {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
  }

  .refine-label {
    color: var(--text-muted);
    font-size: var(--text-xs);
    font-weight: var(--weight-medium);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .n {
    color: var(--text-muted);
    font-variant-numeric: tabular-nums;
  }
</style>
