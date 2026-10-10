<script lang="ts">
  import { formatNumber, m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';
  import type { Scaling } from '$features/recipes/surface/scaled.svelte';
  import { everyIngredient, type Ingredient, type RecipeReading } from '$features/recipes/types';

  import { wholeOrTenth } from './format';
  import { roundForLabel } from './rounding';
  import type { Nutrition, NutritionLine, NutritionStatus } from './types';

  /**
   * What became of each ingredient, so the rows add up to the headline. Amounts and grams follow the
   * servings on the page (the recipe is written for its own yield); the energy per row is per portion and
   * never scaled. Each row ends in a slot where a household's correction will sit.
   */
  interface Props {
    nutrition: Nutrition;
    recipe: RecipeReading;
    scaling: Scaling;
  }

  let { nutrition, recipe, scaling }: Props = $props();

  interface Row {
    readonly ingredient: Ingredient;
    readonly line: NutritionLine;
  }

  /** In recipe order; a line whose ingredient has just been edited away waits for the next answer. */
  const rows = $derived.by(() => {
    const written = new Map(everyIngredient(recipe).map((one) => [one.id, one] as const));

    return nutrition.ingredients.flatMap((line): Row[] => {
      const ingredient = written.get(line.ingredientId);

      return ingredient ? [{ ingredient, line }] : [];
    });
  });

  const counted = $derived(rows.filter((row) => row.line.status === 'counted'));
  const notCounted = $derived(rows.filter((row) => row.line.status !== 'counted'));

  const food = (line: NutritionLine) =>
    line.food ? (preferences.locale === 'de' ? line.food.nameDe : line.food.nameEn) : '';

  const gramsText = (line: NutritionLine) => {
    const grams = wholeOrTenth((line.grams ?? 0) * scaling.factor);

    // Grams as written are the recipe's own amount, already on the line above; only a conversion is news.
    switch (line.via) {
      case 'density':
        return m['nutrition.grams.density']({ grams });
      case 'eggSize':
        return m['nutrition.grams.eggSize']({ grams });
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
    excluded: m['nutrition.reason.excluded']
  };

  const reason = (line: NutritionLine) => (line.status === 'counted' ? '' : reasons[line.status]());

  const kcalText = (line: NutritionLine) =>
    m['nutrition.energyLine']({
      kcal: formatNumber(roundForLabel('energy', line.energyKcal ?? 0, false).value)
    });
</script>

{#snippet written(ingredient: Ingredient)}
  {@const amount = scaling.amountFor(ingredient)}
  <span class="written">
    {#if amount.text}<span class="amount">{amount.text}</span>{/if}
    {ingredient.name}
  </span>
{/snippet}

<section class="breakdown" aria-labelledby="nutrition-breakdown">
  <h3 id="nutrition-breakdown" class="title">{m['nutrition.breakdown.title']()}</h3>

  {#if !nutrition.complete}
    <p class="note">{m['nutrition.breakdown.partial']()}</p>
  {/if}

  {#if counted.length > 0}
    <h4 class="group">{m['nutrition.breakdown.counted']()}</h4>

    <ul class="rows">
      {#each counted as { ingredient, line } (ingredient.id)}
        <li class="row">
          <div class="what">
            {@render written(ingredient)}
            <span class="detail">
              {detail(line)}
            </span>
          </div>

          <div class="end">
            {#if line.energyKcal !== null}<span class="kcal">{kcalText(line)}</span>{/if}
          </div>
        </li>
      {/each}
    </ul>
  {/if}

  {#if notCounted.length > 0}
    <h4 class="group">{m['nutrition.breakdown.notCounted']()}</h4>

    <ul class="rows muted">
      {#each notCounted as { ingredient, line } (ingredient.id)}
        <li class="row">
          <div class="what">
            {@render written(ingredient)}
            <span class="detail">{reason(line)}</span>
          </div>

          <div class="end"></div>
        </li>
      {/each}
    </ul>
  {/if}
</section>

<style>
  .breakdown {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    margin-top: var(--space-6);
  }

  .title {
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.08em;
    text-transform: uppercase;
    color: var(--text-muted);
  }

  .note,
  .detail {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .group {
    margin-top: var(--space-2);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
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
    gap: var(--space-4);
    padding-block: var(--space-2);
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
  }

  .amount {
    font-variant-numeric: tabular-nums;
    font-weight: var(--weight-medium);
    white-space: nowrap;
  }

  /* Where a correction will go; the number keeps its place. */
  .end {
    display: flex;
    align-items: baseline;
    gap: var(--space-3);
    flex-shrink: 0;
  }

  .kcal {
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
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
