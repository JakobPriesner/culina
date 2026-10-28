<script lang="ts">
  import { Meter } from '$ds';
  import { formatNumber, m } from '$shell/i18n';

  import Figure from './Figure.svelte';
  import { gramsText, kcalNumber, kcalText } from './format';
  import type { DiaryDay } from './types';

  /**
   * What the day adds up to (docs/food-log-redesign.md §4.1): one number that dominates, one track against the
   * person's own Richtwert if they set one, one sentence that says it in words, and protein, carbohydrate and fat in
   * three columns. A day over the Richtwert looks like a day under it; nothing here judges. Tapping the number
   * switches between eaten and left for the moment; the setting says which it opens on.
   */
  interface Props {
    day: DiaryDay | null;
    kcalTarget: number | null;
    proteinTarget: number | null;
    headline: 'eaten' | 'remaining';
  }

  let { day, kcalTarget, proteinTarget, headline }: Props = $props();

  // The setting is where it starts; a tap changes it until the page is left.
  let mode = $derived<'eaten' | 'remaining'>(headline);

  const kcal = $derived(day?.totals.kcal ?? 0);
  const flags = $derived({ atLeast: day?.atLeast ?? false, estimated: day?.estimated ?? false });
  const partly = $derived(flags.atLeast || flags.estimated);
  const showsLeft = $derived(mode === 'remaining' && kcalTarget !== null);
  const left = $derived(kcalTarget === null ? 0 : Math.max(0, Math.round(kcalTarget - kcal)));
  const over = $derived(kcalTarget === null ? 0 : Math.max(0, Math.round(kcal - kcalTarget)));

  const capitalized = (text: string) => text.charAt(0).toUpperCase() + text.slice(1);

  /** The one sentence: what is left or over as a plain number, or what was eaten when the number shows what is left. */
  const sentence = $derived.by(() => {
    const parts: string[] = [];

    if (kcalTarget !== null && kcal === 0) {
      parts.push(m['diary.summary.target']({ kcal: formatNumber(kcalTarget) }));
    } else if (kcalTarget !== null && showsLeft) {
      parts.push(
        m['diary.summary.eatenOf']({
          eaten: kcalNumber(kcal, flags.atLeast),
          target: formatNumber(kcalTarget)
        })
      );
    } else if (kcalTarget !== null) {
      parts.push(
        left > 0
          ? m['diary.summary.toTarget']({ kcal: formatNumber(left) })
          : over > 0
            ? m['diary.summary.over']({ kcal: formatNumber(over) })
            : m['diary.summary.at']()
      );
    }

    if (partly && kcal > 0) parts.push(m['diary.summary.partly']());

    return parts.length ? capitalized(parts.join(' · ')) : null;
  });

  const macros = $derived([
    { name: m['diary.protein'](), value: day?.totals.protein ?? null, target: proteinTarget },
    { name: m['diary.carbohydrate'](), value: day?.totals.carbohydrate ?? null, target: null },
    { name: m['diary.fat'](), value: day?.totals.fat ?? null, target: null }
  ]);
</script>

{#snippet hero()}
  <span class="number">
    {#if showsLeft}{formatNumber(left)}{:else}<Figure {kcal} {...flags} unit={false} />{/if}
  </span>
  <span class="unit">kcal</span>
  <span class="word"
    >{showsLeft ? m['diary.summary.left']() : m['diary.summary.eaten']()}{#if kcalTarget !== null}
      <!-- Says the number can be turned over: eaten, left. -->
      <svg
        class="swap"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="2"
        aria-hidden="true"
        ><path d="m8 9 4-4 4 4M8 15l4 4 4-4" stroke-linecap="round" stroke-linejoin="round" /></svg
      >{/if}</span
  >
{/snippet}

<section class="summary" aria-label={m['diary.summary.label']()}>
  <div class="top">
    {#if kcalTarget !== null}
      <button
        class="hero"
        type="button"
        title={m['diary.summary.switch']()}
        onclick={() => (mode = mode === 'eaten' ? 'remaining' : 'eaten')}>{@render hero()}</button
      >
      <span class="of">{m['diary.summary.ofTarget']({ kcal: formatNumber(kcalTarget) })}</span>
    {:else}
      <p class="hero">{@render hero()}</p>
    {/if}
  </div>

  {#if kcalTarget !== null}
    <Meter
      value={kcal}
      reference={kcalTarget}
      label={`${m['diary.summary.band']({ kcal: kcalText(kcal, flags), target: formatNumber(kcalTarget) })}. ${sentence ?? ''}`}
    />
  {/if}

  {#if sentence}<p class="sentence">{sentence}</p>{/if}

  <dl class="macros">
    {#each macros as macro (macro.name)}
      <div class="macro">
        <dt>{macro.name}</dt>
        <dd>
          <span class="value"
            >{macro.value == null ? '–' : gramsText(macro.value, flags.atLeast)}</span
          >
          {#if macro.target}<span class="target"
              >{m['diary.summary.of']({ grams: gramsText(macro.target) })}</span
            >{/if}
        </dd>
        {#if macro.target}
          <dd class="track">
            <Meter
              size="thin"
              value={macro.value ?? 0}
              reference={macro.target}
              label={`${macro.name}: ${macro.value == null ? '–' : gramsText(macro.value)} ${m['diary.summary.of']({ grams: gramsText(macro.target) })}`}
            />
          </dd>
        {/if}
      </div>
    {/each}
  </dl>
</section>

<style>
  .summary {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .top {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    justify-content: space-between;
    gap: var(--space-2) var(--space-4);
  }

  /* The one number on the page. */
  .hero {
    display: inline-flex;
    align-items: baseline;
    gap: var(--space-1);
    margin: 0;
    padding: 0;
    border: 0;
    background: none;
    color: var(--text);
    font: inherit;
    text-align: start;
  }

  /* A control, so it looks touchable on hover and press, without moving the number from its edge. */
  button.hero {
    margin: calc(var(--space-1) * -1) calc(var(--space-2) * -1);
    padding: var(--space-1) var(--space-2);
    border-radius: var(--radius-md);
    cursor: pointer;
    transition: background-color var(--duration-fast) var(--ease-out);
  }

  @media (hover: hover) {
    button.hero:hover {
      background: var(--surface-hover);
    }
  }

  button.hero:active {
    background: var(--surface-selected);
  }

  .swap {
    inline-size: 0.9em;
    block-size: 0.9em;
    margin-inline-start: 0.2em;
    vertical-align: -0.1em;
  }

  button.hero:focus-visible {
    outline: 2px solid var(--border-focus);
    outline-offset: 4px;
  }

  /* The mark stays light beside the one big number: a hint, not a second number. */
  .number :global(.mark) {
    font-size: 0.6em;
    vertical-align: 0.2em;
  }

  .number {
    font-size: var(--text-3xl);
    font-weight: var(--weight-semibold);
    font-variant-numeric: tabular-nums;
    letter-spacing: -0.02em;
    line-height: 1;
  }

  .unit {
    font-size: var(--text-lg);
    font-weight: var(--weight-medium);
  }

  .word,
  .of,
  .sentence,
  dt,
  .target {
    color: var(--text-muted);
  }

  .word {
    margin-inline-start: var(--space-1);
  }

  .of {
    font-variant-numeric: tabular-nums;
  }

  .sentence {
    margin: 0;
    font-size: var(--text-sm);
  }

  /*
   * One figure each under its label, parted from the day's number by a hairline. Each takes what its longest word
   * needs and the rest is shared, so "Kohlenhydrate" stays whole; with larger text the last wraps to a second row.
   */
  .macros {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-3) var(--space-4);
    margin: var(--space-3) 0 0;
    padding-block-start: var(--space-4);
    border-block-start: 1px solid var(--border);
  }

  .macro {
    display: flex;
    flex-direction: column;
    flex: 1 1 auto;
    gap: var(--space-1);
  }

  dt {
    font-size: var(--text-xs);
    overflow-wrap: normal;
  }

  dd {
    margin: 0;
    font-variant-numeric: tabular-nums;
  }

  .value {
    font-size: var(--text-lg);
    font-weight: var(--weight-medium);
  }

  /* Its own line: "von 110 g" beside the value breaks in a column a third of a phone wide. */
  .target {
    display: block;
    font-size: var(--text-xs);
  }

  .track {
    margin-block-start: var(--space-1);
  }
</style>
