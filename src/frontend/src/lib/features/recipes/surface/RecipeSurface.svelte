<script lang="ts">
  import { tick } from 'svelte';

  import { resolve } from '$app/paths';

  import { Button, Image } from '$ds';

  import { m } from '$shell/i18n';
  import { createScaling } from './scaled.svelte';
  import ScaleToAmountSheet from './ScaleToAmountSheet.svelte';
  import IngredientRow from './IngredientRow.svelte';
  import ServingsControl from './ServingsControl.svelte';
  import StepNeeds from './StepNeeds.svelte';
  import StepText from './StepText.svelte';
  import { imageSrcset, imageUrl } from '../recipeImage';
  import { metaLineFor } from '../recipeMeta';
  import { ingredientsOf, type Ingredient, type Recipe, type Step } from '../types';

  /**
   * The host of the original, for the "from …" line.
   *
   * The host and not the whole address: "chefkoch.de" is the fact worth showing
   * and a 140-character URL with tracking parameters on the end is the same
   * fact, unreadable. An address that will not parse simply has no line.
   */
  const hostOf = (url: string): string => {
    try {
      return new URL(url).host.replace(/^www\./, '');
    } catch {
      return url;
    }
  };

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
    /** Opens the sheet that says which cookbooks this recipe is on. */
    onaddtocookbook?: () => void;
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
    editable = false,
    cookbooks = [],
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

  const byId = $derived(ingredientsOf(recipe));

  /**
   * What one step needs, as the ingredient lines themselves.
   *
   * A step holds ids; an id the recipe no longer has drops out rather than
   * rendering as a hole. The order is the recipe's own, because that is the
   * order the ingredient list beside it is already in.
   */
  const needsOf = (step: Step): Ingredient[] =>
    step.uses.map((id) => byId.get(id)).filter((one) => one !== undefined);

  /** Only what the current step needs, once cooking. */
  const stepIngredients = $derived(new Set(recipe.steps[currentStep]?.uses ?? []));

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

  let stepList = $state<HTMLOListElement>();

  /**
   * Whether a step has already been centred once.
   *
   * The first run is the page arriving, and a page that scrolls itself the
   * moment it opens has taken the reader somewhere they did not ask to go. It
   * is every *move* after that which needs the screen to follow.
   */
  let centred = false;

  /**
   * Waits for the steps to finish changing size.
   *
   * The step becoming current grows and the one leaving it shrinks, both
   * animated, so the geometry a step has the instant it becomes current is not
   * the geometry it has a blink later. Measuring the first scrolls to where the
   * step was rather than where it lands. `getAnimations` is missing in jsdom,
   * and an animation that never ends must not stall the scroll forever.
   */
  const resized = async (list: HTMLElement) => {
    await tick();

    await Promise.race([
      Promise.allSettled(
        (list.getAnimations?.({ subtree: true }) ?? []).map((one) => one.finished)
      ),
      new Promise((done) => setTimeout(done, 400))
    ]);
  };

  /**
   * The step being cooked comes to the middle of the screen.
   *
   * Steps are as long as they need to be, so after two or three of them the
   * one being cooked is wherever the previous one left it — often under the
   * controls at the bottom, or off the top. Moving it to the middle means the
   * answer to "what am I doing now" is always in the same place, and the step
   * after it is already visible underneath.
   *
   * Only while cooking: reading is scrolled by the person doing it, and a page
   * that moves under a reader's thumb is a page fighting them.
   */
  $effect(() => {
    const index = currentStep;
    const list = stepList;

    if (!cooking || !list) {
      return;
    }

    if (!centred) {
      centred = true;
      return;
    }

    void resized(list).then(() => {
      const step = list.children[index];

      // `scrollIntoView` is missing in jsdom, and moving the screen is a
      // courtesy rather than behaviour the page depends on.
      if (!(step instanceof HTMLElement) || !step.scrollIntoView) {
        return;
      }

      // A step grows as it becomes current, and a long one is most of the
      // screen or more. Centring that hides its first words behind the header
      // or its last behind the controls, so only a step with room to spare is
      // centred; a longer one starts at the top, where it is read from. The
      // `scroll-margin` on `.step` is what keeps that clear of the header.
      const fits = step.getBoundingClientRect().height < window.innerHeight / 2;

      step.scrollIntoView({
        block: fits ? 'center' : 'start',
        // Smoothly, so it reads as the page following rather than jumping —
        // unless the person has said they do not want things moving.
        behavior: window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
          ? 'auto'
          : 'smooth'
      });
    });
  });
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
    <div class="titleRow">
      <h1 class="title">{recipe.title}</h1>

      <!-- Quiet, and the other end of the editor's "← Done": writing a recipe
           down is not one of the things this page is for, it is the way back
           out of it. Hidden while cooking, where the only correct edit is the
           one you make to the pan. -->
      {#if editable && !cooking}
        <a class="edit" href={resolve('/(app)/recipes/[recipeId]/edit', { recipeId: recipe.id })}>
          {m['editor.edit']()}
        </a>
      {/if}
    </div>

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
        lastCookedAt: null,
        updatedAt: recipe.updatedAt,
        match: null
      })}
    </p>

    {#if recipe.description}
      <p class="description">{recipe.description}</p>
    {/if}

    <!-- Where this recipe lives, and only while reading: a cook standing at the
         hob does not need to be told which shelf it came off. -->
    {#if cookbooks.length > 0 && !cooking}
      <p class="shelves">
        <span class="shelves-label">{m['cookbooks.recipe.inLabel']()}</span>
        {#each cookbooks as shelf, index (shelf.id)}
          <a href={resolve('/(app)/cookbooks/[cookbookId]', { cookbookId: shelf.id })}
            >{shelf.name}</a
          >{#if index < cookbooks.length - 1}<span aria-hidden="true">, </span>{/if}
        {/each}
      </p>
    {/if}

    <!-- Where it started. Deliberately the quietest line on the page: this is
         an ordinary recipe now, and anything louder would make "imported" into
         a second kind of recipe. Reading only — at the hob, where it came from
         is the least useful fact on the screen. -->
    {#if recipe.origin && !cooking}
      <p class="origin">
        {#if recipe.origin.sourceUrl}
          <!-- Off site, and the one link on this page that is: resolve() is for
               this app's own routes, and there is nothing here to resolve. -->
          <!-- eslint-disable-next-line svelte/no-navigation-without-resolve -->
          <a href={recipe.origin.sourceUrl} rel="noreferrer nofollow" target="_blank">
            {m['import.origin.from']({ where: hostOf(recipe.origin.sourceUrl) })}
          </a>
        {:else}
          {m['import.origin.fromApp']()}
        {/if}
      </p>
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
             what this step needs, so "none written down" would be a lie about a
             recipe that has seven. -->
        <p class="empty">
          {cooking ? m['recipe.noneThisStep']() : m['recipe.noIngredients']()}
        </p>
      {/if}
    </section>

    <section class="steps" aria-label={m['recipe.steps']()}>
      <h2 class="section">{m['recipe.steps']()}</h2>

      {#if recipe.steps.length > 0}
        <ol class="list" bind:this={stepList}>
          {#each recipe.steps as step, index (step.id ?? index)}
            {@const needs = needsOf(step)}

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

                  <!-- Reading is planning: this is the step's own gathering
                       list, at the amounts on screen. Cooking is doing, and
                       there the panel to the left has already become it. -->
                  {#if needs.length > 0}
                    <StepNeeds ingredients={needs} {scaling} />
                  {/if}
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
      <!--
        Three actions of two ranks, never three of three. The two supporting
        ones share one look, so the accented "Start cooking" is the only thing
        on the bar asking to be pressed.

        On a phone they lose their words rather than their room: "Add to the
        shopping list" set across a third of a 360px screen is three wrapped
        lines and a bar half the height of the viewport. The label survives as
        the accessible name — which is what `label` is doing here, and why it
        is passed on every width rather than only on the narrow one.
      -->
      {#if onaddtocookbook}
        <Button size="lg" label={m['cookbooks.add.action']()} onclick={onaddtocookbook}>
          {#snippet icon()}
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <!-- A book, closed, spine to the left. -->
              <path
                d="M6.5 3.5H17a1 1 0 0 1 1 1v15a1 1 0 0 1-1 1H6.5a2 2 0 0 1-2-2v-13a2 2 0 0 1 2-2Z"
              />
              <path d="M8 3.5v17" />
            </svg>
          {/snippet}

          {m['cookbooks.add.action']()}
        </Button>
      {/if}

      {#if onaddtolist}
        <Button size="lg" label={m['shopping.addToList']()} onclick={onaddtolist}>
          {#snippet icon()}
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <!-- The same basket the shopping tab is marked with. -->
              <path d="M4 8h16l-1.4 10a2 2 0 0 1-2 1.7H7.4a2 2 0 0 1-2-1.7Z" />
              <path d="M9 8 12 3l3 5" />
            </svg>
          {/snippet}

          {m['shopping.addToList']()}
        </Button>
      {/if}

      <!-- Takes whatever the two icons leave: it is the action the page exists
           to offer, and a target's size should say so. -->
      <span class="advance">
        <Button variant="primary" size="lg" full onclick={onstartcooking}>
          {m['recipe.startCooking']()}
        </Button>
      </span>
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
  .shelves {
    margin-top: var(--space-3);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .origin {
    margin-top: var(--space-2);
    color: var(--text-subtle);
    font-size: var(--text-xs);
  }

  .shelves-label {
    margin-right: var(--space-1);
  }

  .shelves a {
    color: inherit;
  }

  .shelves a:hover {
    color: var(--text);
  }

  .printed-yield {
    display: none;
  }

  .hero {
    max-height: 24rem;
    overflow: hidden;
    border-radius: var(--radius-lg);
  }

  /* Baselines, not centres: the link sits on the first line of a title that
     may run to three of them. */
  .titleRow {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    justify-content: space-between;
    gap: var(--space-4);
  }

  .edit {
    flex: none;
    color: var(--text-muted);
    font-size: var(--text-sm);
    text-decoration: none;
  }

  .edit:hover {
    color: var(--text);
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
    margin-top: var(--space-3);
    max-width: var(--measure);
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

  .body {
    display: grid;
    grid-template-columns: minmax(0, 20rem) minmax(0, 1fr);
    gap: var(--layout-section-gap);
    align-items: start;
  }

  .ingredients {
    padding: clamp(1rem, 2vw, 1.5rem);
    background: var(--surface-sunken);
    border-radius: var(--radius-lg);
  }

  .section {
    font-family: var(--font-editorial);
    font-size: var(--text-2xl);
    font-weight: var(--weight-regular);
    letter-spacing: -0.025em;
    color: var(--text);
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

  .steps {
    min-width: 0;
  }

  .step {
    scroll-margin-block: var(--space-24) calc(var(--bottom-inset) + var(--space-24));
    padding-block: var(--space-6);
    border-top: 1px solid var(--border);
  }

  .step-body {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    line-height: var(--leading-relaxed);
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
    color: var(--accent);
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
    bottom: calc(max(var(--bottom-inset), env(safe-area-inset-bottom, 0px)) + var(--space-6));
    max-width: 100%;
    z-index: var(--z-sticky);
    /* Breathing room under the bar: while it floats, above whatever the shell
       has parked at the bottom; once the page ends, below its resting place. */
    margin-block-end: var(--space-8);
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: center;
    gap: var(--space-2);
    align-self: center;
    padding: var(--space-2);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
    box-shadow: var(--shadow-card);
  }

  .advance {
    display: flex;
    min-width: 0;
  }

  /* Step navigation is the cooking page's persistent action strip. Keeping
     this footer sticky too would put both controls at the same bottom inset. */
  .cooking .foot {
    position: static;
  }

  @media (width < 64rem) {
    .body {
      grid-template-columns: 1fr;
      gap: var(--space-8);
    }
  }

  /*
   * On a phone the bar is the width of the page and holds one row.
   *
   * Wrapping three worded buttons gave a different shape at every phone
   * width — two and one, one and two, three centred lines of different
   * lengths — and none of them read as a designed bar. Here the two
   * supporting actions are square targets of a fixed size and "Start cooking"
   * takes everything else, so the bar is the same shape on every phone and the
   * accented control is the one the thumb lands on.
   */
  @media (width < 52rem) {
    .foot {
      align-self: stretch;
      width: 100%;
    }

    /*
     * Takes the rest of the row, but never less than it can say "Start
     * cooking" in. Plain `flex: 1` let it shrink to a column of single
     * letters at 200% text rather than taking the line below, which is what
     * there is room for.
     */
    .advance {
      flex: 1 1 10rem;
    }

    /* Icon only: the words stay as the accessible name, set on the button. */
    .foot :global(.button.secondary .label) {
      display: none;
    }

    .foot :global(.button.secondary) {
      flex-shrink: 0;
      width: var(--control-md);
      padding-inline: 0;
    }

    /* Narrower than the default so "Start cooking" stays on one line down to
       320px, which is where the bar has the least room and needs it most. */
    .advance :global(.button) {
      padding-inline: var(--space-4);
    }

    /* The icon grows into the room the label left, to the size the icon-only
       controls elsewhere in the app are drawn at. */
    .foot :global(.button.secondary .icon) {
      width: var(--space-6);
      height: var(--space-6);
    }
  }

  @media screen and (max-height: 32rem) {
    .foot {
      position: static;
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
    .meta,
    .edit {
      display: none;
    }

    .shelves {
      margin-top: var(--space-3);
      color: var(--text-muted);
      font-size: var(--text-sm);
    }

    .shelves-label {
      margin-right: var(--space-1);
    }

    .shelves a {
      color: inherit;
    }

    .shelves a:hover {
      color: var(--text);
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

    .ingredients {
      padding: 0;
      background: none;
    }

    .steps .list {
      gap: 4mm;
    }
  }
</style>
