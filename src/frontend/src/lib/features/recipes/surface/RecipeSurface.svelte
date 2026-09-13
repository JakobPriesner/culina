<script lang="ts">
  import { Button, Image } from '$ds';

  import { m } from '$shell/i18n';
  import { createScaling } from './scaled.svelte';
  import ScaleToAmountSheet from './ScaleToAmountSheet.svelte';
  import IngredientRow from './IngredientRow.svelte';
  import ServingsControl from './ServingsControl.svelte';
  import StepText from './StepText.svelte';
  import { imageSrcset, imageUrl } from '../recipeImage';
  import { metaLineFor } from '../recipeMeta';
  import type { Recipe } from '../types';

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
   * │ ingredients             ← SAME PLACE; contracts to the current
   * │                           step's ingredients when cooking
   * │ steps                   ← current step grows in place
   * └──────────────────────────────────────────────┘
   *
   * A later change that moves the servings control or the ingredient region
   * between the two weightings breaks the product's defining interaction. If
   * you need to move one, move it in both.
   */
  interface Props {
    recipe: Recipe;
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
    onstopcooking,
    onstep
  }: Props = $props();

  let highlighted = $state<string | null>(null);
  let scalingByAmount = $state(false);

  const scaling = createScaling(
    () => recipe,
    () => servings
  );

  const cooking = $derived(emphasis === 'cook');

  /** Only the ingredients the current step names, once cooking. */
  const stepIngredients = $derived(
    new Set(
      (recipe.steps[currentStep]?.segments ?? [])
        .filter((segment) => segment.kind === 'ingredient')
        .map((segment) => (segment.kind === 'ingredient' ? segment.ingredientId : ''))
    )
  );

  const groups = $derived(
    recipe.groups.map((group) => ({
      ...group,
      ingredients: cooking
        ? group.ingredients.filter((one) => stepIngredients.has(one.id))
        : group.ingredients
    }))
  );

  const anyIngredients = $derived(groups.some((group) => group.ingredients.length > 0));

  /** Named groups get a heading; a single unnamed group is just a list. */
  const showGroupNames = $derived(recipe.groups.some((group) => group.name));
</script>

<article class="surface" class:cooking>
  <!--
    The photo comes first while reading and disappears while cooking: it is what
    makes you choose the recipe, and it is dead weight once you are standing at
    the hob with your hands full.
  -->
  {#if recipe.imageId && !cooking}
    <div class="hero">
      <Image
        src={imageUrl(recipe.id, 1600)}
        srcset={imageSrcset(recipe.id)}
        sizes="(min-width: 72rem) 72rem, 100vw"
        alt=""
        ratio={16 / 9}
        loading="eager"
      />
    </div>
  {/if}

  <header class="head">
    <h1 class="title">{recipe.title}</h1>

    <p class="meta">
      {metaLineFor({
        id: recipe.id,
        title: recipe.title,
        imageId: recipe.imageId,
        totalMinutes: recipe.totalMinutes,
        yieldAmount: recipe.yieldAmount,
        yieldKind: recipe.yieldKind,
        tags: recipe.tags,
        cookCount: 0,
        updatedAt: recipe.updatedAt,
        match: null
      })}
    </p>

    {#if recipe.description}
      <p class="description">{recipe.description}</p>
    {/if}

    <!-- Paper only. On screen the servings control says this, and says it
         better because it can be changed; on paper there is nothing to say it
         at all, and the amounts below have to be accounted for. -->
    <p class="printed-yield">{scaling.currentYieldLabel}</p>
  </header>

  <!-- Same position in both weightings. Moving this breaks the transition. -->
  <div class="servings">
    <ServingsControl
      value={servings}
      kind={recipe.yieldKind}
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
      <h2 class="section">{m['recipe.ingredients']()}</h2>

      {#if anyIngredients}
        {#each groups as group (group.id ?? 'default')}
          {#if group.ingredients.length > 0}
            {#if showGroupNames && group.name}
              <h3 class="group">{group.name}</h3>
            {/if}

            <ul class="list">
              {#each group.ingredients as ingredient (ingredient.id)}
                <IngredientRow {ingredient} {scaling} highlighted={highlighted === ingredient.id} />
              {/each}
            </ul>
          {/if}
        {/each}
      {:else}
        <!-- Two different emptinesses. While cooking the list is filtered to
             what this step names, so "none written down" would be a lie about a
             recipe that has seven. -->
        <p class="empty">
          {cooking ? m['recipe.noneThisStep']() : m['recipe.noIngredients']()}
        </p>
      {/if}
    </section>

    <section class="steps" aria-label={m['recipe.steps']()}>
      <h2 class="section">{m['recipe.steps']()}</h2>

      {#if recipe.steps.length > 0}
        <ol class="list">
          {#each recipe.steps as step, index (step.id ?? index)}
            <li class="step" class:current={cooking && index === currentStep}>
              <!--
                While cooking the whole step is the control, because the gesture
                that matters is "next". While reading it is text, so that the
                ingredient references inside it can be pointed at — one or the
                other, never a button inside a button.
              -->
              {#if cooking}
                <button
                  class="step-body"
                  type="button"
                  aria-current={index === currentStep ? 'step' : undefined}
                  onclick={() => onstep?.(index)}
                >
                  <span class="number">{m['recipe.step']({ number: index + 1 })}</span>
                  <StepText {step} {scaling} interactive={false} />
                </button>
              {:else}
                <div class="step-body">
                  <span class="number">{m['recipe.step']({ number: index + 1 })}</span>
                  <StepText {step} {scaling} onhighlight={(id) => (highlighted = id)} />
                </div>
              {/if}
            </li>
          {/each}
        </ol>
      {:else}
        <p class="empty">{m['recipe.noSteps']()}</p>
      {/if}
    </section>
  </div>

  <ScaleToAmountSheet
    bind:open={scalingByAmount}
    {recipe}
    onapply={(value) => onservings?.(value)}
  />

  <footer class="foot">
    {#if cooking}
      <Button size="lg" onclick={onstopcooking}>{m['recipe.stopCooking']()}</Button>
    {:else}
      {#if onaddtolist}
        <Button size="lg" onclick={onaddtolist}>{m['shopping.addToList']()}</Button>
      {/if}

      <Button variant="primary" size="lg" onclick={onstartcooking}>
        {m['recipe.startCooking']()}
      </Button>
    {/if}
  </footer>
</article>

<style>
  .surface {
    display: flex;
    flex-direction: column;
    gap: var(--space-8);
  }

  /* Screen has the servings control for this; paper has nothing. */
  .printed-yield {
    display: none;
  }

  .hero {
    max-height: 24rem;
    overflow: hidden;
    border-radius: var(--radius-lg);
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-display);
    font-weight: var(--weight-regular);
    letter-spacing: -0.03em;
  }

  .meta,
  .description {
    color: var(--text-muted);
    margin-top: var(--space-2);
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
  }

  .warning {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .body {
    display: grid;
    grid-template-columns: minmax(0, 20rem) minmax(0, 1fr);
    gap: var(--space-12);
    align-items: start;
  }

  .section {
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.08em;
    text-transform: uppercase;
    color: var(--text-muted);
    margin-bottom: var(--space-3);
  }

  .group {
    margin-top: var(--space-4);
    font-size: var(--text-base);
  }

  .list {
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .step {
    padding-block: var(--space-3);
    border-top: 1px solid var(--border);
  }

  .step-body {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    width: 100%;
    padding: 0;
    border: none;
    background: none;
    color: inherit;
    font: inherit;
    text-align: start;
    cursor: pointer;
  }

  .number {
    color: var(--text-subtle);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  /*
   * The current step grows in place. Its neighbours stay where they are and
   * stay readable — quieter, not faded, because the step you just finished is
   * the one you most often need to glance back at, and at 0.45 opacity it was
   * 2.7:1 against the page. Readable has a number, and that was not it.
   */
  .cooking .step {
    color: var(--text-muted);
    transition:
      color var(--duration-base) var(--ease-out),
      font-size var(--duration-base) var(--ease-spatial);
  }

  .cooking .step.current {
    color: var(--text);
    font-size: var(--text-cook);
    line-height: var(--leading-normal);
  }

  .empty {
    color: var(--text-muted);
  }

  /* Clear of whatever the shell has already parked at the bottom of the
     viewport — the cooking bar, the phone's navigation bar, or both. */
  .foot {
    position: sticky;
    bottom: calc(var(--bottom-inset) + var(--space-4));
    display: flex;
    flex-wrap: wrap;
    justify-content: center;
    gap: var(--space-3);
  }

  @media (max-width: 47.999rem) {
    .body {
      grid-template-columns: 1fr;
      gap: var(--space-8);
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .cooking .step {
      transition: none;
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
    .servings,
    .foot {
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

    /* The meta line carries the yield the recipe was *written* for, which
       beside a scaled ingredient list is the one number on the page that is
       not true. Its tags and its timings are no loss either: what a paper
       recipe needs is what it makes and how to make it. */
    .meta {
      display: none;
    }

    .printed-yield {
      display: block;
      margin-top: 2mm;
      font-weight: var(--weight-medium);
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

    .steps .list {
      gap: 4mm;
    }
  }
</style>
