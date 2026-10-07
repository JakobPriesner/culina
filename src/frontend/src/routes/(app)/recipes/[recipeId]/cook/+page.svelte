<script lang="ts">
  import { onMount, untrack } from 'svelte';

  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { Button, ErrorState } from '$ds';
  import { busy } from '$shell/busy.svelte';
  import { session } from '$features/auth/session.svelte';
  import { autoScrollStep } from '$features/cooking/autoScrollStep';
  import CookingControls from '$features/cooking/CookingControls.svelte';
  import CookSkeleton from '$features/cooking/CookSkeleton.svelte';
  import { finishCooking } from '$features/cooking/finishCooking';
  import { cookLog } from '$features/cooking/stores/cookLog.svelte';
  import { cooking } from '$features/cooking/stores/cooking.svelte';
  import { kitchenTimers as timers } from '$features/cooking/kitchen.svelte';
  import KitchenSheet from '$features/cooking/KitchenSheet.svelte';
  import NotesSheet from '$features/cooking/NotesSheet.svelte';
  import { createStepGestures } from '$features/cooking/stepGestures';
  import RecipeSurface from '$features/recipes/surface/RecipeSurface.svelte';
  import { recipes } from '$features/recipes/stores/recipes.svelte';
  import { urlAtYield, yieldFrom } from '$features/recipes/surface/yieldInUrl';
  import { m } from '$shell/i18n';
  import { haptics } from '$shell/haptics';
  import NotFound from '$shell/NotFound.svelte';
  import Olli from '$shell/olli/Olli.svelte';
  import Page from '$shell/Page.svelte';
  import { toaster } from '$shell/toaster.svelte';

  /**
   * The same recipe, being cooked.
   *
   * A route rather than a flag, so cooking has a URL: it survives a reload, it
   * can be resumed on the phone propped against the bowl, and the back button
   * means what it looks like it means.
   */
  const recipeId = $derived(page.params.recipeId ?? '');
  const servings = $derived(yieldFrom(page.url, recipes.detail));

  /** The recipe on screen, once it is the one in the address. */
  const recipe = $derived(recipes.detail?.id === recipeId ? recipes.detail : null);
  const totalSteps = $derived(recipes.detail?.steps.length ?? 0);

  /**
   * Which step is being cooked, kept inside the recipe that is on screen.
   *
   * The session records a position by index, and a recipe edited from another
   * device can have fewer steps than it had when the cooking started. Clamping
   * means the worst case is being shown the last step rather than a blank
   * screen. It cannot detect a step *inserted* above this one — that needs the
   * position to be a step's identity rather than its place in a list, which is
   * recorded in docs/domain-model.md as a known limit.
   */
  const currentStep = $derived(
    Math.min(cooking.session?.currentStepIndex ?? 0, Math.max(0, totalSteps - 1))
  );

  /**
   * Whether there is a session to move within.
   *
   * The controls are drawn from the recipe, which arrives first; the session
   * they move is a separate request. Between the two, a tap on Next did
   * nothing at all — no step, no request, no explanation — which in a kitchen
   * reads as a broken button rather than as a slow one.
   */
  const ready = $derived(cooking.session?.recipeId === recipeId);

  const onLastStep = $derived(currentStep >= totalSteps - 1);

  /** The recipe page, at the yield being cooked. */
  const recipeHref = $derived(
    urlAtYield(
      new URL(resolve('/(app)/recipes/[recipeId]', { recipeId }), page.url),
      servings,
      recipes.detail
    )
  );

  /** Forward, or done — the same control, because it is the same gesture. */
  const advance = () => (onLastStep ? finish(true) : move(currentStep + 1));

  const gestures = createStepGestures({
    ready: () => ready,
    currentStep: () => currentStep,
    move,
    advance
  });

  onMount(() => {
    // Nothing interrupts somebody at a hob — not even an offer. See
    // `$shell/busy`.
    const release = busy.hold();

    return () => {
      release();
    };
  });

  $effect(() => {
    if (recipeId) {
      void recipes.load(recipeId);
    }
  });

  /**
   * Set the moment cooking is over, and never unset.
   *
   * Ending clears the session, and clearing the session is exactly what the
   * effect below watches for — so without this, finishing started a fresh
   * session on the way out, and the cook arrived back at the recipe with the
   * bar still telling them something was on the hob.
   */
  let over = $state(false);

  // Starting is idempotent from the page's point of view: arriving here with a
  // session already going for this recipe simply resumes it.
  $effect(() => {
    const detail = recipes.detail;

    if (over || !detail || detail.id !== recipeId || !cooking.resolved) {
      return;
    }

    if (cooking.session?.recipeId !== recipeId) {
      void cooking.start(recipeId, servings, session.activeHouseholdId).then(() => timers.load());
    } else {
      timers.load();
    }
  });

  $effect(() => {
    const index = timers.nextStep;
    if (index !== undefined && ready && totalSteps > 0) {
      timers.consumeNextStep();
      cooking.moveTo(recipeId, Math.min(index, totalSteps - 1));
    }
  });

  // A resumed session may carry a different yield from a cooking deep link.
  // Make that displayed yield the session's yield, so every kitchen view agrees.
  $effect(() => {
    const target = servings;
    const active = cooking.session?.sessionId;
    if (over || !ready || !active) return;
    untrack(() => {
      if (cooking.session?.servings !== target) void cooking.rescale(target);
    });
  });

  function scale(value: number) {
    // See the detail page: `replaceState` moves the address bar without
    // telling the page, and every amount here is derived from the yield.
    void goto(urlAtYield(page.url, value, recipes.detail), {
      replaceState: true,
      keepFocus: true,
      noScroll: true
    });
  }

  function move(index: number) {
    if (ready && index >= 0 && index < totalSteps) {
      haptics.step();
      cooking.moveTo(recipeId, index);
    }
  }

  async function finish(completed: boolean) {
    over = true;

    const { closed, recorded } = await finishCooking(
      recipeId,
      servings,
      session.activeHouseholdId,
      completed
    );

    if (completed) {
      toaster.show(
        recorded && closed
          ? {
              message: () => m['cooking.madeIt.toast'](),
              tone: 'success',
              art: cookedArt,
              // Done first, undo offered after: asking "are you sure?" before a
              // one-tap action that was never dangerous costs everyone a
              // decision to protect against a mistake that was already cheap
              // to fix.
              action: {
                label: () => m['cooking.madeIt.undo'](),
                run: () => void cookLog.undo(recipeId, recorded.entryId)
              }
            }
          : { message: () => m['cooking.madeIt.failed'](), tone: 'danger' }
      );
    }

    await goto(recipeHref);
  }

  /**
   * How tall the controls are, so the surface can keep a step out from under
   * them. Measured, because a timer, a phone's second row and enlarged text
   * all change it.
   */
  let controlsHeight = $state(0);
  let autoScrolling = $state(false);
  let notesOpen = $state(false);
  let kitchenOpen = $state(false);
</script>

{#snippet cookedArt()}
  <Olli pose="celebrating" size="sm" still />
{/snippet}

<svelte:head>
  <title>{recipes.detail?.title ?? m['recipes.title']()}</title>
</svelte:head>

<!-- The whole screen advances, because a cook's hands are busy and the target
     should be the phone rather than a button on it. -->
<svelte:window
  onkeydown={gestures.keydown}
  ontouchstart={gestures.touchstart}
  ontouchend={gestures.touchend}
/>

<Page>
  {#if recipe}
    <div
      class="cook"
      style:--controls-height="{controlsHeight}px"
      use:autoScrollStep={{
        enabled: autoScrolling && ready && !over,
        step: currentStep,
        suspended: notesOpen || kitchenOpen,
        onstop: () => (autoScrolling = false)
      }}
    >
      <RecipeSurface
        {recipe}
        emphasis="cook"
        {servings}
        onservings={scale}
        {currentStep}
        onstep={move}
        onstopcooking={() => finish(false)}
      />

      <CookingControls
        {recipe}
        {currentStep}
        {ready}
        {recipeHref}
        {autoScrolling}
        bind:height={controlsHeight}
        onmove={move}
        onadvance={advance}
        onautoscroll={() => (autoScrolling = !autoScrolling)}
        onnotes={() => (notesOpen = true)}
        onkitchen={() => (kitchenOpen = true)}
      />
    </div>
  {:else if recipes.detailStatus === 'failed' && recipes.detailError?.status === 404}
    <NotFound kind="recipe" level={1} />
  {:else if recipes.detailStatus === 'failed'}
    <ErrorState
      title={m['recipes.failed.title']()}
      body={m['recipes.failed.body']()}
      requestIdLabel={m['error.reference']()}
      requestId={recipes.detailError?.requestId}
    >
      {#snippet action()}
        <Button variant="primary" onclick={() => recipes.load(recipeId)}>
          {m['error.retry']()}
        </Button>
      {/snippet}
    </ErrorState>
  {:else}
    <CookSkeleton />
  {/if}
</Page>

<KitchenSheet open={kitchenOpen} {recipe} onclose={() => (kitchenOpen = false)} />
<NotesSheet open={notesOpen} {recipeId} onclose={() => (notesOpen = false)} />

<style>
  /* What the controls stand over: their own height, the gap they float at and
     as much again, so a step's last line is not flush against them. */
  .cook {
    --controls-inset: calc(var(--controls-height) + var(--space-8));
  }

  @media (width < 64rem) {
    .cook {
      --step-top-inset: calc(env(safe-area-inset-top, 0px) + var(--space-4));
    }
  }

  @media screen and (max-height: 32rem) {
    .cook {
      --controls-inset: var(--space-8);
    }
  }
</style>
