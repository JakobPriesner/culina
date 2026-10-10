<script lang="ts">
  import { onDestroy, untrack } from 'svelte';

  import { Button, Disclosure } from '$ds';
  import { createScaling } from '$features/recipes/surface/scaled.svelte';
  import type { Recipe } from '$features/recipes/types';
  import { formatNumber, m } from '$shell/i18n';
  import { createLoadingState } from '$shell/loadingState.svelte';

  import { labelNumber, withBound } from './format';
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
  }

  let { recipe, servings, householdId }: Props = $props();

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

  let open = $state(false);

  const per = $derived(
    answer?.per === 'piece' ? m['nutrition.perPiece']() : m['nutrition.perServing']()
  );
  const energy = $derived(answer?.values.energyKcal);
  const nothingCounted = $derived(answer?.counted === 0);
</script>

<section class="nutrition" aria-label={m['nutrition.title']()}>
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
  {:else if answer && energy}
    <Disclosure bind:open>
      {#snippet summary()}
        <span class="line">
          <span class="label">{m['nutrition.title']()}</span>

          {#if nothingCounted}
            <span class="nothing">{m['nutrition.nothing']()}</span>
          {:else}
            <span class="headline">
              <span class="figure">
                {withBound(
                  m['nutrition.energyLine']({ kcal: labelNumber('energy', energy) }),
                  energy.atLeast
                )}
              </span>
              <span class="per">{per}</span>
              {#if !answer.complete}
                <span class="coverage">
                  · {m['nutrition.coverage']({
                    counted: formatNumber(answer.counted),
                    lines: formatNumber(answer.lines)
                  })}
                </span>
              {/if}
            </span>
          {/if}
        </span>
      {/snippet}

      <!-- One column, so the table's values and the breakdown's energy share a right edge. -->
      <div class="open">
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
  .coverage,
  .nothing {
    color: var(--text-muted);
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
