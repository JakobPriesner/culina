<script lang="ts">
  import { m } from '$shell/i18n';

  import { amountText, kcalText } from './format';
  import type { DiaryEntry } from './types';

  /**
   * One thing eaten, on the ruled page: its name, how much, and its energy with the word that says how far to trust it
   * ("mind.", "ca.", or "von dir eingetragen" for the person's own figures). A tap opens it to change or remove.
   */
  interface Props {
    entry: DiaryEntry;
    onopen: (entry: DiaryEntry) => void;
  }

  let { entry, onopen }: Props = $props();

  const kcal = $derived(entry.figures.kcal);
</script>

<li>
  <button class="row" type="button" onclick={() => onopen(entry)}>
    <span class="what">
      <span class="label">{entry.label}</span>
      <span class="amount">
        {#if entry.source === 'typed'}<span class="pencil" aria-hidden="true">✎</span>{/if}
        {amountText(entry)}
      </span>
    </span>
    <span class="kcal">{kcal == null ? m['diary.unknown']() : kcalText(kcal, entry)}</span>
  </button>
</li>

<style>
  .row {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: var(--space-4);
    inline-size: 100%;
    min-block-size: var(--control-sm);
    padding: var(--space-2) 0;
    border: 0;
    border-block-end: 1px solid var(--border);
    background: transparent;
    color: var(--text);
    font: inherit;
    text-align: start;
    cursor: pointer;
  }

  .row:hover .label {
    text-decoration: underline;
    text-decoration-color: var(--border-strong);
  }

  .what {
    display: flex;
    flex-direction: column;
    min-inline-size: 0;
  }

  .label {
    font-family: var(--font-editorial);
    overflow-wrap: anywhere;
  }

  .amount {
    color: var(--text-muted);
    font-size: var(--text-xs);
  }

  .pencil {
    margin-inline-end: var(--space-1);
  }

  .kcal {
    flex-shrink: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-variant-numeric: tabular-nums;
  }
</style>
