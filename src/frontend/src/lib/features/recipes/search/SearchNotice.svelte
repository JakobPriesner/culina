<script lang="ts">
  import { resolve } from '$app/paths';

  import { Button } from '$ds';
  import { m } from '$shell/i18n';

  import type { Interpretation, SearchChip } from '../types';
  import { chipLabel } from './wording';

  /**
   * What the search had to change to find anything — said, never done quietly.
   *
   * Every way the server recovers from an empty answer is named here with the
   * way back from it: a corrected word with the original one tap away, a
   * reading that was set aside with a way to drop it for good, two readings
   * that contradict each other with a way to drop either. An unlabelled
   * fallback is a search box that lies.
   *
   * And when there is truly nothing, the end of the search is the start of a
   * task: a recipe nobody has is one to write down or bring in.
   */
  interface Props {
    interpretation: Interpretation | null;
    /** How many results there are, so "nothing" is only said once it is true. */
    total: number;
    /** What was typed, for naming the empty answer. */
    query: string;
    /**
     * Whether an empty answer offers to write or import the recipe. A page
     * with an empty state of its own says that itself.
     */
    offer?: boolean;
    onastyped: () => void;
    onremove: (chip: SearchChip) => void;
  }

  let { interpretation, total, query, offer = true, onastyped, onremove }: Props = $props();

  const conflict = $derived(interpretation?.conflict ?? []);
  const relaxed = $derived(interpretation?.relaxed ?? []);
  const empty = $derived(offer && total === 0 && query.trim().length > 0 && conflict.length === 0);
</script>

{#if interpretation?.correctedFrom}
  <p class="notice">
    {m['search.corrected']({ corrected: interpretation.freeText })}
    <button type="button" class="link" onclick={onastyped}>
      {m['search.corrected.undo']({ typed: interpretation.correctedFrom })}
    </button>
  </p>
{/if}

{#if relaxed.length > 0}
  <div class="notice">
    <p>{m['search.relaxed']({ labels: relaxed.map(chipLabel).join(', ') })}</p>
    <div class="actions">
      {#each relaxed as chip (`${chip.kind}:${chip.start}`)}
        <Button size="sm" variant="ghost" onclick={() => onremove(chip)}>
          {m['search.chip.remove']({ label: chipLabel(chip) })}
        </Button>
      {/each}
    </div>
  </div>
{/if}

{#if conflict.length === 2}
  <div class="notice" role="status">
    <p>
      {m['search.conflict']({ first: chipLabel(conflict[0]!), second: chipLabel(conflict[1]!) })}
    </p>
    <div class="actions">
      {#each conflict as chip (`${chip.kind}:${chip.start}`)}
        <Button size="sm" onclick={() => onremove(chip)}>
          {m['search.chip.remove']({ label: chipLabel(chip) })}
        </Button>
      {/each}
    </div>
  </div>
{:else if empty}
  <div class="offer">
    <p class="title">{m['search.empty.title']({ query: query.trim() })}</p>
    <p class="body">{m['search.empty.body']()}</p>
    <div class="actions">
      <Button variant="primary" size="sm" href={resolve('/(app)/recipes/new')}>
        {m['search.empty.create']()}
      </Button>
      <Button size="sm" href={resolve('/(app)/recipes/import')}>
        {m['search.empty.import']()}
      </Button>
    </div>
  </div>
{/if}

<style>
  .notice,
  .offer {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .offer {
    align-items: flex-start;
    padding-block: var(--space-6);
  }

  .title {
    color: var(--text);
    font-size: var(--text-base);
    font-weight: var(--weight-medium);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  .link {
    padding: 0;
    border: 0;
    background: none;
    color: var(--accent);
    font: inherit;
    text-decoration: underline;
    text-underline-offset: 0.15em;
    cursor: pointer;
  }
</style>
