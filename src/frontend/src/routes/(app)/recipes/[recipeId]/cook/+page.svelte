<script lang="ts">
  import { onMount } from 'svelte';

  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { Button, IconButton, Sheet, Skeleton } from '$ds';
  import { busy } from '$shell/busy.svelte';
  import { session } from '$features/auth/session.svelte';
  import { cookLog } from '$features/cooking/stores/cookLog.svelte';
  import PersonalNotePanel from '$features/cooking/PersonalNotePanel.svelte';
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

  /** Forward, or done — the same control, because it is the same gesture. */
  const advance = () => (onLastStep ? finish(true) : move(currentStep + 1));

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
    if (ready && index >= 0 && index < totalSteps) {
      cooking.moveTo(recipeId, index);
    }
  }

  async function finish(completed: boolean) {
    over = true;

    const closed = await cooking.end(completed);
    timers.clear();

    if (completed) {
      const recorded = await cookLog.record(recipeId, servings, session.activeHouseholdId);

      // Both halves of "I made it" are reported on, not just the one that
      // happens to have a toast. Saying it was added when the attempt never
      // reached the server, or when the session it belongs to is still open,
      // is worse than saying nothing: the history is the only place anyone
      // would go to check.
      toaster.show(
        recorded && closed
          ? {
              message: () => m['cooking.madeIt.toast'](),
              tone: 'success',
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

  /**
   * What the timer is called once it has left the step behind.
   *
   * A timer outlives the screen it was started from — it shows in the bar with
   * four others — so it takes the step's own name when there is one. "Proving"
   * is findable among five running timers in a way "Step 3" is not.
   */
  const stepName = $derived(
    recipes.detail?.steps[currentStep]?.title ?? m['recipe.step']({ number: currentStep + 1 })
  );

  /**
   * How tall the controls are, so the surface can keep a step out from under
   * them. Measured, because a timer, a phone's second row and enlarged text
   * all change it.
   */
  let controlsHeight = $state(0);
  let notesOpen = $state(false);

  function stepFromKeyboard(event: KeyboardEvent) {
    if (!ready || event.defaultPrevented) {
      return;
    }

    const target = event.target;

    if (
      target instanceof HTMLElement &&
      target.closest('input, textarea, select, button, a, [contenteditable="true"]')
    ) {
      return;
    }

    if (event.key === 'ArrowRight' || event.key === 'PageDown') {
      event.preventDefault();
      move(currentStep + 1);
    } else if (event.key === 'ArrowLeft' || event.key === 'PageUp') {
      event.preventDefault();
      move(currentStep - 1);
    }
  }
</script>

<svelte:head>
  <title>{recipes.detail?.title ?? m['recipes.title']()}</title>
</svelte:head>

<!-- The whole screen advances, because a cook's hands are busy and the target
     should be the phone rather than a button on it. Arrow keys for a laptop
     propped on the counter. -->
<svelte:window onkeydown={stepFromKeyboard} />

<Page>
  {#if recipes.detail && recipes.detail.id === recipeId}
    <div class="cook" style:--controls-height="{controlsHeight}px">
      <RecipeSurface
        recipe={recipes.detail}
        emphasis="cook"
        {servings}
        onservings={scale}
        {currentStep}
        onstep={move}
        onstopcooking={() => finish(false)}
      />

      <div class="controls" bind:clientHeight={controlsHeight}>
        {#if duration !== null}
          <StepTimer
            durationSeconds={duration}
            timer={stepTimer}
            secondsLeft={stepTimer ? timers.remaining(stepTimer) : 0}
            onstart={() => timers.start(currentStep, duration, stepName)}
            ondismiss={() => timers.dismiss(currentStep)}
          />
        {/if}

        <p class="progress">
          {m['cooking.stepOf']({ current: currentStep + 1, total: totalSteps })}
        </p>

        <div class="moves">
          <IconButton
            label={m['notes.title']()}
            size="lg"
            bordered
            onclick={() => (notesOpen = true)}
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M5 4.5h14v15H5z" stroke-linejoin="round" />
              <path d="M8 8h8M8 12h8M8 16h5" stroke-linecap="round" />
            </svg>
          </IconButton>

          <!-- The largest control size, because these are pressed with a wet
             thumb while looking at a pan rather than at the screen.
             Measured at 320 px, "Previous step" used to be the *wider* of the
             two simply because it is a longer phrase — the control that undoes
             progress was an easier target than the one pressed at every step.
             It is an icon now, and next takes the room that frees. -->
          <IconButton
            label={m['cooking.previous']()}
            size="lg"
            bordered
            disabled={!ready || currentStep === 0}
            onclick={() => move(currentStep - 1)}
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="m14 6-6 6 6 6" stroke-linecap="round" stroke-linejoin="round" />
            </svg>
          </IconButton>

          <!-- One control that changes what it says, not two that replace each
             other. Swapping the element loses focus at exactly the moment
             somebody reaches the last step, which for a keyboard user means
             tabbing back into the page to finish. -->
          <div class="advance">
            <Button size="lg" variant="primary" full disabled={!ready} onclick={advance}>
              {onLastStep ? m['cooking.finish']() : m['cooking.next']()}
            </Button>
          </div>
        </div>
      </div>
    </div>
  {:else}
    <div class="cook-skeleton" aria-busy="true" aria-label={m['recipes.list.loading']()}>
      <div class="back">
        <Skeleton width="6rem" height="1.5rem" />
      </div>

      <div class="cook-head">
        <Skeleton width="50%" height="2rem" />
        <Skeleton width="25%" height="1rem" />
      </div>

      <div class="cook-servings">
        <Skeleton shape="block" width="8.5rem" height="var(--control-sm)" />
      </div>

      <div class="cook-step-card">
        <div class="cook-step-header">
          <Skeleton shape="circle" width="2.25rem" height="2.25rem" />
          <Skeleton width="5rem" height="1.25rem" />
        </div>
        <div class="cook-step-body">
          <Skeleton width="95%" height="1.25rem" />
          <Skeleton width="85%" height="1.25rem" />
          <Skeleton width="70%" height="1.25rem" />
        </div>
      </div>

      <div class="controls" aria-hidden="true">
        <Skeleton width="6rem" height="1rem" />
        <div class="moves">
          <Skeleton shape="circle" width="var(--control-lg)" height="var(--control-lg)" />
          <div class="advance">
            <Skeleton shape="block" width="9rem" height="var(--control-lg)" />
          </div>
        </div>
      </div>
    </div>
  {/if}
</Page>

<Sheet
  open={notesOpen}
  title={m['notes.title']()}
  closeLabel={m['picker.close']()}
  onclose={() => (notesOpen = false)}
>
  <PersonalNotePanel {recipeId} variant="cook" />
</Sheet>

<style>
  /* What the controls stand over: their own height, the gap they float at and
     as much again, so a step's last line is not flush against them. */
  .cook {
    --controls-inset: calc(var(--controls-height) + var(--space-8));
  }

  .cook-skeleton {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
  }

  .cook-head {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }

  .cook-servings {
    padding-block: var(--space-4);
    border-block: 1px solid var(--border);
  }

  .cook-step-card {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    padding: var(--space-6);
    background: var(--surface-sunken);
    border-radius: var(--radius-lg);
    min-height: 14rem;
  }

  .cook-step-header {
    display: flex;
    align-items: center;
    gap: var(--space-3);
  }

  .cook-step-body {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .controls {
    position: sticky;
    bottom: calc(max(var(--bottom-inset), env(safe-area-inset-bottom, 0px)) + var(--space-4));
    z-index: var(--z-sticky);
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
    flex: 1;
    min-width: 0;
    gap: var(--space-2);
  }

  /* Next takes whatever is left. It is pressed once per step and Previous is
     pressed when something went wrong, and a target's size should say which is
     which. */
  .advance {
    flex: 1;
    min-width: 0;
  }

  /* On a phone the progress line takes its own row, so the controls have the
     whole width rather than whatever the words beside them left over. */
  @media (width < 40rem) {
    .controls {
      flex-direction: column;
      align-items: stretch;
    }

    .progress {
      text-align: center;
    }
  }
  @media screen and (max-height: 32rem) {
    .cook {
      --controls-inset: var(--space-8);
    }

    .controls {
      position: static;
    }
  }
</style>
