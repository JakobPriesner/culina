<script lang="ts">
  import { onDestroy, untrack } from 'svelte';

  import { resolve } from '$app/paths';
  import { Button, Disclosure, Icon, VisuallyHidden } from '$ds';
  import { createScaling } from '$features/recipes/surface/scaled.svelte';
  import type { Recipe, RecipeReading } from '$features/recipes/types';
  import { formatNumber, m } from '$shell/i18n';
  import { createLoadingState } from '$shell/loadingState.svelte';

  import {
    amountAndName,
    implausibleLines,
    isWholeRecipe,
    missingNames,
    perWords,
    withoutWords
  } from './headline';
  import { labelNumber, nbsp } from './format';
  import { roundForLabel } from './rounding';
  import type { NutritionValue } from './types';
  import CalorieInfo from './CalorieInfo.svelte';
  import NutritionBreakdown from './NutritionBreakdown.svelte';
  import NutritionLabel from './NutritionLabel.svelte';
  import NutritionSkeleton from './NutritionSkeleton.svelte';
  import { nutrition } from './stores/nutrition.svelte';

  /**
   * A calm overview with independently accessible calorie info and a disclosure for the full label and
   * ingredient breakdown. Lower bounds stay in accessible names and the info panel. Per-portion values
   * do not change when the ingredients and servings are scaled together.
   */
  interface Props {
    /** A shared recipe is read as it is, with the token for its id. */
    recipe: Recipe | RecipeReading;
    /** The servings on the page: the working out follows the ingredient list, the per-portion figures do not move. */
    servings: number;
    /** Whose corrections count; null before a household is known. */
    householdId: string | null;
    /** Whether the working out is open; the page opens it from the recipe's meta line. */
    open?: boolean;
    /** A visitor with a share link: the figures only, no household's corrections and no way into an editor. */
    readonly?: boolean;
    /** An inherited recipe allows household food corrections, but not editing the original recipe. */
    editable?: boolean;
  }

  let {
    recipe,
    servings,
    householdId,
    open = $bindable(false),
    readonly = false,
    editable = true
  }: Props = $props();

  const scaling = createScaling(
    () => recipe,
    () => servings
  );

  const answer = $derived(
    readonly ? nutrition.answerForShared(recipe.id) : nutrition.answerFor(recipe.id, householdId)
  );
  const status = $derived(
    readonly ? nutrition.statusForShared(recipe.id) : nutrition.statusFor(recipe.id, householdId)
  );

  const ask = () =>
    void (readonly ? nutrition.loadShared(recipe.id) : nutrition.load(recipe.id, householdId));

  const loading = createLoadingState();

  onDestroy(() => loading.dispose());

  // The recipe's version is what an edit changes, and with it the answer.
  $effect(() => {
    void ('version' in recipe && recipe.version);
    ask();
  });

  $effect(() => {
    const waiting = status === 'idle' || status === 'loading';

    untrack(() => (waiting ? loading.start() : loading.stop()));
  });

  const nothingCounted = $derived(answer?.counted === 0);
  const without = $derived(answer ? withoutWords(missingNames(answer, recipe)) : null);
  const wholeRecipe = $derived(answer ? isWholeRecipe(answer) : false);

  const macros = $derived(
    answer
      ? [
          { label: m['nutrition.protein'](), value: answer.values.protein },
          { label: m['nutrition.carbohydrate'](), value: answer.values.carbohydrate },
          { label: m['nutrition.fat'](), value: answer.values.fat }
        ]
      : []
  );

  // A lower bound rounded to zero is unknown, just as in the full nutrition label.
  const unknownMacro = (value: NutritionValue) =>
    value.atLeast && roundForLabel('macro', value.value, true).value === 0;

  const implausible = $derived(
    answer
      ? implausibleLines(answer, recipe).map(({ ingredient }) => ({
          id: ingredient.id,
          what: amountAndName(scaling.amountFor(ingredient).text, ingredient.name)
        }))
      : []
  );
</script>

<section id="nutrition" class="nutrition" aria-label={m['nutrition.title']()}>
  {#if loading.showing}
    <NutritionSkeleton />
  {:else if status === 'failed'}
    <div class="unavailable" role="status">
      <p>{m['nutrition.unavailable']()}</p>
      <Button size="sm" variant="secondary" onclick={ask}>{m['error.retry']()}</Button>
    </div>
  {:else if answer}
    <div class="summaryOverview">
      <span class="line">
        <span class="heading">
          <span class="label">{m['nutrition.title']()}</span>
          <span class="status"
            >{answer.complete
              ? m['nutrition.status.complete']()
              : m['nutrition.status.partial']()}</span
          >
        </span>

        {#if nothingCounted}
          <span class="nothing">{m['nutrition.nothing']()}</span>
        {:else}
          <span class="overview">
            <span class="headline">
              <span class="figure">
                {#if answer.values.energyKcal.atLeast}
                  <span class="bound"
                    ><VisuallyHidden>{m['nutrition.atLeast']()}{nbsp}</VisuallyHidden></span
                  >
                {/if}
                <span class="energyNumber">{labelNumber('energy', answer.values.energyKcal)}</span>
                <span class="energyUnit">kcal</span>
                <CalorieInfo
                  atLeast={answer.values.energyKcal.atLeast}
                  estimated={answer.values.energyKcal.estimated}
                  basis={perWords(answer)}
                  detail={without}
                />
              </span>
              <span class="per">{perWords(answer)}</span>
              {@render amountHint()}
            </span>

            <span class="macros">
              {#each macros as macro (macro.label)}
                <span class="macro">
                  <span class="macroLabel">{macro.label}</span>
                  <span class="macroFigure">
                    {#if unknownMacro(macro.value)}
                      <span class="macroNumber" aria-hidden="true">–</span>
                      <VisuallyHidden>{m['nutrition.notKnown']()}</VisuallyHidden>
                    {:else}
                      {#if macro.value.atLeast}
                        <span class="macroBound">{m['nutrition.atLeast']()}</span>
                        <span class="macroNumber"
                          >{labelNumber('macro', macro.value)}{nbsp}<span class="macroUnit">g</span
                          ></span
                        >
                      {:else}
                        <span class="macroNumber"
                          >{labelNumber('macro', macro.value)}{nbsp}<span class="macroUnit">g</span
                          ></span
                        >
                      {/if}
                    {/if}
                  </span>
                </span>
              {/each}
            </span>
          </span>
        {/if}

        {#if nothingCounted}{@render amountHint()}{/if}
      </span>
    </div>

    <Disclosure bind:open>
      {#snippet summary()}
        <span class="affordance"
          >{open ? m['nutrition.details.hide']() : m['nutrition.details.show']()}</span
        >
      {/snippet}

      <div class="open">
        {#if implausible.length > 0}
          <div class="notice">
            <Icon size="sm">
              <svg
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              >
                <circle cx="12" cy="12" r="9" />
                <path d="M12 8v5M12 16.5v.01" />
              </svg>
            </Icon>

            <ul>
              {#each implausible as one (one.id)}
                <li>
                  {m['nutrition.implausible.line']({ what: one.what })}
                  {#if !readonly && editable}
                    <a
                      href="{resolve('/(app)/recipes/[recipeId]/edit', {
                        recipeId: recipe.id
                      })}#ingredient-{one.id}">{m['nutrition.implausible.fix']()}</a
                    >
                  {/if}
                </li>
              {/each}
            </ul>
          </div>
        {/if}

        {#if wholeRecipe}
          <p class="quiet">
            {m['nutrition.oneServing']()}
            {#if !readonly && editable}
              <a href="{resolve('/(app)/recipes/[recipeId]/edit', { recipeId: recipe.id })}#yield"
                >{m['nutrition.setServings']()}</a
              >
            {/if}
          </p>
        {/if}

        <p class="coverage">
          {m['nutrition.summary.coverage']({
            counted: formatNumber(answer.counted),
            lines: formatNumber(answer.lines)
          })}
        </p>

        <div class="details" class:empty={nothingCounted}>
          {#if !nothingCounted}
            <div class="values">
              <NutritionLabel nutrition={answer} />
            </div>
          {/if}

          <NutritionBreakdown
            nutrition={answer}
            {recipe}
            {scaling}
            householdId={readonly ? null : householdId}
          />
        </div>

        <p class="source">{@render attribution()}</p>
      </div>
    </Disclosure>

    <!-- The closed overview still prints with its source. -->
    {#if !open}
      <p class="source paper">{@render attribution()}</p>
    {/if}
  {/if}
</section>

{#snippet amountHint()}
  {#if implausible[0]}
    <span class="hint">· {m['nutrition.implausible.hint']({ what: implausible[0].what })}</span>
  {/if}
{/snippet}

{#snippet attribution()}
  {#if answer}
    {m['nutrition.source']()}
    {answer.source.publisher},
    <a href="https://blsdb.de" rel="noopener">{answer.source.name} {answer.source.version}</a>
    ({answer.source.licence})
  {/if}
{/snippet}

<style>
  .nutrition {
    container-type: inline-size;
    scroll-margin-top: calc(var(--header-inset) + var(--space-4));
    margin-top: var(--space-8);
    border-radius: var(--space-6);
    background: var(--surface-raised);
  }

  .summaryOverview {
    padding: var(--space-8) var(--space-8) var(--space-6);
  }

  .nutrition :global(.summary) {
    position: relative;
    margin-inline: var(--space-8);
    padding: var(--space-4) 0 var(--space-6);
    border-top: 1px solid var(--border);
    gap: var(--space-2);
    border-radius: 0 0 var(--space-6) var(--space-6);
  }

  .nutrition :global(.chevron) {
    flex: none;
    order: 1;
    margin-inline-start: auto;
    color: var(--accent);
  }

  .nutrition :global(.content) {
    padding: 0;
  }

  .line {
    display: grid;
    gap: var(--space-6);
    flex: 1;
    min-width: 0;
  }

  .heading {
    display: flex;
    align-items: baseline;
    flex-wrap: wrap;
    justify-content: space-between;
    gap: var(--space-2) var(--space-4);
  }

  .label {
    font-size: var(--text-xl);
    font-weight: var(--weight-semibold);
    color: var(--text);
  }

  .headline,
  .nothing {
    font-weight: var(--weight-regular);
  }

  .figure {
    font-variant-numeric: tabular-nums;
    color: var(--text);
    line-height: var(--leading-tight);
    white-space: nowrap;
  }

  .bound,
  .energyUnit {
    font-size: var(--text-base);
    font-weight: var(--weight-regular);
    color: var(--text-muted);
  }

  .energyNumber {
    font-size: var(--text-display);
    font-weight: var(--weight-semibold);
    letter-spacing: -0.045em;
  }

  .overview {
    display: grid;
    gap: var(--space-6);
    min-width: 0;
  }

  .headline {
    display: block;
  }

  .per {
    margin-inline-start: var(--space-2);
  }

  .status {
    font-size: var(--text-xs);
    font-weight: var(--weight-regular);
    color: var(--text-muted);
  }

  .macros {
    display: grid;
    grid-template-columns: repeat(3, minmax(0, 1fr));
    gap: var(--space-4);
  }

  .macro {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    min-width: 0;
  }

  .macroLabel {
    font-size: var(--text-sm);
    font-weight: var(--weight-regular);
    color: var(--text-muted);
    overflow-wrap: anywhere;
  }

  .macroFigure {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    gap: var(--space-1);
    font-variant-numeric: tabular-nums;
  }

  .macroBound {
    font-size: var(--text-xs);
    font-weight: var(--weight-regular);
    color: var(--text-muted);
  }

  .macroNumber {
    font-size: var(--text-xl);
    font-weight: var(--weight-semibold);
    letter-spacing: -0.025em;
    white-space: nowrap;
  }

  .macroUnit {
    font-size: var(--text-sm);
    font-weight: var(--weight-regular);
    color: var(--text-muted);
  }

  .affordance {
    flex: 1;
    color: var(--accent);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
  }

  .nutrition :global(.summary:hover .affordance) {
    text-decoration: underline;
    text-underline-offset: 0.2em;
  }

  .per,
  .hint,
  .nothing {
    color: var(--text-muted);
  }

  .headline,
  .hint,
  .nothing {
    overflow-wrap: anywhere;
    line-height: var(--leading-normal);
  }

  .hint {
    display: block;
    margin-top: var(--space-2);
    font-size: var(--text-sm);
  }

  .notice {
    display: flex;
    gap: var(--space-3);
    margin-bottom: var(--space-4);
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-md);
    background: var(--warning-subtle);
    font-size: var(--text-sm);
  }

  .notice ul {
    margin: var(--space-1) 0 0;
    padding: 0;
    list-style: none;
  }

  .notice a,
  .quiet a {
    color: inherit;
    text-decoration: underline;
  }

  .quiet {
    margin-bottom: var(--space-3);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .unavailable {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2) var(--space-4);
    min-height: var(--control-sm);
    padding: var(--card-padding);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .open {
    padding: 0 var(--space-8) var(--space-8);
  }

  .coverage {
    margin-bottom: var(--space-4);
    font-size: var(--text-sm);
    color: var(--text-muted);
  }

  .details {
    display: grid;
    grid-template-columns: minmax(0, 1fr);
    gap: var(--space-8);
    align-items: start;
  }

  .values {
    min-width: 0;
  }

  .source {
    margin-top: var(--space-6);
    padding-top: var(--space-4);
    border-top: 1px solid var(--border);
    color: var(--text-subtle);
    font-size: var(--text-xs);
    overflow-wrap: anywhere;
  }

  .source a {
    color: inherit;
    text-decoration: underline;
  }

  .paper {
    display: none;
  }

  @container (width >= 52rem) {
    .overview {
      grid-template-columns: minmax(0, 2fr) minmax(0, 3fr);
      gap: var(--space-12);
      align-items: center;
    }

    .headline .per {
      display: block;
      margin: var(--space-1) 0 0;
    }

    .details:not(.empty) {
      grid-template-columns: minmax(0, 2fr) minmax(0, 3fr);
      gap: var(--space-12);
    }
  }

  @container (width < 32rem) {
    .summaryOverview {
      padding: var(--space-6) var(--space-6) var(--space-4);
    }

    .nutrition :global(.summary) {
      margin-inline: var(--space-6);
      padding: var(--space-3) 0 var(--space-5);
    }

    .open {
      padding: 0 var(--space-6) var(--space-6);
    }

    .heading {
      flex-direction: column;
      gap: var(--space-1);
    }

    .headline .per {
      display: block;
      margin: var(--space-1) 0 0;
    }

    .macros {
      grid-template-columns: minmax(0, 1fr) max-content minmax(0, 1fr);
      gap: var(--space-3);
    }

    .macroLabel {
      font-size: var(--text-xs);
      white-space: nowrap;
    }

    .macroFigure {
      flex-direction: column;
      gap: 0;
    }

    .macroNumber {
      font-size: var(--text-lg);
    }
  }

  @media print {
    .nutrition {
      break-inside: avoid;
      margin-top: var(--space-4);
      border: 0;
      border-top: 1px solid var(--border);
      border-radius: 0;
      background: none;
    }

    .nutrition :global(.summary) {
      display: none;
    }

    .summaryOverview,
    .open {
      padding: var(--space-3) 0;
    }

    .unavailable {
      display: none;
    }

    .paper {
      display: block;
      margin-top: var(--space-1);
      padding-top: 0;
      border: 0;
    }

    .macros,
    .affordance,
    .status {
      display: none;
    }

    .energyNumber {
      font-size: var(--text-xl);
    }
  }
</style>
