<script lang="ts">
  import { tick } from 'svelte';

  import { IconButton } from '$ds';
  import { explain } from '$shell/explain';
  import { formatNumber, m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';
  import { toaster } from '$shell/toaster.svelte';
  import type { Scaling } from '$features/recipes/surface/scaled.svelte';
  import { everyIngredient, type Ingredient, type RecipeReading } from '$features/recipes/types';

  import { wholeOrTenth } from './format';
  import NutritionCorrectionSheet from './NutritionCorrectionSheet.svelte';
  import { roundForLabel } from './rounding';
  import { nutrition as store } from './stores/nutrition.svelte';
  import type { Correction, FoodHit, Nutrition, NutritionLine, NutritionStatus } from './types';

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

  /** The row whose sheet is open; read from `rows` so the sheet shows what the answer now says. */
  let section = $state<HTMLElement>();
  let editing = $state<string | null>(null);

  const editedRow = $derived(rows.find((row) => row.ingredient.id === editing) ?? null);

  /** The same name on other lines of this recipe changes with it; the server folds names, this only stands in until it answers. */
  const sameName = (name: string) =>
    rows
      .filter((row) => row.ingredient.name.trim().toLowerCase() === name.trim().toLowerCase())
      .map((row) => row.ingredient.id);

  /** Applies a correction: the sheet closes, the rows change at once, and a refusal puts them back and says why. */
  async function correct(correction: Correction) {
    const row = editedRow;

    editing = null;

    if (!row || !householdId) {
      return;
    }

    const label = m['nutrition.change']({ name: row.ingredient.name });

    const { name } = row.ingredient;
    const failure = await store.correct(recipe.id, householdId, name, sameName(name), correction);

    // The row may have moved between the groups, which makes its button a new element.
    await tick();
    const buttons = section?.querySelectorAll<HTMLElement>('button') ?? [];

    [...buttons].find((button) => button.getAttribute('aria-label') === label)?.focus();

    toaster.show(
      failure
        ? { message: () => explain(failure), tone: 'danger' }
        : { message: () => saidAfter(name, correction) }
    );
  }

  const saidAfter = (name: string, correction: Correction) => {
    switch (correction.kind) {
      case 'food':
        return m['nutrition.correct.done']({ name, food: foodIn(correction.food) });
      case 'exclude':
        return m['nutrition.correct.doneExcluded']({ name });
      case 'default':
        return m['nutrition.correct.doneDefault']({ name });
    }
  };

  const choose = (hit: FoodHit) =>
    correct({ kind: 'food', food: { code: hit.code, nameDe: hit.nameDe, nameEn: hit.nameEn } });

  const counted = $derived(rows.filter((row) => row.line.status === 'counted'));
  const notCounted = $derived(rows.filter((row) => row.line.status !== 'counted'));

  const foodIn = (one: { nameDe: string; nameEn: string }) =>
    preferences.locale === 'de' ? one.nameDe : one.nameEn;

  const food = (line: NutritionLine) => (line.food ? foodIn(line.food) : '');

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
    excluded: m['nutrition.reason.excluded'],
    implausible: m['nutrition.reason.implausible']
  };

  const reason = (line: NutritionLine) => (line.status === 'counted' ? '' : reasons[line.status]());

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
      onclick={() => (editing = ingredient.id)}
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
            {@render change(ingredient)}
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

          <div class="end">{@render change(ingredient)}</div>
        </li>
      {/each}
    </ul>
  {/if}
</section>

<NutritionCorrectionSheet
  name={editedRow?.ingredient.name ?? null}
  line={editedRow?.line ?? null}
  onchoose={choose}
  onexclude={() => void correct({ kind: 'exclude' })}
  onreset={() => void correct({ kind: 'default' })}
  onclose={() => (editing = null)}
/>

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
