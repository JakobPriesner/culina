<script lang="ts">
  import { ListRow } from '$ds';
  import { m } from '$shell/i18n';

  import Figure from './Figure.svelte';
  import { amountText } from './format';
  import type { DiaryEntry } from './types';

  /**
   * One thing eaten, as a row of its meal (docs/food-log-redesign.md §4.1): its name, how much, and its energy in the
   * meal's column of figures with how far to trust it (≈, ≥; ✎ for the person's own figures). A tap opens it to change
   * or remove.
   */
  interface Props {
    entry: DiaryEntry;
    onopen: (entry: DiaryEntry) => void;
  }

  let { entry, onopen }: Props = $props();

  const kcal = $derived(entry.figures.kcal);
</script>

<ListRow onclick={() => onopen(entry)}>
  <span class="label">{entry.label}</span>
  <span class="amount">
    {#if entry.source === 'typed'}<span class="pencil" aria-hidden="true">✎</span>{/if}
    {amountText(entry)}
  </span>
  {#snippet trailing()}
    {#if kcal == null}{m['diary.unknown']()}{:else}<Figure
        {kcal}
        atLeast={entry.atLeast}
        estimated={entry.estimated}
        unit={false}
      />{/if}
  {/snippet}
</ListRow>

<style>
  .label {
    font-weight: var(--weight-medium);
  }

  .amount {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .pencil {
    margin-inline-end: var(--space-1);
  }
</style>
