<script lang="ts">
  import { Button, Chevron, IconButton } from '$ds';
  import type { Recipe } from '$features/recipes/types';
  import { m } from '$shell/i18n';
  import CookingControlsBar from './CookingControlsBar.svelte';
  import { kitchenTimers as timers } from './kitchen.svelte';
  import StepTimer from './StepTimer.svelte';

  interface Props {
    recipe: Recipe;
    currentStep: number;
    /** Whether there is a session to move within. */
    ready: boolean;
    /** The recipe page at the yield being cooked. */
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

  /** Timers outlive this screen, so prefer the step's own title ("Proving") over "Step 3". */
  const stepName = $derived(
    recipe.steps[currentStep]?.title ?? m['recipe.step']({ number: currentStep + 1 })
  );
</script>

<CookingControlsBar bind:height>
  {#if duration !== null}
    <div class="control-timer">
      <StepTimer
        size="lg"
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

  <!-- One toolbar, one size: every control is 56px (a wet thumb), the tools are circles and only Next is a labelled pill. -->
  <div class="control-summary">
    <div class="control-place">
      <!-- eslint-disable svelte/no-navigation-without-resolve -- The page resolves the route before appending the yield. -->
      <a
        class="recipe-return"
        href={recipeHref}
        aria-label={m['cooking.backToRecipe']()}
        title={m['cooking.backToRecipe']()}
      >
        <Chevron direction="back" />
        <span>{m['cooking.recipe']()}</span>
      </a>
      <!-- eslint-enable svelte/no-navigation-without-resolve -->
      <p class="progress">
        {m['cooking.stepOf']({ current: currentStep + 1, total: totalSteps })}
      </p>
    </div>

    <div class="tools">
      <IconButton label={m['notes.title']()} size="lg" bordered onclick={onnotes}>
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M5 4.5h14v15H5z" stroke-linejoin="round" />
          <path d="M8 8h8M8 12h8M8 16h5" stroke-linecap="round" />
        </svg>
      </IconButton>

      <IconButton label={m['kitchen.controls']()} size="lg" bordered onclick={onkitchen}>
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M4 7h16M4 17h16M9 4v6M15 14v6" stroke-linecap="round" />
        </svg>
      </IconButton>

      <!-- The name says what a press will do (start or stop); the fill shows it is running. -->
      <div class="auto-scroll" class:running={autoScrolling}>
        <IconButton
          size="lg"
          bordered
          label={autoScrolling ? m['cooking.autoScroll.stop']() : m['cooking.autoScroll.start']()}
          disabled={!ready}
          onclick={onautoscroll}
        >
          {#if autoScrolling}
            <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
              <rect x="7" y="7" width="10" height="10" rx="2" />
            </svg>
          {:else}
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path
                d="M12 4v13m-5-5 5 5 5-5M6 21h12"
                stroke-linecap="round"
                stroke-linejoin="round"
              />
            </svg>
          {/if}
        </IconButton>
      </div>
    </div>
  </div>

  <div class="moves">
    <!-- Previous is an icon so Next gets the width. -->
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

    <!-- One button that changes its label: swapping elements would drop keyboard focus on the last step. -->
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
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-3);
    min-width: 0;
  }

  .control-place {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0 var(--space-3);
    min-width: 0;
  }

  .progress {
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-variant-numeric: tabular-nums;
    overflow-wrap: normal;
    white-space: nowrap;
  }

  /* A navigation bar's back item: chevron and the place it returns to, in the accent. */
  .recipe-return {
    display: inline-flex;
    align-items: center;
    gap: var(--space-1);
    min-height: var(--control-sm);
    color: var(--accent);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    text-decoration: none;
  }

  .recipe-return:hover {
    color: var(--accent-hover);
  }

  .tools {
    display: flex;
    flex: none;
    align-items: center;
    gap: var(--space-2);
  }

  /* Running: filled with the accent, so the state reads at arm's length and not only from the icon. */
  .auto-scroll.running :global(.icon-button) {
    border-color: var(--accent);
    background: var(--accent);
    color: var(--accent-contrast);
  }

  .advance :global(.button) {
    padding-inline: var(--space-3);
  }

  .advance-label {
    overflow-wrap: normal;
    white-space: nowrap;
  }

  /* The place shrinks to the chevron alone before anything wraps: its accessible name stays. */
  @media screen and (max-width: 24rem) {
    .recipe-return span {
      display: none;
    }

    .recipe-return {
      min-width: var(--control-sm);
      justify-content: center;
    }

    .tools {
      gap: var(--space-1);
    }

    /* The tools keep their 56px, so the counter takes what is left, over two lines if "Schritt 12 von 12" does not fit one. */
    .progress {
      white-space: normal;
    }
  }
</style>
