<script lang="ts">
  import { Button, Chevron, IconButton } from '$ds';
  import type { Recipe } from '$features/recipes/types';
  import { m } from '$shell/i18n';
  import CookingControlsBar from './CookingControlsBar.svelte';
  import { kitchenTimers as timers } from './kitchen.svelte';
  import { durationsIn } from './stepDurations';
  import StepTimer from './StepTimer.svelte';

  interface Props {
    recipe: Recipe;
    currentStep: number;
    /** Whether there is a session to move within. */
    ready: boolean;
    /** The recipe page at the yield being cooked. */
    recipeHref: string;
    autoScrolling: boolean;
    /** Whether you wrote a note on this recipe, so the notes button says there is something to read. */
    noted?: boolean;
    height?: number;
    onmove: (index: number) => void;
    /** Forward, or done. */
    onadvance: () => void;
    onautoscroll: () => void;
    onnotes: () => void;
    onkitchen: () => void;
    /** Steps read aloud, where the device can speak; absent where it cannot. */
    readingAloud?: boolean;
    onreadaloud?: () => void;
  }

  let {
    recipe,
    currentStep,
    ready,
    recipeHref,
    autoScrolling,
    noted = false,
    height = $bindable(0),
    onmove,
    onadvance,
    onautoscroll,
    onnotes,
    onkitchen,
    readingAloud = false,
    onreadaloud
  }: Props = $props();

  const totalSteps = $derived(recipe.steps.length);
  const onLastStep = $derived(currentStep >= totalSteps - 1);
  const stepTimer = $derived(timers.timers.find((timer) => timer.stepIndex === currentStep));
  const duration = $derived(recipe.steps[currentStep]?.durationSeconds ?? null);

  /** The waits the step's words name ("4 Min. braten"), offered as timers when the recipe set none for it. */
  const written = $derived.by(() => {
    const step = recipe.steps[currentStep];

    return step && duration === null
      ? durationsIn(
          step.segments.map((segment) => (segment.kind === 'text' ? segment.text : '')).join(' ')
        )
      : [];
  });

  /** Timers outlive this screen, so prefer the step's own title ("Proving") over "Step 3". */
  const stepName = $derived(
    recipe.steps[currentStep]?.title ?? m['recipe.step']({ number: currentStep + 1 })
  );
</script>

<CookingControlsBar bind:height>
  {#if duration !== null || stepTimer}
    <div class="control-timer">
      <StepTimer
        size="lg"
        durationSeconds={duration ?? 0}
        timer={stepTimer}
        secondsLeft={stepTimer ? timers.remaining(stepTimer) : 0}
        onstart={() => duration !== null && timers.start(currentStep, duration, stepName)}
        ondismiss={() => timers.dismiss(currentStep)}
        onpause={() => timers.pause(currentStep)}
        onresume={() => timers.resume(currentStep)}
      />
    </div>
  {:else if written.length > 0}
    <!-- One tap per wait the step names; a range starts at its lower bound, since early is safe and late is not. -->
    <div class="control-timer written" role="group" aria-label={m['cooking.timer.written']()}>
      {#each written as seconds (seconds)}
        <Button
          size="lg"
          label={m['cooking.timer.start']({ minutes: Math.round(seconds / 60) })}
          onclick={() => timers.start(currentStep, seconds, stepName)}
        >
          {#snippet icon()}
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="13" r="7.5" />
              <path d="M12 9.5V13l2.5 2M10 2.5h4M12 2.5v3" stroke-linecap="round" />
            </svg>
          {/snippet}
          {m['cooking.timer.minutes']({ minutes: Math.round(seconds / 60) })}
        </Button>
      {/each}
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
      <span class="notes" class:noted>
        <IconButton
          label={noted ? m['notes.marked']() : m['notes.title']()}
          size="lg"
          bordered
          onclick={onnotes}
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M5 4.5h14v15H5z" stroke-linejoin="round" />
            <path d="M8 8h8M8 12h8M8 16h5" stroke-linecap="round" />
          </svg>
        </IconButton>
      </span>

      {#if onreadaloud}
        <!-- Hands busy, eyes on the pan: the step said out loud, each time it changes. -->
        <div class="auto-scroll" class:running={readingAloud}>
          <IconButton
            label={readingAloud ? m['cooking.readAloud.stop']() : m['cooking.readAloud.start']()}
            size="lg"
            bordered
            onclick={onreadaloud}
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M4 9.5h3.5L12 6v12l-4.5-3.5H4z" stroke-linejoin="round" />
              {#if readingAloud}
                <path d="M15.5 9a4 4 0 0 1 0 6M18 6.5a7.5 7.5 0 0 1 0 11" stroke-linecap="round" />
              {:else}
                <path d="m16 10 4 4m0-4-4 4" stroke-linecap="round" />
              {/if}
            </svg>
          </IconButton>
        </div>
      {/if}

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
            <!-- A page with an arrow running down and back up it: the page moving by itself, not a download (an arrow into a tray). -->
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <rect x="5" y="3" width="14" height="18" rx="3" />
              <path
                d="M12 7.5v9m-2.5-6.5 2.5-2.5 2.5 2.5m-5 4 2.5 2.5 2.5-2.5"
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
  /* A dot on the notes button when there is a note: what you wrote last time is worth a look before the step. */
  .notes {
    position: relative;
    display: inline-flex;
  }

  .noted::after {
    content: '';
    position: absolute;
    top: var(--space-1);
    right: var(--space-1);
    width: 0.625rem;
    height: 0.625rem;
    border: 2px solid var(--surface);
    border-radius: var(--radius-full);
    background: var(--accent);
    pointer-events: none;
  }

  .control-timer {
    grid-column: 1 / -1;
    min-width: 0;
  }

  /* Shares one row when it can: a second row would push the step up under the cook's eyes. */
  .written {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  /* A short label needs less side room than a sentence; it must stay on one line. */
  .written > :global(.button) {
    flex: 1 1 0;
    padding-inline: var(--space-3);
    white-space: nowrap;
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

  @media (hover: hover) {
    .recipe-return:hover {
      color: var(--accent-hover);
    }
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

    /* Four 56px tools leave the counter no room beside them: it keeps a line with the back chevron, the tools the row below. */
    .control-summary {
      flex-wrap: wrap;
      row-gap: var(--space-1);
    }

    .control-place {
      flex: 1 0 100%;
      flex-wrap: nowrap;
    }

    .tools {
      flex: 1 0 100%;
      justify-content: space-between;
    }
  }
</style>
