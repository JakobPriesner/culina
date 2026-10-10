<script lang="ts">
  import { onDestroy, untrack } from 'svelte';

  import { resolve } from '$app/paths';
  import { Button, Disclosure, Icon } from '$ds';
  import { createScaling } from '$features/recipes/surface/scaled.svelte';
  import type { Recipe } from '$features/recipes/types';
  import { m } from '$shell/i18n';
  import { createLoadingState } from '$shell/loadingState.svelte';

  import {
    amountAndName,
    energyFigure,
    implausibleLines,
    isWholeRecipe,
    missingNames,
    perWords,
    withoutWords
  } from './headline';
  import NutritionBreakdown from './NutritionBreakdown.svelte';
  import NutritionLabel from './NutritionLabel.svelte';
  import NutritionSkeleton from './NutritionSkeleton.svelte';
  import { nutrition } from './stores/nutrition.svelte';

  /**
   * What a recipe has in it per portion, in one closed line that already says the headline, and open as the
   * label from a package and the working out behind it. It says what it covers: a figure for part of the
   * recipe is a lower bound and is written as one. Not part of the recipe surface, which keeps its layout
   * still while cooking.
   */
  interface Props {
    recipe: Recipe;
    /** The servings on the page: the working out follows the ingredient list, the per-portion figures do not move. */
    servings: number;
    /** Whose corrections count; null before a household is known. */
    householdId: string | null;
    /** Whether the working out is open; the page opens it from the recipe's meta line. */
    open?: boolean;
  }

  let { recipe, servings, householdId, open = $bindable(false) }: Props = $props();

  const scaling = createScaling(
    () => recipe,
    () => servings
  );

  const answer = $derived(nutrition.answerFor(recipe.id, householdId));
  const status = $derived(nutrition.statusFor(recipe.id, householdId));

  const loading = createLoadingState();

  onDestroy(() => loading.dispose());

  // The recipe's version is what an edit changes, and with it the answer.
  $effect(() => {
    void recipe.version;
    void nutrition.load(recipe.id, householdId);
  });

  $effect(() => {
    const waiting = status === 'idle' || status === 'loading';

    untrack(() => (waiting ? loading.start() : loading.stop()));
  });

  const nothingCounted = $derived(answer?.counted === 0);
  const without = $derived(answer ? withoutWords(missingNames(answer, recipe)) : null);
  const wholeRecipe = $derived(answer ? isWholeRecipe(answer) : false);

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
      <Button
        size="sm"
        variant="secondary"
        onclick={() => void nutrition.load(recipe.id, householdId)}
      >
        {m['error.retry']()}
      </Button>
    </div>
  {:else if answer}
    <Disclosure bind:open>
      {#snippet summary()}
        <span class="line">
          <span class="label">{m['nutrition.title']()}</span>

          {#if nothingCounted}
            <span class="nothing">{m['nutrition.nothing']()}</span>
          {:else}
            <span class="headline">
              <span class="figure">{energyFigure(answer)}</span>
              <span class="per">{perWords(answer)}</span>
              {#if without}
                <span class="without">· {without}</span>
              {/if}
            </span>
          {/if}

          {#if implausible[0]}
            <span class="hint"
              >· {m['nutrition.implausible.hint']({ what: implausible[0].what })}</span
            >
          {/if}
        </span>
      {/snippet}

      <!-- One column, so the table's values and the breakdown's energy share a right edge. -->
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
                  <a
                    href="{resolve('/(app)/recipes/[recipeId]/edit', {
                      recipeId: recipe.id
                    })}#ingredient-{one.id}">{m['nutrition.implausible.fix']()}</a
                  >
                </li>
              {/each}
            </ul>
          </div>
        {/if}

        {#if wholeRecipe}
          <p class="quiet">
            {m['nutrition.oneServing']()}
            <a href="{resolve('/(app)/recipes/[recipeId]/edit', { recipeId: recipe.id })}#yield"
              >{m['nutrition.setServings']()}</a
            >
          </p>
        {/if}

        {#if !nothingCounted}
          <NutritionLabel nutrition={answer} />
        {/if}

        <NutritionBreakdown nutrition={answer} {recipe} {scaling} {householdId} />

        <p class="source">{@render attribution()}</p>
      </div>
    </Disclosure>

    <!-- A closed disclosure prints only its summary, and the data may not travel without its credit. -->
    {#if !open}
      <p class="source paper">{@render attribution()}</p>
    {/if}
  {/if}
</section>

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
    scroll-margin-top: var(--space-16);
    margin-top: var(--space-8);
    padding-block: var(--space-4) var(--space-3);
    border-top: 1px solid var(--border);
  }

  .line {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    column-gap: var(--space-3);
    min-width: 0;
  }

  .label {
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.08em;
    text-transform: uppercase;
    color: var(--text-muted);
  }

  .headline,
  .nothing {
    font-weight: var(--weight-regular);
  }

  .figure {
    font-variant-numeric: tabular-nums;
    font-weight: var(--weight-medium);
    color: var(--text);
  }

  .per,
  .without,
  .hint,
  .nothing {
    color: var(--text-muted);
  }

  .notice {
    display: flex;
    gap: var(--space-3);
    margin-bottom: var(--space-4);
    padding: var(--space-3) var(--space-4);
    border: 1px solid var(--warning);
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
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .open {
    max-width: 40rem;
  }

  .source {
    margin-top: var(--space-4);
    color: var(--text-subtle);
    font-size: var(--text-xs);
  }

  .source a {
    color: inherit;
    text-decoration: underline;
  }

  .paper {
    display: none;
  }

  @media print {
    .nutrition {
      break-inside: avoid;
      margin-top: var(--space-4);
    }

    .unavailable {
      display: none;
    }

    .paper {
      display: block;
      margin-top: var(--space-1);
    }
  }
</style>
