<script lang="ts">
  import { Band } from '$ds';
  import { formatNumber, m } from '$shell/i18n';
  import { nbsp } from '$features/nutrition/format';

  import { gramsText, kcalText } from './format';
  import type { DiaryDay } from './types';

  /**
   * The day as a page's margin note (docs/diary-design.md §9.1): one number and one sentence that describes, never
   * judges; the band against the person's own Richtwert, if they set one; and protein, carbohydrate and fat as rows.
   * A day over the Richtwert looks like a day under it, and "remaining" never goes below zero.
   */
  interface Props {
    day: DiaryDay | null;
    kcalTarget: number | null;
    proteinTarget: number | null;
    headline: 'eaten' | 'remaining';
  }

  let { day, kcalTarget, proteinTarget, headline }: Props = $props();

  const kcal = $derived(day?.totals.kcal ?? 0);
  const flags = $derived({ atLeast: day?.atLeast ?? false, estimated: day?.estimated ?? false });
  const remaining = $derived(headline === 'remaining' && kcalTarget !== null);
  const left = $derived(kcalTarget === null ? 0 : Math.max(0, kcalTarget - kcal));

  const sentence = $derived.by(() => {
    if (kcalTarget === null) return null;
    if (kcal === 0) return m['diary.summary.target']({ kcal: formatNumber(kcalTarget) });

    const share = kcal / kcalTarget;

    return share < 0.9
      ? m['diary.summary.below']({ kcal: formatNumber(kcalTarget) })
      : share <= 1.1
        ? m['diary.summary.near']()
        : m['diary.summary.above']();
  });

  const macros = $derived([
    { name: m['diary.protein'](), value: day?.totals.protein ?? null, target: proteinTarget },
    { name: m['diary.carbohydrate'](), value: day?.totals.carbohydrate ?? null, target: null },
    { name: m['diary.fat'](), value: day?.totals.fat ?? null, target: null }
  ]);
</script>

<section class="summary" aria-label={m['diary.summary.label']()}>
  <p class="hero">
    {#if remaining}
      <span class="number">{formatNumber(Math.round(left))}{nbsp}kcal</span>
      <span class="word">{m['diary.summary.left']()}</span>
    {:else}
      <span class="number">{kcalText(kcal, flags)}</span>
      <span class="word">{m['diary.summary.eaten']()}</span>
    {/if}
  </p>

  {#if sentence}<p class="sentence">{sentence}</p>{/if}
  {#if (day?.typedKcal ?? 0) > 0}
    <p class="sentence">
      {m['diary.summary.typed']({
        kcal: kcalText(day?.typedKcal ?? 0, { atLeast: false, estimated: false })
      })}
    </p>
  {/if}

  {#if kcalTarget !== null}
    <Band
      value={kcal}
      reference={kcalTarget}
      atLeast={flags.atLeast}
      estimated={flags.estimated}
      label={m['diary.summary.band']({
        kcal: kcalText(kcal, flags),
        target: formatNumber(kcalTarget)
      })}
    />
  {/if}

  <dl class="macros">
    {#each macros as macro (macro.name)}
      <div class="macro">
        <dt>{macro.name}</dt>
        <dd>
          {macro.value == null ? '–' : gramsText(macro.value, flags.atLeast)}
          {#if macro.target}<span class="of"
              >{m['diary.summary.of']({ grams: gramsText(macro.target) })}</span
            >{/if}
        </dd>
      </div>
    {/each}
  </dl>
</section>

<style>
  .summary {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    margin-block-end: var(--space-8);
  }

  .hero {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    gap: var(--space-2);
    margin: 0;
  }

  .number {
    font-size: var(--text-3xl);
    font-weight: var(--weight-semibold);
    font-variant-numeric: tabular-nums;
    letter-spacing: -0.02em;
  }

  .word,
  .sentence,
  .of {
    color: var(--text-muted);
  }

  .sentence {
    margin: 0;
    font-size: var(--text-sm);
  }

  .macros {
    display: flex;
    flex-direction: column;
    margin: var(--space-2) 0 0;
  }

  .macro {
    display: flex;
    justify-content: space-between;
    gap: var(--space-4);
    padding-block: var(--space-2);
    border-block-end: 1px solid var(--border);
  }

  dt {
    color: var(--text-muted);
  }

  dd {
    margin: 0;
    font-variant-numeric: tabular-nums;
  }

  .of {
    margin-inline-start: var(--space-1);
    font-size: var(--text-sm);
  }
</style>
