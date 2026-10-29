<script lang="ts">
  import { m } from '$shell/i18n';
  import ServingsControl from './ServingsControl.svelte';
  import type { Scaling } from './scaled.svelte';
  import type { RecipeReading } from '../types';
  import { wordYield, yieldNoun } from '../yieldWords';

  /**
   * How much is being made, at the top of the ingredients it changes. Same position in both weightings;
   * moving it breaks the transition.
   */
  interface Props {
    recipe: RecipeReading;
    servings: number;
    scaling: Scaling;
    onservings?: (value: number) => void;
    /** Opens the sheet that scales by an amount rather than a number of portions. */
    onscaleto: () => void;
    /** The yield the next planned meal is for; offered when it is not the one on screen, so cooking matches the plan. */
    plannedServings?: number | null;
  }

  let {
    recipe,
    servings,
    scaling,
    onservings,
    onscaleto,
    plannedServings = null
  }: Props = $props();

  const offersPlanned = $derived(
    Boolean(onservings && plannedServings && plannedServings !== servings)
  );
</script>

<div class="servings">
  <div class="amount">
    <ServingsControl
      value={servings}
      kind={recipe.yieldKind}
      label={recipe.yieldLabel}
      base={recipe.yieldAmount}
      onchange={(value) => onservings?.(value)}
    />
    <!-- The stepper already names it for a screen reader. -->
    <span class="noun" aria-hidden="true">{yieldNoun(recipe)}</span>
  </div>

  {#if offersPlanned && plannedServings}
    <button type="button" class="chip" onclick={() => onservings?.(plannedServings)}>
      {m['recipe.servings.asPlanned']({ amount: wordYield(plannedServings, recipe) })}
    </button>
  {/if}

  <button type="button" class="scale text-action hit-target" onclick={onscaleto}>
    {m['scaleTo.open']()}
  </button>

  {#if scaling.timesAreDoubtful}
    <!-- Baking time follows thickness and oven temperature does not scale, so warn rather than invent a formula. -->
    <p class="warning">
      {scaling.factor > 1
        ? m['recipe.timesWarning.up']({ count: scaling.baseYieldLabel })
        : m['recipe.timesWarning']({ count: scaling.baseYieldLabel })}
    </p>
  {/if}
</div>

<style>
  /* A line of its own under the heading, set off from the list it scales by a hairline. */
  .servings {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2) var(--space-4);
    padding-bottom: var(--space-4);
    margin-bottom: var(--space-4);
    border-bottom: 1px solid var(--border);
  }

  .amount {
    display: flex;
    align-items: center;
    gap: var(--space-2);
  }

  .noun {
    color: var(--text-muted);
  }

  .scale {
    font-size: var(--text-sm);
  }

  /* Quiet capsule: it reports the plan, so it is tinted rather than filled with the accent. */
  .chip {
    font: inherit;
    border: 0;
    cursor: pointer;
    display: inline-flex;
    align-items: center;
    min-height: var(--control-sm);
    padding-inline: var(--space-3);
    border-radius: var(--radius-full);
    background: var(--surface-accent-subtle);
    color: var(--text);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    transition:
      background-color var(--duration-fast) var(--ease-out),
      transform var(--duration-fast) var(--ease-out);
  }

  @media (hover: hover) {
    .chip:hover {
      background: var(--surface-highlight);
    }
  }

  .chip:active {
    transform: scale(var(--press-scale));
  }

  .warning {
    flex-basis: 100%;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  @media print {
    .servings {
      display: none !important;
    }
  }
</style>
