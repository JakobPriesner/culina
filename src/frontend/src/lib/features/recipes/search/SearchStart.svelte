<script lang="ts">
  import { m } from '$shell/i18n';

  import SearchFacet from './SearchFacet.svelte';

  /** What the field offers before anything is typed: the last searches, and four constant ones. */
  interface Props {
    recent: readonly string[];
    onpick: (query: string) => void;
  }

  let { recent, onpick }: Props = $props();

  const quick = $derived([
    m['search.quick.under30'](),
    m['search.quick.vegetarian'](),
    m['search.quick.breakfast'](),
    m['search.quick.dessert']()
  ]);
</script>

<div class="start">
  {#if recent.length > 0}
    <section>
      <h3 class="heading">{m['search.recent']()}</h3>
      <ul class="plain">
        {#each recent as one (one)}
          <li>
            <button type="button" class="again" onclick={() => onpick(one)}>↩ {one}</button>
          </li>
        {/each}
      </ul>
    </section>
  {/if}
  <!-- Four constant searches, not a personalised shelf: a panel whose
       contents change before anything is typed is one nobody can learn. -->
  <section>
    <h3 class="heading">{m['search.quick']()}</h3>
    <div class="quick">
      {#each quick as one (one)}
        <SearchFacet onclick={() => onpick(one)}>{one}</SearchFacet>
      {/each}
    </div>
  </section>
</div>

<style>
  .start {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .plain {
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .heading {
    padding-block: var(--space-3) var(--space-1);
    color: var(--text-muted);
    font-size: var(--text-xs);
    font-weight: var(--weight-medium);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .quick {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
  }

  .again {
    min-height: var(--control-sm);
    padding: 0 var(--space-1);
    border: 0;
    border-radius: var(--radius-full);
    background: none;
    color: var(--text);
    font: inherit;
    font-size: var(--text-sm);
    cursor: pointer;
  }
</style>
