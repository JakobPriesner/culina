<script lang="ts">
  import { tick } from 'svelte';

  import type { AppError } from '$api';
  import { IconButton, VisuallyHidden } from '$ds';
  import { session } from '$features/auth/session.svelte';
  import { explain } from '$shell/explain';
  import { formatNumber, m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';
  import { toaster } from '$shell/toaster.svelte';
  import type { Scaling } from '$features/recipes/surface/scaled.svelte';
  import type { Ingredient, RecipeReading } from '$features/recipes/types';

  import { wholeOrTenth } from './format';
  import { perWords } from './headline';
  import { roundForLabel } from './rounding';
  import { createLineCorrection, foodIn } from './lineCorrection.svelte';
  import LineCorrectionSheet from './LineCorrectionSheet.svelte';
  import type { Nutrition, NutritionLine, NutritionReason, NutritionStatus } from './types';

  /**
   * What became of each ingredient, so the rows add up to the headline. Amounts and grams follow the
   * servings on the page (the recipe is written for its own yield); the energy per row is per portion and
   * never scaled. Each row ends in a quiet button to say what the ingredient really is: a correction is about
   * the name, so it is offered on every row, even one with no amount, and it holds for the household's other recipes too.
   */
  interface Props {
    nutrition: Nutrition;
    recipe: RecipeReading;
    scaling: Scaling;
    /** Whose answer a correction changes; null before a household is known, when there is nothing to correct. */
    householdId: string | null;
  }

  let { nutrition, recipe, scaling, householdId }: Props = $props();

  let section = $state<HTMLElement>();

  /** Moves focus back to the row's button and says how it went: the row may have moved between the groups, which makes its button a new element. */
  async function finish(label: string, failure: AppError | null, said: () => string) {
    await tick();
    const buttons = section?.querySelectorAll<HTMLElement>('button') ?? [];

    [...buttons].find((button) => button.getAttribute('aria-label') === label)?.focus();

    toaster.show(failure ? { message: () => explain(failure), tone: 'danger' } : { message: said });
  }

  const correction = createLineCorrection({
    recipe: () => recipe,
    nutrition: () => nutrition,
    householdId: () => householdId,
    after: finish
  });

  /** In recipe order; a line whose ingredient has just been edited away waits for the next answer. */
  const rows = $derived(correction.rows);
  const edit = (id: string) => correction.edit(id);

  const counted = $derived(rows.filter((row) => row.line.status === 'counted'));
  const notCounted = $derived(rows.filter((row) => row.line.status !== 'counted'));

  const food = (line: NutritionLine) => (line.food ? foodIn(line.food) : '');

  /** The table's own name, as the citation under the friendlier one; nothing when they are the same words. */
  const blsName = (line: NutritionLine) => {
    if (line.food?.source === 'package') {
      return m['nutrition.fromPackage']();
    }

    const name = line.food
      ? preferences.locale === 'de'
        ? line.food.nameDe
        : line.food.nameEn
      : '';

    return name && name !== food(line) ? m['nutrition.bls']({ name }) : null;
  };

  const gramsText = (line: NutritionLine) => {
    const grams = wholeOrTenth((line.grams ?? 0) * scaling.factor);

    // Grams as written are the recipe's own amount, already on the line above; only a conversion is news.
    switch (line.via) {
      case 'density':
        return m['nutrition.grams.density']({ grams });
      case 'eggSize':
        return m['nutrition.grams.eggSize']({ grams });
      case 'typicalWeight':
        return m['nutrition.grams.typical']({ grams });
      case 'householdWeight': {
        const from = inheritedName(line.weighedBy);

        if (from === null) {
          return m['nutrition.grams.household']({ grams });
        }

        return from
          ? m['nutrition.grams.inherited']({ grams, household: from })
          : m['nutrition.grams.inheritedSomewhere']({ grams });
      }
      default:
        return null;
    }
  };

  const detail = (line: NutritionLine) =>
    [m['nutrition.countedAs']({ food: food(line) }), gramsText(line)].filter(Boolean).join(' · ');

  /** Spelled out one by one: message keys must stay visible to the unused-key check. */
  const reasons: Record<Exclude<NutritionStatus, 'counted'>, () => string> = {
    amountNotInGrams: m['nutrition.reason.amountNotInGrams'],
    noAmount: m['nutrition.reason.noAmount'],
    unknownFood: m['nutrition.reason.unknownFood'],
    unknownKind: m['nutrition.reason.unknownKind'],
    excluded: m['nutrition.reason.excluded'],
    implausible: m['nutrition.reason.implausible']
  };

  const unitReasons: Record<NutritionReason, () => string> = {
    spoonOfSolid: m['nutrition.reason.spoonOfSolid'],
    volumeOfSolid: m['nutrition.reason.volumeOfSolid'],
    count: m['nutrition.reason.count'],
    householdUnit: m['nutrition.reason.householdUnit']
  };

  /** Whose weight or choice: null for the household's own, else the household it inherits it from ('' when the session does not know its name). */
  const inheritedName = (by: string | null | undefined) =>
    !by || by === householdId ? null : (session.householdName(by) ?? '');

  const reason = (line: NutritionLine) => {
    if (line.status === 'counted') {
      return '';
    }

    const excludedBy = line.status === 'excluded' ? inheritedName(line.chosenBy) : null;

    if (excludedBy !== null) {
      return excludedBy
        ? m['nutrition.correct.chosenIn']({ household: excludedBy })
        : m['nutrition.correct.chosenAbove']();
    }

    // A unit with no reason given is an older answer: the general words still say it.
    return line.status === 'amountNotInGrams' && line.reason
      ? unitReasons[line.reason]()
      : reasons[line.status]();
  };

  const kcalText = (line: NutritionLine) =>
    m['nutrition.energyLine']({
      kcal: formatNumber(roundForLabel('energy', line.energyKcal ?? 0, false).value)
    });
</script>

{#snippet change(ingredient: Ingredient)}
  {#if householdId}
    <IconButton
      label={m['nutrition.change']({ name: ingredient.name })}
      size="sm"
      onclick={() => edit(ingredient.id)}
    >
      <!-- A pencil: this is named differently, not removed. -->
      <svg
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="2"
        stroke-linecap="round"
        stroke-linejoin="round"
      >
        <path d="M4 20h4L19 9l-4-4L4 16zM13.5 6.5l4 4" />
      </svg>
    </IconButton>
  {/if}
{/snippet}

{#snippet written(ingredient: Ingredient)}
  {@const amount = scaling.amountFor(ingredient)}
  <span class="written">
    {#if amount.text}<span class="amount">{amount.text}</span>{/if}
    {ingredient.name}
  </span>
{/snippet}

<section bind:this={section} class="breakdown" aria-labelledby="nutrition-breakdown">
  <h3 id="nutrition-breakdown" class="title">{m['nutrition.breakdown.title']()}</h3>

  <p class="basis">
    {m['nutrition.breakdown.basis']({ yield: scaling.currentYieldLabel, per: perWords(nutrition) })}
  </p>

  {#if !nutrition.complete}
    <p class="note">{m['nutrition.breakdown.partial']()}</p>
  {/if}

  {#if counted.length > 0}
    <h4 class="group">
      {m['nutrition.breakdown.counted']({ count: formatNumber(counted.length) })}
    </h4>

    <ul class="rows">
      {#each counted as { ingredient, line } (ingredient.id)}
        <li class="row">
          <div class="what">
            {@render written(ingredient)}
            <span class="detail">
              {detail(line)}
            </span>
            {#if blsName(line)}<span class="bls">{blsName(line)}</span>{/if}
            {#if line.via === 'typicalWeight' && line.source}
              <span class="bls">{m['nutrition.weight.source']({ source: line.source })}</span>
            {/if}
          </div>

          <div class="end">
            {#if line.energyKcal !== null}<span class="kcal">{kcalText(line)}</span>{/if}
            {@render change(ingredient)}
          </div>
        </li>
      {/each}
    </ul>
  {/if}

  {#if notCounted.length > 0}
    <h4 class="group">
      {m['nutrition.breakdown.notCounted']({ count: formatNumber(notCounted.length) })}
    </h4>

    <ul class="rows muted">
      {#each notCounted as { ingredient, line } (ingredient.id)}
        <li class="row">
          <div class="what">
            {@render written(ingredient)}
            <span class="detail">{reason(line)}</span>
            {#if householdId && line.unitKey}
              <button
                type="button"
                class="weigh"
                onclick={() => correction.edit(ingredient.id, true)}
              >
                {m['nutrition.weight.set']()}<VisuallyHidden>{ingredient.name}</VisuallyHidden>
              </button>
            {/if}
          </div>

          <div class="end">{@render change(ingredient)}</div>
        </li>
      {/each}
    </ul>
  {/if}
</section>

<LineCorrectionSheet {correction} {householdId} />

<style>
  .breakdown {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    min-width: 0;
  }

  .title {
    font-size: var(--text-base);
    font-weight: var(--weight-semibold);
    color: var(--text);
  }

  .basis,
  .note,
  .detail {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .basis {
    font-size: var(--text-xs);
  }

  .note {
    margin-bottom: var(--space-2);
    font-size: var(--text-xs);
    line-height: var(--leading-normal);
  }

  .bls {
    color: var(--text-subtle);
    font-size: var(--text-xs);
    overflow-wrap: anywhere;
  }

  .group {
    margin-top: var(--space-2);
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
  }

  .rows {
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .row {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: var(--space-3);
    padding-block: var(--space-4);
    border-bottom: 1px solid var(--border);
  }

  .row:last-child {
    border-bottom: 0;
  }

  .what {
    display: flex;
    flex-direction: column;
    min-width: 0;
  }

  .written {
    overflow-wrap: anywhere;
    font-weight: var(--weight-medium);
  }

  .amount {
    font-variant-numeric: tabular-nums;
    font-weight: var(--weight-medium);
    white-space: nowrap;
  }

  /* The number keeps its place; the button is the same width on every row, so the column of kcal stays aligned. */
  .end {
    display: flex;
    align-items: baseline;
    gap: var(--space-1);
    flex-shrink: 0;
  }

  /* Quiet, and centred on the row without making it taller than its two lines of text. */
  .end :global(button) {
    align-self: center;
    margin-block: calc(var(--space-2) * -1);
  }

  @media print {
    .end :global(button) {
      display: none;
    }
  }

  /* A text button for the one thing a row can still be told, quiet like the rest of the row. */
  .weigh {
    align-self: flex-start;
    padding: 0;
    border: 0;
    background: none;
    color: var(--accent);
    font: inherit;
    font-size: var(--text-sm);
    text-decoration: underline;
    text-underline-offset: 0.2em;
    cursor: pointer;
  }

  @media print {
    .weigh {
      display: none;
    }
  }

  .kcal {
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
    font-size: var(--text-sm);
  }

  /* Nothing is wrong with these rows; they are just not in the sum. */
  .muted .written {
    color: var(--text-muted);
  }

  @media print {
    .breakdown {
      break-inside: avoid;
    }
  }
</style>
