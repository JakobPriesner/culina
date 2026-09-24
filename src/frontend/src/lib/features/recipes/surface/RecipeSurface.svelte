<script lang="ts">
  import { tick } from 'svelte';

  import { resolve } from '$app/paths';

  import { Button, IconButton, Image, Popover, SegmentedControl } from '$ds';

  import { m } from '$shell/i18n';
  import {
    recallIngredientsView,
    rememberIngredientsView,
    type IngredientsView
  } from './ingredientsView';
  import { createScaling } from './scaled.svelte';
  import ScaleToAmountSheet from './ScaleToAmountSheet.svelte';
  import IngredientList from './IngredientList.svelte';
  import ServingsControl from './ServingsControl.svelte';
  import StepNeeds from './StepNeeds.svelte';
  import StepText from './StepText.svelte';
  import { imageSrcset, imageUrl } from '../recipeImage';
  import { metaLineFor } from '../recipeMeta';
  import {
    everyIngredient,
    ingredientsOf,
    type Ingredient,
    type RecipeReading,
    type Step
  } from '../types';

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
    /** Opens the sheet that hands out, and takes back, the link to this recipe. */
    onshare?: () => void;
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
    onshare,
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
   * Whether anything is parked at the bottom of the screen.
   *
   * One action, not four: the bar carries the thing the page exists to offer
   * and nothing else. A visitor following a share link cannot cook here —
   * cooking keeps a session, and they have none — and a bar floating over the
   * last step with nothing on it is worse than no bar.
   */
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

  const hasActions = $derived(cooking || canCook);

  /**
   * Whether the group beside the title has anything in it.
   *
   * The same question for the other end of the page: the supporting actions
   * are offered to whoever owns the recipe, and to nobody else.
   */
  const inMenu = $derived(!cooking && Boolean(editable || onaddtocookbook || onshare));
  const hasSupportingActions = $derived(inMenu || Boolean(!cooking && onaddtolist));

  /**
   * Closes the menu the pressed item is in, then does the thing.
   *
   * The browser closes a popover when the click lands outside it and not when
   * it lands on one of the choices, which is right for a panel of checkboxes
   * and wrong for a menu: every item here opens a sheet or leaves the page, and
   * a menu still hanging over it afterwards is a menu nobody dismissed.
   */
  function choose(event: MouseEvent, run?: () => void) {
    const panel = (event.currentTarget as HTMLElement).closest('[popover]');

    if (panel instanceof HTMLElement && typeof panel.hidePopover === 'function') {
      panel.hidePopover();
    }

    run?.();
  }

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

  let stepList = $state<HTMLOListElement>();

  /**
   * Whether the page has arrived.
   *
   * Arriving is not a move. A page that scrolls itself the moment it opens has
   * taken the cook somewhere they did not ask to go — unless it opened with the
   * step they are cooking under the controls, where it cannot be read. The
   * first run only rescues a step like that; every move after it follows.
   */
  let arrived = false;

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

    const moved = arrived;
    arrived = true;

    void resized(list).then(() => reveal(list.children[index], moved));
  });

  /**
   * The same rescue when the screen changes shape.
   *
   * A phone turned on its side reflows every step, and the one being cooked
   * lands wherever the reflow puts it. Width only: the address bar sliding
   * away as the cook scrolls changes the height, and answering that would pull
   * the page back out from under their thumb.
   */
  $effect(() => {
    const list = stepList;

    if (!cooking || !list) {
      return;
    }

    let width = window.innerWidth;

    const onresize = () => {
      if (window.innerWidth !== width) {
        width = window.innerWidth;
        void resized(list).then(() => reveal(list.children[currentStep], false));
      }
    };

    window.addEventListener('resize', onresize);

    return () => window.removeEventListener('resize', onresize);
  });

  /**
   * Brings a step into the part of the screen nothing is parked over.
   *
   * That part is the viewport less the `scroll-margin` on `.step`, which the
   * cook page sizes to its own controls — so "fits" means fits between the
   * header and the controls, not half a viewport that the controls may be
   * standing in. A step that fits is centred there, with the next one already
   * showing underneath; a longer one starts at the top, where it is read from.
   *
   * `followed` is a move, which always brings the step over. Otherwise the
   * page is left alone while the step is readable: it starts in the clear,
   * and ends there too when it can.
   */
  const reveal = (step: Element | undefined, followed: boolean) => {
    // `scrollIntoView` is missing in jsdom, and moving the screen is a
    // courtesy rather than behaviour the page depends on.
    if (!(step instanceof HTMLElement) || !step.scrollIntoView) {
      return;
    }

    const style = getComputedStyle(step);
    const top = parseFloat(style.scrollMarginTop) || 0;
    const bottom = window.innerHeight - (parseFloat(style.scrollMarginBottom) || 0);
    const box = step.getBoundingClientRect();
    const fits = box.height <= bottom - top;
    const readable = box.top >= top && box.top < bottom && (!fits || box.bottom <= bottom);

    if (!followed && readable) {
      return;
    }

    step.scrollIntoView({
      block: fits ? 'center' : 'start',
      // A move is followed smoothly, so it reads as the page following rather
      // than jumping — unless the person has said they do not want things
      // moving. A rescue is not something to watch.
      behavior:
        followed && !window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
          ? 'smooth'
          : 'auto'
    });
  };
</script>

<article class="surface" class:cooking class:perStep>
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

      <!--
        Everything except cooking, beside the title.

        One rank in one place. What used to be here was a bare "Edit" link while
        four unrelated actions floated at the bottom of the screen, which meant
        the page answered "what can I do with this recipe" in two places and in
        neither of them completely.

        The shopping list keeps its own control because it is the weekly loop —
        read a recipe, put it on the list — and a loop that runs twice a week
        does not belong behind a menu. The other three are occasional, so they
        go in one, the way a document's rarely-used actions do everywhere else.

        Nothing at all while cooking: hands are full, and the only correct edit
        then is the one made to the pan.
      -->
      {#if hasSupportingActions}
        <div class="actions">
          {#if onaddtolist}
            <Button label={m['shopping.addToList']()} onclick={onaddtolist}>
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

          {#if inMenu}
            <!-- Opening towards the middle of the page: the group is at the
                 inline end of a full-width row, and a panel that preferred the
                 other side would be hanging off the edge of the screen. -->
            <Popover placement="bottom-end">
              {#snippet trigger({ popovertarget })}
                <IconButton bordered label={m['recipe.moreActions']()} {popovertarget}>
                  <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
                    <circle cx="12" cy="5" r="1.6" />
                    <circle cx="12" cy="12" r="1.6" />
                    <circle cx="12" cy="19" r="1.6" />
                  </svg>
                </IconButton>
              {/snippet}

              <div class="menu">
                {#if onaddtocookbook}
                  <button class="item" type="button" onclick={(e) => choose(e, onaddtocookbook)}>
                    <svg
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      stroke-width="1.8"
                      stroke-linecap="round"
                      stroke-linejoin="round"
                      aria-hidden="true"
                    >
                      <!-- A book, closed, spine to the left. -->
                      <path
                        d="M6.5 3.5H17a1 1 0 0 1 1 1v15a1 1 0 0 1-1 1H6.5a2 2 0 0 1-2-2v-13a2 2 0 0 1 2-2Z"
                      />
                      <path d="M8 3.5v17" />
                    </svg>

                    {m['cookbooks.add.action']()}
                  </button>
                {/if}

                {#if onshare}
                  <button class="item" type="button" onclick={(e) => choose(e, onshare)}>
                    <svg
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      stroke-width="1.8"
                      stroke-linecap="round"
                      stroke-linejoin="round"
                      aria-hidden="true"
                    >
                      <!-- Three nodes and the two threads between them: the shape
                           every platform's share control has settled on, so
                           nobody has to learn what this one means. -->
                      <circle cx="18" cy="5" r="2.5" />
                      <circle cx="6" cy="12" r="2.5" />
                      <circle cx="18" cy="19" r="2.5" />
                      <path d="M8.2 10.8 15.8 6.4" />
                      <path d="m8.2 13.2 7.6 4.4" />
                    </svg>

                    {m['recipe.share.action']()}
                  </button>
                {/if}

                <!-- A link, not a button, and the other end of the editor's
                     "← Done": a recipe somebody is about to rewrite is one they
                     open in a second tab beside the one they are reading. -->
                {#if editable}
                  <a
                    class="item"
                    href={resolve('/(app)/recipes/[recipeId]/edit', { recipeId: recipe.id })}
                    onclick={(e) => choose(e)}
                  >
                    <svg
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      stroke-width="1.8"
                      stroke-linecap="round"
                      stroke-linejoin="round"
                      aria-hidden="true"
                    >
                      <path d="M4 20h4L19 9a2.1 2.1 0 0 0-3-3L5 17v3Z" />
                      <path d="m15 6 3 3" />
                    </svg>

                    {m['editor.edit']()}
                  </a>
                {/if}
              </div>
            </Popover>
          {/if}
        </div>
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
        yieldLabel: recipe.yieldLabel,
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
    {#if recipe.sourceUrl && !cooking}
      <p class="origin">
        <!-- Off site, and the one link on this page that is: resolve() is for
             this app's own routes, and there is nothing here to resolve. -->
        <!-- eslint-disable-next-line svelte/no-navigation-without-resolve -->
        <a href={recipe.sourceUrl} rel="noreferrer nofollow" target="_blank">
          {m['import.origin.from']({ where: hostOf(recipe.sourceUrl) })}
        </a>
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
      <div class="section-head">
        <h2 class="section">{m['recipe.ingredients']()}</h2>

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
      </div>

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

    <section class="steps" aria-label={m['recipe.steps']()}>
      <!-- The same wrapper the ingredients heading sits in, so the two
           headings line up across the columns whether or not this one has a
           control beside it. -->
      <div class="section-head">
        <h2 class="section">{m['recipe.steps']()}</h2>
      </div>

      {#if recipe.steps.length > 0}
        <ol class="list" bind:this={stepList}>
          {#each recipe.steps as step, index (step.id ?? index)}
            {@const needs = needsOf(step)}

            <li class="step" class:current={cooking && index === currentStep}>
              <!-- Beside the step, in the column the whole list would
                   otherwise fill — which is the arrangement's entire point:
                   what step two needs is level with step two. -->
              {#if perStep && needs.length > 0}
                <div class="step-needs">
                  <IngredientList
                    ingredients={needs}
                    {scaling}
                    {highlighted}
                    onhover={(id) => (highlighted = id)}
                  />
                </div>
              {/if}

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
                  <span class="number">{step.title ?? m['recipe.step']({ number: index + 1 })}</span
                  >
                  <StepText {step} {scaling} interactive={false} />
                </button>
              {:else}
                <div class="step-body">
                  <span class="number">{step.title ?? m['recipe.step']({ number: index + 1 })}</span
                  >

                  <!-- Under the step's own title and above its words, because
                       that is the order the step is carried out in: get these
                       out, then do this. At the end it was an afterthought to
                       a sentence already read, and the whole point of it is to
                       be read first.

                       Reading is planning: this is the step's own gathering
                       list, at the amounts on screen. Cooking is doing, and
                       there the panel to the left has already become it — as
                       has the column beside this step, once the reader has
                       asked for the ingredients by step. A lone step's list
                       is the panel beside it, so saying it twice says
                       nothing. -->
                  {#if !perStep && divisible && needs.length > 0}
                    <StepNeeds ingredients={needs} {scaling} />
                  {/if}

                  <StepText
                    {step}
                    {scaling}
                    {highlighted}
                    onhighlight={(id) => (highlighted = id)}
                    recipeIngredients={written}
                    onlocate={locateIngredient}
                  />
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

  {#if hasActions}
    <footer class="foot">
      {#if cooking}
        <Button size="lg" onclick={onstopcooking}>{m['recipe.stopCooking']()}</Button>
      {:else if canCook}
        <!--
          One button, alone, and the only thing this page parks at the bottom of
          the screen.

          It is here rather than beside the title because the decision is made
          at the end of the reading, not at the start of it: you look at the
          photograph, you read down the ingredients, you work out whether you
          have the cream — and by then the title is three screens up. Everything
          that is not that decision went to the title row, so what floats over
          the recipe is one accented control instead of a strip of four.
        -->
        <Button variant="primary" size="lg" onclick={onstartcooking}>
          {m['recipe.startCooking']()}
        </Button>
      {/if}
    </footer>
  {/if}
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

  .actions {
    display: flex;
    align-items: center;
    gap: var(--space-2);
  }

  /*
   * A menu of rows, not a panel of buttons.
   *
   * The full width each, so the icons line up down the left and the words down
   * beside them — which is what makes a list of three things scannable rather
   * than three separate controls that happen to be in one box.
   */
  .menu {
    display: flex;
    flex-direction: column;
    min-width: 12rem;
  }

  .item {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    min-height: var(--control-sm);
    padding: var(--space-2) var(--space-3);
    border: none;
    border-radius: var(--radius-md);
    background: none;
    color: var(--text);
    font: inherit;
    font-size: var(--text-sm);
    text-align: start;
    text-decoration: none;
    white-space: nowrap;
    cursor: pointer;
  }

  .item:hover {
    background: var(--surface-hover);
  }

  .item svg {
    flex: none;
    width: var(--space-4);
    height: var(--space-4);
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

  /*
   * The width of the ingredient column, named once.
   *
   * Three things have to agree on it or the page stops lining up: the body's
   * own columns, the column each step reserves for its ingredients, and the
   * heading row that carries the switch. It is declared here rather than in
   * the token scale because it is this page's proportion, not the app's.
   */
  .body {
    --ingredients-column: 20rem;
    /* The card's inset, named because four rules have to agree on it — two
       surfaces, and the heading that has to line up with one of them. */
    --card-padding: clamp(1rem, 2vw, 1.5rem);
    display: grid;
    grid-template-columns: minmax(0, var(--ingredients-column)) minmax(0, 1fr);
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
   * travels with them — see `.perStep .step-needs`.
   */
  .ingredients {
    padding: var(--card-padding);
    background: var(--surface-sunken);
    border-radius: var(--radius-lg);
  }

  /*
   * The method has no box, so its heading carries the inset the panel's box
   * gives the heading beside it. Without it the two headings sit a card's
   * padding apart, which is the kind of misalignment that is invisible in a
   * component and obvious on the page. The block only: the inline edge has to
   * stay level with the step text underneath it.
   */
  .steps > .section-head {
    padding-block-start: var(--card-padding);
    min-height: calc(var(--control-lg) + var(--card-padding));
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
   */
  /*
   * No height limit, deliberately.
   *
   * Capping the panel to the viewport and letting it scroll inside itself
   * reads as a bug long before it helps: an overlay scrollbar is invisible
   * until it is touched, so a fifteen-line list on an ordinary laptop is just
   * a card with its end cut off — and that is the common recipe, not the rare
   * one. A list taller than the screen keeps its top pinned instead, which is
   * the half you glance back at, and shows its end once the steps beside it
   * run out. Whole and occasionally clipped beats tidy and apparently broken.
   */
  .ingredients {
    position: sticky;
    top: var(--space-24);
  }

  /*
   * Beside the steps, the two columns are shared rather than owned.
   *
   * The panel keeps its column and its card — it is still where "Zutaten" is
   * answered — and the method's heading stays level with it, so the page opens
   * on the same two words in the same places as it does in the other
   * arrangement. What changes underneath: the list has been dealt out to the
   * steps, so each row below the headings has to reach across both columns.
   *
   * `display: contents` is what lets it. The steps section stops being a box
   * and its heading and its list become items of the grid above, which is the
   * only way a row of that list can start in the panel's column while the
   * heading above it stays in the method's.
   */
  .perStep .steps {
    display: contents;
  }

  .perStep .steps > .section-head {
    grid-column: 2;
    grid-row: 1;
  }

  .perStep .steps > .list,
  .perStep .steps > .empty {
    grid-column: 1 / -1;
    grid-row: 2;
  }

  .perStep .steps > .list {
    display: grid;
    grid-template-columns: subgrid;
  }

  /* Nothing to follow: what is left in the panel is the heading and whatever
     no step asked for, and the rest is already level with the step that needs
     it. */
  .perStep .ingredients {
    grid-column: 1;
    grid-row: 1;
    position: static;
  }

  .section-head {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2);
    /* Capped, not stretched: the switch then sits in the same place whether
       the section is the column or the whole width of the page. */
    max-width: var(--ingredients-column);
    /*
     * One height for both headings, whether or not a switch is sitting beside
     * this one. Without it the control makes its own row taller and its
     * heading rides down the middle of it, half a line below the heading in
     * the next column — the kind of misalignment that is invisible in a
     * component and obvious on the page.
     */
    min-height: var(--control-lg);
    margin-bottom: var(--space-3);
  }

  .section {
    font-family: var(--font-editorial);
    font-size: var(--text-2xl);
    font-weight: var(--weight-regular);
    letter-spacing: -0.025em;
    color: var(--text);
    margin-bottom: var(--space-3);
  }

  .section-head .section {
    margin-bottom: 0;
  }

  .leftovers {
    margin-bottom: var(--space-2);
    color: var(--text-subtle);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
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
    /* Clear of the page's own floating controls too; see `--controls-inset`. */
    scroll-margin-block: var(--space-24) calc(var(--bottom-inset) + var(--controls-inset));
    padding-block: var(--space-6);
    border-top: 1px solid var(--border);
  }

  /*
   * The step and what it needs, on one row.
   *
   * The body's own two tracks, borrowed rather than restated, so the
   * ingredients stay under the heading that names them and the method stays
   * where it was. The text column is placed explicitly because a step that
   * needs nothing has no first cell to push it across.
   */
  .perStep .step {
    grid-column: 1 / -1;
    display: grid;
    grid-template-columns: subgrid;
    align-items: start;
  }

  /*
   * The surface belongs to the ingredients, not to the row.
   *
   * The section spans both columns here so a step and its ingredients can
   * share one — but a background running under the whole row would put the
   * method on a panel too, and the method is prose: read straight down, once.
   * So the card moves in one level and onto the left, where it carries on down
   * the page from the panel at the top of that column. The same material in
   * the same column means the same thing in both arrangements: this is what
   * you need out.
   */
  .perStep .step-needs {
    grid-column: 1;
    padding: var(--card-padding);
    background: var(--surface-sunken);
    border-radius: var(--radius-lg);
  }

  .perStep .step-body {
    grid-column: 2;
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

  /*
   * Clear of whatever the shell has already parked at the bottom of the
   * viewport — the cooking bar, the phone's navigation bar, or both.
   *
   * No enclosure of its own any more. A panel around four controls was what
   * held them together as a bar; around one accented button it is a box drawn
   * around a box, and the button's own shadow already lifts it off the page.
   */
  .foot {
    position: sticky;
    bottom: calc(max(var(--bottom-inset), env(safe-area-inset-bottom, 0px)) + var(--space-6));
    max-width: 100%;
    z-index: var(--z-sticky);
    /* Breathing room under the button: while it floats, above whatever the
       shell has parked at the bottom; once the page ends, below its resting
       place. */
    margin-block-end: var(--space-8);
    display: flex;
    justify-content: center;
    align-self: center;
  }

  /* Step navigation is the cooking page's persistent action strip. Keeping
     this footer sticky too would put both controls at the same bottom inset. */
  .cooking .foot {
    position: static;
  }

  /*
   * `screen and`, because a sheet of A4 is narrower than this and is not a
   * phone: paper wants the two columns, and had to spend the whole print block
   * undoing these rules to get them back.
   */
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

    /* Nothing left to share, so the section is a section again and a step is
       a step with its ingredients under it. */
    .perStep .steps {
      display: block;
    }

    /* Stacked, so there is no heading in the next column to line up with. */
    .steps > .section-head {
      padding-block-start: 0;
      min-height: var(--control-lg);
    }

    .perStep .step {
      display: flex;
      flex-direction: column;
      gap: var(--space-4);
    }

    /* Under the step rather than beside it, because that is the room there is
       — and under it rather than over it, so the step's own number still
       introduces the step. The one place the two arrangements disagree about
       where "what this step needs" sits: a card between a step's title and its
       words would have to be a child of the step body, and here it is a cell
       of the row beside it. */
    .perStep .step-needs {
      order: 1;
    }

    .section-head {
      max-width: none;
    }
  }

  /*
   * On a phone it is the width of the page.
   *
   * A thumb reaching the bottom of a propped-up phone does not aim, so the one
   * control down there is given the whole line to land on.
   */
  @media (width < 52rem) {
    /* Under the title rather than beside it: a heading set at display size and
       a worded button cannot share a phone's line, and squeezing the heading to
       make them is giving up the wrong one. */
    .titleRow {
      grid-template-columns: minmax(0, 1fr);
    }

    .foot {
      align-self: stretch;
      width: 100%;
    }

    .foot :global(.button) {
      flex: 1;
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
    .actions {
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

    .perStep .step {
      gap: 8mm;
    }

    /* Ink, not three grey blocks. */
    .ingredients,
    .perStep .step-needs {
      padding: 0;
      background: none;
    }

    .section-head {
      min-height: 0;
    }

    .steps > .section-head {
      padding-block-start: 0;
    }

    .ingredients {
      position: static;
    }

    .steps .list {
      gap: 4mm;
    }
  }
</style>
