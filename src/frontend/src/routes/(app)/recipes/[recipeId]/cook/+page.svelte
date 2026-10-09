<script lang="ts">
  import { onMount, untrack } from 'svelte';

  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import type { AppError } from '$api';
  import { Button, ErrorState } from '$ds';
  import { busy } from '$shell/busy.svelte';
  import FormFailure from '$features/auth/FormFailure.svelte';
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

  /** Cooking is a route, not a flag, so it has a URL: it survives a reload and back behaves. */
  const recipeId = $derived(page.params.recipeId ?? '');
  const servings = $derived(yieldFrom(page.url, recipes.detail));

  const recipe = $derived(recipes.detail?.id === recipeId ? recipes.detail : null);
  const totalSteps = $derived(recipes.detail?.steps.length ?? 0);

  /** The step being cooked, clamped to the on-screen recipe because an edit elsewhere can shorten it; an inserted step above is a known limit (docs/domain-model.md). */
  const currentStep = $derived(
    Math.min(cooking.session?.currentStepIndex ?? 0, Math.max(0, totalSteps - 1))
  );

  /** Whether a session exists to move within; before it does, a tap on Next would silently do nothing. */
  const ready = $derived(cooking.session?.recipeId === recipeId);

  const onLastStep = $derived(currentStep >= totalSteps - 1);

  const recipeHref = $derived(
    urlAtYield(
      new URL(resolve('/(app)/recipes/[recipeId]', { recipeId }), page.url),
      servings,
      recipes.detail
    )
  );

  const advance = () => (onLastStep ? finish(true) : move(currentStep + 1));

  const gestures = createStepGestures({
    ready: () => ready,
    currentStep: () => currentStep,
    move,
    advance
  });

  onMount(() => {
    // Nothing interrupts somebody at a hob, not even an update offer; see `$shell/busy`.
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

  /** Set when cooking ends, never unset: ending clears the session, which the effect below watches and would restart. */
  let over = $state(false);

  /** Why no session could be started, so the cook is told rather than left with Next disabled. */
  let startFailure = $state<AppError | null>(null);

  async function startSession() {
    startFailure = await cooking.start(recipeId, servings, session.activeHouseholdId);

    if (!startFailure) {
      void timers.load();
    }
  }

  function retryStart() {
    startFailure = null;
    void startSession();
  }

  // Idempotent: arriving with a session already going for this recipe resumes it.
  $effect(() => {
    const detail = recipes.detail;

    if (over || !detail || detail.id !== recipeId || !cooking.resolved) {
      return;
    }

    if (cooking.session?.recipeId !== recipeId) {
      void startSession();
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

  // A resumed session may carry another yield from a deep link; adopt the displayed one so every kitchen view agrees.
  $effect(() => {
    const target = servings;
    const active = cooking.session?.sessionId;
    if (over || !ready || !active) return;
    untrack(() => {
      if (cooking.session?.servings !== target) void cooking.rescale(target);
    });
  });

  function scale(value: number) {
    // `replaceState` moves the address bar without telling the page (see the detail page), and every amount derives from the yield.
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
              // Done first, undo offered after: confirming a cheap, one-tap action costs more than it protects.
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

  /** Controls' height, measured because a timer, a second phone row and enlarged text change it; keeps a step out from under them. */
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

<!-- The whole screen advances: a cook's hands are busy, so the target is the phone, not a button. -->
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
      {#if startFailure}
        <div class="start-failure">
          <FormFailure failure={startFailure} message={m['cooking.start.failed']()} />

          <Button variant="primary" onclick={retryStart}>{m['error.retry']()}</Button>
        </div>
      {/if}

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
  /* Clearance above the controls: their height, their float gap and as much again. */
  .cook {
    --controls-inset: calc(var(--controls-height) + var(--space-8));
  }

  .start-failure {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-3);
    margin-block-end: var(--space-4);
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
