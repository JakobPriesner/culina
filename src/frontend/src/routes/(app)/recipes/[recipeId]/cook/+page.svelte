<script lang="ts">
  import { onMount } from 'svelte';

  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { Button, Skeleton } from '$ds';
  import { busy } from '$shell/busy.svelte';
  import { cookLog } from '$features/cooking/stores/cookLog.svelte';
  import { cooking } from '$features/cooking/stores/cooking.svelte';
  import StepTimer from '$features/cooking/StepTimer.svelte';
  import { createTimers } from '$features/cooking/timers.svelte';
  import { createWakeLock } from '$features/cooking/wakeLock.svelte';
  import RecipeSurface from '$features/recipes/surface/RecipeSurface.svelte';
  import { recipes } from '$features/recipes/stores/recipes.svelte';
  import { urlAtYield, yieldFrom } from '$features/recipes/surface/yieldInUrl';
  import { m } from '$shell/i18n';
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

  const wakeLock = createWakeLock();
  const timers = createTimers(() => cooking.session?.sessionId ?? null);

  const currentStep = $derived(cooking.session?.currentStepIndex ?? 0);
  const totalSteps = $derived(recipes.detail?.steps.length ?? 0);

  /**
   * Whether there is a session to move within.
   *
   * The controls are drawn from the recipe, which arrives first; the session
   * they move is a separate request. Between the two, a tap on Next did
   * nothing at all — no step, no request, no explanation — which in a kitchen
   * reads as a broken button rather than as a slow one.
   */
  const ready = $derived(cooking.session?.recipeId === recipeId);

  onMount(() => {
    const stopHolding = wakeLock.engage();
    const stopTicking = timers.tick();
    // Nothing interrupts somebody at a hob — not even an offer. See
    // `$shell/busy`.
    const release = busy.hold();

    return () => {
      stopHolding();
      stopTicking();
      release();
    };
  });

  $effect(() => {
    if (recipeId) {
      void recipes.load(recipeId);
    }
  });

  // Starting is idempotent from the page's point of view: arriving here with a
  // session already going for this recipe simply resumes it.
  $effect(() => {
    const detail = recipes.detail;

    if (!detail || detail.id !== recipeId || !cooking.resolved) {
      return;
    }

    if (cooking.session?.recipeId !== recipeId) {
      void cooking.start(recipeId, servings).then(() => timers.load());
    } else {
      timers.load();
    }
  });

  function scale(value: number) {
    // See the detail page: `replaceState` moves the address bar without
    // telling the page, and every amount here is derived from the yield.
    void goto(urlAtYield(page.url, value, recipes.detail), {
      replaceState: true,
      keepFocus: true,
      noScroll: true
    });
    void cooking.rescale(value);
  }

  function move(index: number) {
    if (index >= 0 && index < totalSteps) {
      cooking.moveTo(index);
    }
  }

  async function finish(completed: boolean) {
    await cooking.end(completed);
    timers.clear();

    if (completed) {
      const recorded = await cookLog.record(recipeId, servings);

      // Done first, undo offered after: asking "are you sure?" before a one-tap
      // action that was never dangerous costs everyone a decision to protect
      // against a mistake that was already cheap to fix.
      toaster.show({
        message: m['cooking.madeIt.toast'](),
        tone: 'success',
        action: recorded
          ? {
              label: m['cooking.madeIt.undo'](),
              run: () => void cookLog.undo(recipeId, recorded.entryId)
            }
          : undefined
      });
    }

    await goto(
      urlAtYield(
        new URL(resolve('/(app)/recipes/[recipeId]', { recipeId }), page.url),
        servings,
        recipes.detail
      )
    );
  }

  const stepTimer = $derived(timers.timers.find((timer) => timer.stepIndex === currentStep));
  const duration = $derived(recipes.detail?.steps[currentStep]?.durationSeconds ?? null);
</script>

<svelte:head>
  <title>{recipes.detail?.title ?? m['recipes.title']()}</title>
</svelte:head>

<!-- The whole screen advances, because a cook's hands are busy and the target
     should be the phone rather than a button on it. Arrow keys for a laptop
     propped on the counter. -->
<svelte:window
  onkeydown={(event) => {
    if (event.key === 'ArrowRight' || event.key === 'PageDown') {
      move(currentStep + 1);
    } else if (event.key === 'ArrowLeft' || event.key === 'PageUp') {
      move(currentStep - 1);
    }
  }}
/>

<Page>
  {#if recipes.detail && recipes.detail.id === recipeId}
    <RecipeSurface
      recipe={recipes.detail}
      emphasis="cook"
      {servings}
      onservings={scale}
      {currentStep}
      onstep={move}
      onstopcooking={() => finish(false)}
    />

    <div class="controls">
      {#if duration !== null}
        <StepTimer
          durationSeconds={duration}
          timer={stepTimer}
          secondsLeft={stepTimer ? timers.remaining(stepTimer) : 0}
          onstart={() =>
            timers.start(currentStep, duration, m['recipe.step']({ number: currentStep + 1 }))}
          ondismiss={() => timers.dismiss(currentStep)}
        />
      {/if}

      <p class="progress">
        {m['cooking.stepOf']({ current: currentStep + 1, total: totalSteps })}
      </p>

      <div class="moves">
        <Button disabled={!ready || currentStep === 0} onclick={() => move(currentStep - 1)}>
          {m['cooking.previous']()}
        </Button>

        {#if currentStep < totalSteps - 1}
          <Button variant="primary" disabled={!ready} onclick={() => move(currentStep + 1)}>
            {m['cooking.next']()}
          </Button>
        {:else}
          <Button variant="primary" disabled={!ready} onclick={() => finish(true)}>
            {m['cooking.finish']()}
          </Button>
        {/if}
      </div>
    </div>
  {:else}
    <div aria-busy="true" aria-label={m['recipes.list.loading']()}>
      <Skeleton width="100%" height="12rem" />
    </div>
  {/if}
</Page>

<style>
  .controls {
    position: sticky;
    bottom: calc(var(--bottom-inset) + var(--space-4));
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-4);
    margin-top: var(--space-8);
    padding: var(--space-3) var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-overlay);
    box-shadow: var(--shadow-overlay);
  }

  .progress {
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-variant-numeric: tabular-nums;
  }

  .moves {
    display: flex;
    gap: var(--space-3);
  }
</style>
