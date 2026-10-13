<script lang="ts">
  import type { Snippet } from 'svelte';

  import { session } from '$features/auth/session.svelte';

  import { createIngredientPanel } from './ingredientPanel.svelte';
  import { createScaling } from './scaled.svelte';
  import CookingAction from './CookingAction.svelte';
  import IngredientsPanel from './IngredientsPanel.svelte';
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
    onaddtolist?: () => void;
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
    onstopcooking?: () => void;
    onstep?: (index: number) => void;
    /** Reports whether the title is still on screen; see `SurfaceHead`. */
    ontitleview?: (visible: boolean) => void;
    /** The nutrition headline for the meta line; absent while unknown, and never shown while cooking. */
    nutrition?: NutritionLink | null;
    /** Where the recipe is in the loop (planned, on the list), after the servings band; not while cooking. */
    whereabouts?: Snippet;
  }

  let {
    recipe,
    emphasis = 'read',
    currentStep = 0,
    servings,
    onservings,
    onstartcooking,
    onaddtolist,
    onaddtocookbook,
    onaddtoplan,
    onshare,
    oncopy,
    ondelete,
    photo,
    editable = false,
    cookbooks = [],
    onstopcooking,
    onstep,
    ontitleview,
    nutrition = null,
    whereabouts
  }: Props = $props();

  let highlighted = $state<string | null>(null);
  let scalingByAmount = $state(false);
  // Downloaded the first time it is asked for, not with the recipe; then it stays to animate closed.
  let scaledByAmount = $state(false);

  function scaleByAmount() {
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
    {onaddtocookbook}
    {onaddtoplan}
    {onshare}
    {oncopy}
    {ondelete}
    {ontitleview}
    {nutrition}
  />

  <SurfaceServings {recipe} {servings} {scaling} {onservings} onscaleto={scaleByAmount} />

  <!-- After the servings band, not under the title: on a phone that keeps the servings on the first screen. -->
  {#if whereabouts && !cooking}
    <div class="whereabouts">{@render whereabouts()}</div>
  {/if}

  <div class="body">
    <IngredientsPanel
      panel={ingredients}
      {cooking}
      {scaling}
      {highlighted}
      onhighlight={(id) => (highlighted = id)}
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
      {onstep}
      onhighlight={(id) => (highlighted = id)}
      onlocate={locateIngredient}
    />
  </div>

  {#if scaledByAmount}
    {#await import('./ScaleToAmountSheet.svelte') then { default: ScaleToAmountSheet }}
      <ScaleToAmountSheet
        bind:open={scalingByAmount}
        {recipe}
        onapply={(value) => onservings?.(value)}
      />
    {/await}
  {/if}

  <CookingAction {cooking} {canCook} {onstartcooking} {onstopcooking} />
</article>

<style>
  .surface {
    display: flex;
    flex-direction: column;
    gap: var(--space-8);
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
      position: relative;
      gap: var(--space-4);
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
