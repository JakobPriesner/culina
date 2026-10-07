<script lang="ts">
  import { createIngredientPanel } from './ingredientPanel.svelte';
  import { createScaling } from './scaled.svelte';
  import CookingAction from './CookingAction.svelte';
  import IngredientsPanel from './IngredientsPanel.svelte';
  import ScaleToAmountSheet from './ScaleToAmountSheet.svelte';
  import StepList from './StepList.svelte';
  import SurfaceHead from './SurfaceHead.svelte';
  import SurfaceHero from './SurfaceHero.svelte';
  import SurfaceServings from './SurfaceServings.svelte';
  import type { RecipeReading } from '../types';

  /**
   * The one surface the product is built around.
   *
   * Reading and cooking are the same component at two weightings, not two
   * pages that happen to show the same data. That is the whole point: the
   * positions below were chosen so that **nothing has to move** when the
   * emphasis changes.
   *
   * ┌──────────────────────────────────────────────┐
   * │ title + meta            (recedes when cooking)│
   * │ servings control        ← SAME PLACE in both  │
   * │ ingredients             ← SAME PLACE; contracts to what the current
   * │                           step needs when cooking
   * │ steps                   ← current step grows in place
   * └──────────────────────────────────────────────┘
   *
   * A later change that moves the servings control or the ingredient region
   * between the two weightings breaks the product's defining interaction. If
   * you need to move one, move it in both.
   *
   * The ingredient region and its two arrangements are described in
   * `ingredientPanel.svelte.ts`.
   */
  interface Props {
    recipe: RecipeReading;
    /** `read` is the whole recipe; `cook` weights it towards the current step. */
    emphasis?: 'read' | 'cook';
    /** Which step is being cooked, when cooking. */
    currentStep?: number;
    /** How many it is being made for. Owned by the page, which keeps it in the URL. */
    servings: number;
    onservings?: (value: number) => void;
    onstartcooking?: () => void;
    /** Puts the ingredients on the shopping list, at the scaling on screen. */
    onaddtolist?: () => void;
    /** Opens the sheet that says which cookbooks this recipe is on. */
    onaddtocookbook?: () => void;
    /** Opens the week at the servings currently shown on this recipe. */
    onaddtoplan?: () => void;
    /** Opens the sheet that hands out, and takes back, the link to this recipe. */
    onshare?: () => void;
    /**
     * Makes this household its own copy, for a recipe it can read but not
     * change: what "Edit" becomes when editing is somebody else's.
     */
    oncopy?: () => void;
    /** Asks whether to delete it. The page asks; the surface only offers. */
    ondelete?: () => void;
    /**
     * Where the photograph is, when it is not at the recipe's own address.
     *
     * The one thing the surface cannot work out for itself. Whoever follows a
     * share link holds a token and no recipe id, so their copy of this page
     * fetches the picture from somewhere else entirely — and that is the whole
     * of the difference between their page and the household's.
     */
    photo?: { readonly src: string; readonly srcset: string };
    /**
     * Whether to offer the way back into the editor.
     *
     * The address is built here, from the recipe, like the cookbook links
     * above it — what the page decides is whether writing this recipe down is
     * one of the things it is for. The cook route says nothing and gets
     * nothing.
     */
    editable?: boolean;
    /**
     * The cookbooks it is already on.
     *
     * Read here and written by the dock: the line under the title is the
     * answer, and the control is the question, so they must never disagree.
     */
    cookbooks?: readonly { readonly id: string; readonly name: string }[];
    onstopcooking?: () => void;
    onstep?: (index: number) => void;
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
    onstep
  }: Props = $props();

  let highlighted = $state<string | null>(null);
  let scalingByAmount = $state(false);

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

  /**
   * Whether there is anything to cook.
   *
   * A recipe with no steps offered the button like any other, and cook mode
   * then opened on "Step 1 of 0" with nothing under it. An empty recipe is an
   * ordinary state — one saved from an import, or half written — so the page
   * says so where the steps would be, rather than letting somebody walk into a
   * screen that looks broken.
   */
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
  />

  <SurfaceServings
    {recipe}
    {servings}
    {scaling}
    {onservings}
    onscaleto={() => (scalingByAmount = true)}
  />

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

  <ScaleToAmountSheet
    bind:open={scalingByAmount}
    {recipe}
    onapply={(value) => onservings?.(value)}
  />

  <CookingAction {cooking} {canCook} {onstartcooking} {onstopcooking} />
</article>

<style>
  .surface {
    display: flex;
    flex-direction: column;
    gap: var(--space-8);
  }

  /*
   * Two columns: the ingredients, and the method. Their width and the card's
   * inset are `--side-column` and `--card-padding` in the token scale, because
   * the heading rows and the steps in the components below have to agree on
   * them.
   */
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

  /* Reading is the job here, so the phone keeps the recipe's identity without
     spending its whole first screenful on it. */
  @media (width < 52rem) {
    .surface {
      position: relative;
      gap: var(--space-4);
    }
  }

  /*
   * One page: the title, what it makes at the servings on screen, the
   * ingredients and the steps.
   *
   * The amounts printed are the scaled ones, because they are the ones on
   * screen. Scaling a recipe to six and printing it for four is exactly the
   * kind of quiet lie this app is built to avoid.
   */
  @media print {
    .surface {
      display: block;
      max-width: none;
      padding: 0;
    }

    /* Two columns on paper, which a screen cannot afford and a page can: the
       ingredients sit beside the first steps instead of on a page of their
       own, and most recipes come out as one sheet. */
    .body {
      display: grid;
      grid-template-columns: 32% 1fr;
      gap: 8mm;
      align-items: start;
    }
  }
</style>
