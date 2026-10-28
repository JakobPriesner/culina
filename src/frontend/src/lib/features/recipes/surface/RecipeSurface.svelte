<script lang="ts">
  import type { Snippet } from 'svelte';

  import { session } from '$features/auth/session.svelte';

  import { createIngredientPanel } from './ingredientPanel.svelte';
  import { createScaling } from './scaled.svelte';
  import CookingAction from './CookingAction.svelte';
  import IngredientsPanel from './IngredientsPanel.svelte';
  import RecipeTags from './RecipeTags.svelte';
  import StepList from './StepList.svelte';
  import SurfaceHead from './SurfaceHead.svelte';
  import SurfaceHero from './SurfaceHero.svelte';
  import SurfaceServings from './SurfaceServings.svelte';
  import type { NutritionLink } from './nutritionLink';
  import { nouns } from '../stores/nouns.svelte';
  import type { RecipeReading } from '../types';

  /**
   * Reading and cooking are one surface at two weightings: the servings control and ingredient region
   * must stay in the same place in both. If one moves, move it in both. See `ingredientPanel.svelte.ts`.
   */
  interface Props {
    recipe: RecipeReading;
    emphasis?: 'read' | 'cook';
    currentStep?: number;
    servings: number;
    onservings?: (value: number) => void;
    onstartcooking?: () => void;
    /** A session for this recipe is open, so the cooking action leads back to it. */
    resuming?: boolean;
    onaddtolist?: () => void;
    /** The recipe still has lines on the shopping list, which its action says. */
    listed?: boolean;
    /** Whether an ingredient of this name is still to buy, marked in the list; absent where there is no list. */
    onTheList?: (name: string) => boolean;
    onaddtocookbook?: () => void;
    onaddtoplan?: () => void;
    onshare?: () => void;
    /** Copies a read-only recipe into this household; replaces "Edit" there. */
    oncopy?: () => void;
    /** The page confirms; the surface only offers. */
    ondelete?: () => void;
    /** Photo location when it isn't at the recipe's own address (share links hold a token, not a recipe id). */
    photo?: { readonly src: string; readonly srcset: string };
    /** Offers the way back into the editor; the cook route leaves it off. */
    editable?: boolean;
    /** Cookbooks it is on; shown here, edited by the dock. */
    cookbooks?: readonly { readonly id: string; readonly name: string }[];
    /** "I made it" without cook mode; see `CookingAction`. */
    onmadeit?: () => void;
    /** How often this person made it and when last, for the meta line like a recipe card's. */
    cooked?: { readonly count: number; readonly lastMadeAt: string | null } | null;
    onstep?: (index: number) => void;
    /** Reports whether the title is still on screen; see `SurfaceHead`. */
    ontitleview?: (visible: boolean) => void;
    /** The nutrition headline for the meta line; absent while unknown, and never shown while cooking. */
    nutrition?: NutritionLink | null;
    /** Where the recipe is in the loop (planned, on the list), after the servings band; not while cooking. */
    whereabouts?: Snippet;
    /** Your own note on a step, by step id; see `StepList`. */
    stepNote?: (stepId: string) => string;
    /** Controls under the step being cooked; only the cook route supplies them. */
    stepaction?: Snippet;
  }

  let {
    recipe,
    emphasis = 'read',
    currentStep = 0,
    servings,
    onservings,
    onstartcooking,
    resuming = false,
    onaddtolist,
    listed = false,
    onTheList,
    onaddtocookbook,
    onaddtoplan,
    onshare,
    oncopy,
    ondelete,
    photo,
    editable = false,
    cookbooks = [],
    onmadeit,
    cooked = null,
    onstep,
    ontitleview,
    nutrition = null,
    whereabouts,
    stepNote,
    stepaction
  }: Props = $props();

  let highlighted = $state<string | null>(null);
  let scalingByAmount = $state(false);
  // Downloaded the first time it is asked for, not with the recipe; then it stays to animate closed.
  let scaledByAmount = $state(false);
  /** The ingredient a tap on its amount asked to scale from. */
  let scaleFrom = $state<string | null>(null);

  function scaleByAmount(ingredientId: string | null = null) {
    scaleFrom = ingredientId;
    scalingByAmount = true;
    scaledByAmount = true;
  }

  const locateIngredient = (ingredientId: string) => {
    highlighted = ingredientId;
    const element = document.getElementById(`ingredient-row-${ingredientId}`);
    if (element) {
      element.scrollIntoView({ behavior: 'smooth', block: 'center' });
      if (element instanceof HTMLElement) {
        element.focus({ preventScroll: true });
      }
    }
  };

  // The reader's own words in both numbers, for the names `scaling` fits to the servings.
  $effect(() => {
    const household = session.activeHousehold;

    if (household) {
      void nouns.load(household.householdId);
    }
  });

  const scaling = createScaling(
    () => recipe,
    () => servings
  );

  const cooking = $derived(emphasis === 'cook');

  const ingredients = createIngredientPanel({
    recipe: () => recipe,
    cooking: () => cooking,
    currentStep: () => currentStep
  });

  // A recipe with no steps (e.g. from an import) must not open cook mode on "Step 1 of 0".
  const canCook = $derived(Boolean(onstartcooking) && recipe.steps.length > 0);
</script>

<article
  class="surface"
  class:cooking
  class:perStep={ingredients.perStep}
  class:photographed={Boolean(recipe.imageId && !cooking)}
>
  {#if recipe.imageId && !cooking}
    <SurfaceHero recipeId={recipe.id} imageId={recipe.imageId} {photo} />
  {/if}

  <SurfaceHead
    {recipe}
    {cooking}
    {editable}
    {cookbooks}
    printedYield={scaling.currentYieldLabel}
    {onaddtolist}
    {listed}
    {onaddtocookbook}
    {onaddtoplan}
    onstartcooking={canCook ? onstartcooking : undefined}
    {resuming}
    onmadeit={canCook ? onmadeit : undefined}
    {onshare}
    {oncopy}
    {ondelete}
    {ontitleview}
    {nutrition}
    {cooked}
  />

  <!-- One row on a desktop (servings, tags, notes), so the ingredients start on a laptop's first screen; each its own
       band below that. -->
  <div class="details">
    <SurfaceServings {recipe} {servings} {scaling} {onservings} onscaleto={() => scaleByAmount()} />

    {#if !cooking}
      <RecipeTags recipeId={recipe.id} tags={recipe.tags} />
    {/if}

    <!-- After the servings band, not under the title: on a phone that keeps the servings on the first screen. -->
    {#if whereabouts && !cooking}
      <div class="whereabouts">{@render whereabouts()}</div>
    {/if}
  </div>

  <div class="body">
    <IngredientsPanel
      panel={ingredients}
      {cooking}
      {scaling}
      {highlighted}
      onhighlight={(id) => (highlighted = id)}
      onTheList={cooking ? undefined : onTheList}
      onscaleto={!cooking && onservings ? scaleByAmount : undefined}
    />

    <StepList
      steps={recipe.steps}
      {cooking}
      {currentStep}
      perStep={ingredients.perStep}
      divisible={ingredients.divisible}
      {scaling}
      {highlighted}
      written={ingredients.written}
      needsOf={ingredients.needsOf}
      {stepaction}
      {onstep}
      onhighlight={(id) => (highlighted = id)}
      onlocate={locateIngredient}
      {stepNote}
    />
  </div>

  {#if scaledByAmount}
    {#await import('./ScaleToAmountSheet.svelte') then { default: ScaleToAmountSheet }}
      <ScaleToAmountSheet
        bind:open={scalingByAmount}
        {recipe}
        startWith={scaleFrom}
        onapply={(value) => onservings?.(value)}
      />
    {/await}
  {/if}

  <CookingAction {cooking} {canCook} {resuming} {onstartcooking} {onmadeit} />
</article>

<style>
  .surface {
    display: flex;
    flex-direction: column;
    gap: var(--space-8);
  }

  /* Below a desktop the wrapper is not there for layout: servings, tags and notes are bands of the surface. */
  .details {
    display: contents;
  }

  /*
   * A desktop reads servings, tags and notes as one band between hairlines (the servings band's own), so the head is
   * one row shorter by two and the ingredients start on a 1280x800 screen (culina-v2-6ilt.17). Anything too long for
   * the row (a written note, many tags) wraps under it rather than squeezing the servings.
   */
  @media screen and (min-width: 64rem) {
    .surface:not(.cooking) {
      gap: var(--space-6);
    }

    .surface:not(.cooking) .details {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--space-3) var(--space-4);
      padding-block: var(--space-3);
      border-block: 1px solid var(--border);
    }

    .surface:not(.cooking) .details > :global(.servings) {
      padding-block: 0;
      border-block: none;
    }

    /* Takes what the row has left and wraps inside it, rather than dropping whole to a line of its own. */
    .surface:not(.cooking) .details > .whereabouts {
      display: flex;
      flex: 1 1 0;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--space-3);
      min-width: min(100%, 16rem);
    }

    /* In a row their stacked spacing would only push them off the line. */
    .surface:not(.cooking) .details > .whereabouts > :global(*:not(.peek)) {
      margin-block: 0;
      padding-block: 0;
    }

    .surface:not(.cooking) .details > .whereabouts > :global(.peek) {
      flex-basis: 100%;
    }
  }

  /* Two columns; widths come from `--side-column`/`--card-padding` so heading rows and steps agree. */
  .body {
    display: grid;
    grid-template-columns: minmax(0, var(--side-column)) minmax(0, 1fr);
    gap: var(--layout-section-gap);
    align-items: start;
  }

  @media screen and (width < 64rem) {
    .body {
      grid-template-columns: 1fr;
      gap: var(--space-8);
    }
  }

  @media (width < 52rem) {
    .surface {
      gap: var(--space-4);
    }
  }

  /* A landscape phone has width and no height: the photograph stands beside the title and servings instead of above them, so the title is on the first screen. */
  @media screen and (max-height: 32rem) {
    .photographed {
      display: grid;
      grid-template-columns: minmax(9rem, 2fr) minmax(0, 3fr);
      column-gap: var(--space-6);
      align-items: start;
    }

    .photographed > :global(*),
    .photographed > .details > :global(*) {
      grid-column: 1 / -1;
    }

    .photographed > :global(.hero) {
      grid-column: 1;
      grid-row: 1 / span 2;
      align-self: stretch;
    }

    .photographed > :global(.head),
    .photographed > .details > :global(.servings) {
      grid-column: 2;
    }
  }

  @media print {
    .surface {
      display: block;
      max-width: none;
      padding: 0;
    }

    .body {
      display: grid;
      grid-template-columns: 32% 1fr;
      gap: 8mm;
      align-items: start;
    }
  }
</style>
