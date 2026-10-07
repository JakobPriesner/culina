<script lang="ts">
  import { Button, IconButton } from '$ds';
  import type { Recipe } from '$features/recipes/types';
  import { m } from '$shell/i18n';
  import CookingControlsBar from './CookingControlsBar.svelte';
  import { kitchenTimers as timers } from './kitchen.svelte';
  import StepTimer from './StepTimer.svelte';

  /**
   * What is under the cook's thumb: the step's timer, where they are in the
   * recipe, and the moves between steps.
   */
  interface Props {
    recipe: Recipe;
    currentStep: number;
    /** Whether there is a session to move within. */
    ready: boolean;
    /** The recipe page, at the yield being cooked. */
    recipeHref: string;
    autoScrolling: boolean;
    height?: number;
    onmove: (index: number) => void;
    /** Forward, or done. */
    onadvance: () => void;
    onautoscroll: () => void;
    onnotes: () => void;
    onkitchen: () => void;
  }

  let {
    recipe,
    currentStep,
    ready,
    recipeHref,
    autoScrolling,
    height = $bindable(0),
    onmove,
    onadvance,
    onautoscroll,
    onnotes,
    onkitchen
  }: Props = $props();

  const totalSteps = $derived(recipe.steps.length);
  const onLastStep = $derived(currentStep >= totalSteps - 1);
  const stepTimer = $derived(timers.timers.find((timer) => timer.stepIndex === currentStep));
  const duration = $derived(recipe.steps[currentStep]?.durationSeconds ?? null);

  /**
   * What the timer is called once it has left the step behind.
   *
   * A timer outlives the screen it was started from — it shows in the bar with
   * four others — so it takes the step's own name when there is one. "Proving"
   * is findable among five running timers in a way "Step 3" is not.
   */
  const stepName = $derived(
    recipe.steps[currentStep]?.title ?? m['recipe.step']({ number: currentStep + 1 })
  );
</script>

<CookingControlsBar bind:height>
  {#if duration !== null}
    <div class="control-timer">
      <StepTimer
        durationSeconds={duration}
        timer={stepTimer}
        secondsLeft={stepTimer ? timers.remaining(stepTimer) : 0}
        onstart={() => timers.start(currentStep, duration, stepName)}
        ondismiss={() => timers.dismiss(currentStep)}
        onpause={() => timers.pause(currentStep)}
        onresume={() => timers.resume(currentStep)}
      />
    </div>
  {/if}

  <div class="control-summary">
    <div class="control-place">
      <!-- eslint-disable svelte/no-navigation-without-resolve -- The page resolves the route before appending the yield. -->
      <a
        class="recipe-return"
        href={recipeHref}
        aria-label={m['cooking.backToRecipe']()}
        title={m['cooking.backToRecipe']()}
      >
        <svg
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          aria-hidden="true"
        >
          <path d="m10 6-6 6 6 6M4 12h16" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
        <span>{m['cooking.recipe']()}</span>
      </a>
      <!-- eslint-enable svelte/no-navigation-without-resolve -->
      <p class="progress">
        {m['cooking.stepOf']({ current: currentStep + 1, total: totalSteps })}
      </p>
    </div>
    <Button
      size="sm"
      label={autoScrolling ? m['cooking.autoScroll.stop']() : undefined}
      disabled={!ready}
      onclick={onautoscroll}
    >
      {autoScrolling ? m['cooking.autoScroll.stopShort']() : m['cooking.autoScroll.start']()}
    </Button>
    <IconButton size="sm" bordered label={m['kitchen.controls']()} onclick={onkitchen}>
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <path d="M4 7h16M4 17h16M9 4v6M15 14v6" stroke-linecap="round" />
      </svg>
    </IconButton>
  </div>

  <div class="moves">
    <IconButton label={m['notes.title']()} size="lg" bordered onclick={onnotes}>
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
      onclick={() => onmove(currentStep - 1)}
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
      <Button size="lg" variant="primary" full disabled={!ready} onclick={onadvance}>
        <span class="advance-label">
          {onLastStep ? m['cooking.finish']() : m['cooking.next']()}
        </span>
      </Button>
    </div>
  </div>
</CookingControlsBar>

<style>
  .control-timer {
    grid-column: 1 / -1;
    min-width: 0;
  }

  .control-summary {
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto auto;
    align-items: center;
    gap: var(--space-2);
  }

  .progress {
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-variant-numeric: tabular-nums;
    overflow-wrap: normal;
  }

  .control-place {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    min-width: 0;
  }

  .recipe-return {
    display: inline-flex;
    align-items: center;
    gap: var(--space-1);
    min-height: var(--control-sm);
    color: var(--accent);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    text-decoration: none;
    border-radius: var(--radius-sm);
  }

  .recipe-return:hover {
    color: var(--accent-hover);
    text-decoration: underline;
    text-underline-offset: 0.2em;
  }

  .recipe-return svg {
    flex: none;
    width: var(--space-4);
    height: var(--space-4);
  }

  .advance :global(.button) {
    padding-inline: var(--space-3);
  }

  .advance-label {
    overflow-wrap: normal;
  }
</style>
