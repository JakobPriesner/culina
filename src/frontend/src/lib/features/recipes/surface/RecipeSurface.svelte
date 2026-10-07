<script lang="ts">
  import { Button, Image, SegmentedControl } from '$ds';

  import { m } from '$shell/i18n';
  import {
    recallIngredientsView,
    rememberIngredientsView,
    type IngredientsView
  } from './ingredientsView';
  import { createScaling } from './scaled.svelte';
  import CookingAction from './CookingAction.svelte';
  import IngredientList from './IngredientList.svelte';
  import RecipeActions from './RecipeActions.svelte';
  import RecipeHeadMeta from './RecipeHeadMeta.svelte';
  import ScaleToAmountSheet from './ScaleToAmountSheet.svelte';
  import SectionHead from './SectionHead.svelte';
  import ServingsControl from './ServingsControl.svelte';
  import StepList from './StepList.svelte';
  import { imageSrcset, imageUrl } from '../recipeImage';
  import {
    everyIngredient,
    ingredientsOf,
    type Ingredient,
    type RecipeReading,
    type Step
  } from '../types';

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
   * The ingredient region has two arrangements, and the switch beside its
   * heading picks between them:
   *
   * - `combined` — the whole list in one place, the same thing added up
   *   wherever the recipe asked for it. What you read before you shop.
   * - `perStep` — each step's ingredients beside that step, in the column the
   *   list would otherwise fill. What you read with a pan in your hand.
   *
   * Cooking is `perStep` taken to its conclusion — one step, and only what it
   * needs — so it neither offers the switch nor obeys it. That is also why the
   * region stays in the same place in all three: they are one arrangement at
   * three widths of attention, not three layouts.
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

  let view = $state<IngredientsView>(recallIngredientsView());

  const chooseView = (chosen: string) => {
    view = chosen === 'perStep' ? 'perStep' : 'combined';
    rememberIngredientsView(view);
  };

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

  /**
   * Whether the steps have more than one list to be dealt out between.
   *
   * With one step, "by step" and "all together" are the same list — and dealing
   * it out only moves it off the panel into a second card under an empty one.
   * So a lone step is read side by side, whichever arrangement was chosen, and
   * the choice is kept for the next recipe rather than overwritten.
   */
  const divisible = $derived(recipe.steps.length > 1);

  /** Cooking has already contracted the region to one step; it cannot do both. */
  const perStep = $derived(view === 'perStep' && !cooking && divisible);

  const byId = $derived(ingredientsOf(recipe));

  /**
   * What one step needs, as the ingredient lines themselves.
   *
   * A step holds ids; an id the recipe no longer has drops out rather than
   * rendering as a hole. The order is the recipe's own, because that is the
   * order the ingredient list beside it is already in.
   */
  const needsOf = (step: Step | undefined): Ingredient[] =>
    (step?.uses ?? []).map((id) => byId.get(id)).filter((one) => one !== undefined);

  /** Every ingredient the recipe has, groups flattened, in its own order. */
  const written = $derived(everyIngredient(recipe));

  /** Everything some step asks for, which is very nearly always everything. */
  const claimed = $derived(new Set(recipe.steps.flatMap((step) => step.uses)));

  /**
   * What the panel under the heading holds — three answers to three questions.
   *
   * Cooking asks "what is in my hands now", so it is the current step's list.
   * `perStep` has already put every claimed ingredient beside the step that
   * claims it, so what is left there is what no step mentions: the jar of
   * something that belongs to the whole dish, which would otherwise vanish off
   * the page altogether. Reading the combined list asks "what does this recipe
   * need", and the answer to that is all of it.
   */
  const panel = $derived.by(() => {
    if (cooking) {
      return needsOf(recipe.steps[currentStep]);
    }

    return perStep ? written.filter((one) => !claimed.has(one.id)) : written;
  });
</script>

<article
  class="surface"
  class:cooking
  class:perStep
  class:photographed={Boolean(recipe.imageId && !cooking)}
>
  <!--
    The photo comes first while reading and disappears while cooking: it is what
    makes you choose the recipe, and it is dead weight once you are standing at
    the hob with your hands full.
  -->
  {#if recipe.imageId && !cooking}
    <div class="hero">
      <Image
        src={photo?.src ?? imageUrl(recipe.id, 1600, recipe.imageId)}
        srcset={photo?.srcset ?? imageSrcset(recipe.id, recipe.imageId)}
        sizes="(min-width: 72rem) 72rem, 100vw"
        alt=""
        loading="eager"
        fill
        rounded={false}
      />
    </div>
  {/if}

  <header class="head">
    <div class="titleRow">
      <h1 class="title">{recipe.title}</h1>

      <RecipeActions
        recipeId={recipe.id}
        {cooking}
        {editable}
        {onaddtolist}
        {onaddtocookbook}
        {onaddtoplan}
        {onshare}
        {oncopy}
        {ondelete}
      />
    </div>

    <RecipeHeadMeta {recipe} {cooking} {cookbooks} printedYield={scaling.currentYieldLabel} />
  </header>

  <!-- Same position in both weightings. Moving this breaks the transition. -->
  <div class="servings">
    <ServingsControl
      value={servings}
      kind={recipe.yieldKind}
      label={recipe.yieldLabel}
      base={recipe.yieldAmount}
      onchange={(value) => onservings?.(value)}
    />

    <!-- The other end of the same machinery: a leftover 600 g of flour rather
         than a number of portions. -->
    <Button onclick={() => (scalingByAmount = true)}>{m['scaleTo.open']()}</Button>

    {#if scaling.timesAreDoubtful}
      <!--
        A quiet line, never a modal. Baking time follows the thickness of what
        is in the tin, not its mass, and oven temperature does not scale at
        all — so Culina says so rather than inventing a formula.
      -->
      <p class="warning">
        {m['recipe.timesWarning']({ count: scaling.baseYieldLabel })}
      </p>
    {/if}
  </div>

  <div class="body">
    <!-- Same position in both weightings; contracts to the current step's
         ingredients when cooking. -->
    <section class="ingredients" aria-label={m['recipe.ingredients']()}>
      <!-- The switch sits on the heading's own line, and stays put when the
           arrangement changes: the head keeps the width of the ingredient
           column even where the section has grown past it. -->
      <SectionHead title={m['recipe.ingredients']()}>
        <!-- Nothing to choose while cooking, where one step is the whole
             arrangement, nor in a recipe that only has the one. -->
        {#if !cooking && divisible && written.length > 0}
          <SegmentedControl
            label={m['recipe.ingredientsView.label']()}
            selected={view}
            segments={[
              { id: 'combined', label: m['recipe.ingredientsView.combined']() },
              { id: 'perStep', label: m['recipe.ingredientsView.perStep']() }
            ]}
            onselect={chooseView}
          />
        {/if}
      </SectionHead>

      {#if panel.length > 0}
        <!-- Said only where it needs saying. In the combined list this is the
             list; beside the steps it is the handful the steps never named, and
             a reader who is not told that will wonder what happened to the
             rest. -->
        {#if perStep}
          <p class="leftovers">{m['recipe.notInAnyStep']()}</p>
        {/if}

        <IngredientList
          ingredients={panel}
          {scaling}
          {highlighted}
          onhover={(id) => (highlighted = id)}
        />
      {:else if !perStep}
        <!-- Two different emptinesses. While cooking the list is filtered to
             what this step needs, so "none written down" would be a lie about a
             recipe that has seven. Beside the steps there is a third: every
             ingredient is accounted for, and the right thing to say is
             nothing. -->
        <p class="empty">
          {cooking ? m['recipe.noneThisStep']() : m['recipe.noIngredients']()}
        </p>
      {:else if written.length === 0}
        <p class="empty">{m['recipe.noIngredients']()}</p>
      {/if}
    </section>

    <StepList
      steps={recipe.steps}
      {cooking}
      {currentStep}
      {perStep}
      {divisible}
      {scaling}
      {highlighted}
      {written}
      {needsOf}
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

  /* A 16:9 box, capped so that on a wide screen the photograph does not take
     the whole first screenful. The cap shrinks the box rather than cutting the
     picture off at the bottom: what is worth looking at in a photograph of
     dinner is in the middle of it, so the crop has to come off both ends. */
  .hero {
    display: grid;
    aspect-ratio: 16 / 9;
    max-height: 24rem;
    /* Stated, not left auto: a max-height transfers through an aspect ratio
       into a max-width, and an auto width would obey it — the box would go
       narrow instead of short. */
    width: 100%;
    overflow: hidden;
    border-radius: var(--radius-lg);
  }

  /*
   * Two columns, not a wrapping row.
   *
   * A row let a long title push the controls onto a line of their own at the
   * *start* of it, where they sat directly on top of the meta line — so the
   * page's furniture changed places depending on how long somebody's recipe
   * was called. Here the title takes the room it needs and wraps inside its own
   * column, and the group stays at the end of the row it belongs to.
   */
  .titleRow {
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto;
    align-items: center;
    gap: var(--space-4);
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-display);
    font-weight: var(--weight-regular);
    letter-spacing: -0.03em;
  }

  /* The chrome recedes when cooking; it does not disappear, because knowing
     which recipe you are in is not optional.

     Smaller and quieter, not faded. Opacity on text is how contrast breaks
     without anybody noticing: it blends toward the background by an amount no
     palette review can see, and the theme's own contrast test cannot reach it.
     `--text-muted` is a colour the contract already proves readable in both
     modes. */
  .cooking .head {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .cooking .title {
    font-size: var(--text-xl);
  }

  .servings {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-4);
    padding-block: var(--space-6);
    border-block: 1px solid var(--border);
  }

  .warning {
    color: var(--text-muted);
    font-size: var(--text-sm);
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

  /*
   * Only the list gets a surface.
   *
   * It is the thing you keep glancing back at and have to find again
   * mid-sentence, and the panel is what makes it findable — a shape the eye
   * returns to rather than a column it has to re-locate. The method is prose:
   * read straight down, once, and the longest thing on the page. Drawing a
   * panel around that boxes in something nobody was going to lose, and doubles
   * the page's furniture to say it.
   *
   * The rule holds in both arrangements, which is what makes the surface mean
   * something: wherever it is, it is what you need out. Beside the steps it
   * travels with them — see `.perStep .step-needs` in `StepList`.
   */
  .ingredients {
    padding: var(--card-padding);
    background: var(--surface-sunken);
    border-radius: var(--radius-lg);
  }

  /*
   * The list stays on screen while the method scrolls past it.
   *
   * A recipe's steps are long and its ingredients are the thing you keep
   * glancing back at — "how much of the wine goes in here" is asked at step
   * four, where the list left the screen at step one. Sticking it is what
   * makes the two columns behave like a spread in a cookbook rather than two
   * documents that happen to be side by side.
   *
   * `align-items: start` on the grid is what makes this work at all: a
   * stretched grid item fills its row and has nowhere to travel. If that ever
   * comes off, this silently stops sticking.
   *
   * The offset is the one the steps already scroll to — far enough down that
   * the app's floating header is not sitting on top of it.
   *
   * No height limit, deliberately. Capping the panel to the viewport and
   * letting it scroll inside itself reads as a bug long before it helps: an
   * overlay scrollbar is invisible until it is touched, so a fifteen-line list
   * on an ordinary laptop is just a card with its end cut off — and that is
   * the common recipe, not the rare one. A list taller than the screen keeps
   * its top pinned instead, which is the half you glance back at, and shows
   * its end once the steps beside it run out. Whole and occasionally clipped
   * beats tidy and apparently broken.
   */
  .ingredients {
    position: sticky;
    top: var(--space-24);
  }

  /* Beside the steps the panel keeps its column and its card, and has nothing
     to follow: what is left in it is the heading and whatever no step asked
     for, and the rest is already level with the step that needs it. The steps
     arrange themselves around it; see `StepList`. */
  .perStep .ingredients {
    grid-column: 1;
    grid-row: 1;
    position: static;
  }

  .leftovers {
    margin-bottom: var(--space-2);
    color: var(--text-subtle);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .empty {
    color: var(--text-muted);
  }

  @media screen and (width < 64rem) {
    .body {
      grid-template-columns: 1fr;
      gap: var(--space-8);
    }

    /* One column: the list is above the steps rather than beside them, and
       something stuck to the top of the screen there is not a companion, it is
       a lid. */
    .ingredients {
      position: static;
    }
  }

  @media (width < 52rem) {
    /* Reading is the job here, so the phone keeps the recipe's identity without
       spending its whole first screenful on it. The photograph becomes a wide
       banner and the title steps down one size. */
    .surface {
      position: relative;
      gap: var(--space-4);
    }

    .hero {
      aspect-ratio: 4 / 1;
      max-height: 7rem;
      border-radius: var(--radius-md);
    }

    .titleRow {
      grid-template-columns: minmax(0, 1fr) auto;
      align-items: start;
      gap: var(--space-2);
    }

    .title {
      font-size: var(--text-3xl);
      line-height: var(--leading-tight);
    }

    .servings {
      gap: var(--space-2);
      padding-block: var(--space-3);
    }

    .servings :global(.button) {
      min-height: var(--control-sm);
      padding-inline: var(--space-3);
      font-size: var(--text-sm);
    }
  }

  /*
   * One page: the title, what it makes at the servings on screen, the
   * ingredients and the steps.
   *
   * The photograph does not print. It is what makes you choose a recipe and it
   * is a page of ink once you have chosen it — and the paper is going on a
   * worktop next to something wet, not on a wall.
   *
   * The amounts printed are the scaled ones, because they are the ones on
   * screen. Scaling a recipe to six and printing it for four is exactly the
   * kind of quiet lie this app is built to avoid.
   */
  @media print {
    .hero,
    .servings {
      display: none !important;
    }

    .surface {
      display: block;
      max-width: none;
      padding: 0;
    }

    .head {
      margin-bottom: 6mm;
    }

    .title {
      font-size: 20pt;
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

    /* Ink, not grey blocks. */
    .ingredients {
      padding: 0;
      background: none;
      position: static;
    }
  }
</style>
