<script lang="ts">
  import { onMount } from 'svelte';
  import { Button, IconButton } from '$ds';
  import type { RecipeReading } from '$features/recipes/types';
  import StepText from '$features/recipes/surface/StepText.svelte';
  import { createScaling } from '$features/recipes/surface/scaled.svelte';
  import { haptics } from '$shell/haptics';
  import { m } from '$shell/i18n';
  import { cooking } from './stores/cooking.svelte';
  import { kitchenTimers as timers } from './kitchen.svelte';
  import StepTimer from './StepTimer.svelte';

  let { recipe, onreturn }: { recipe: RecipeReading; onreturn: () => void } = $props();
  let surface: HTMLElement;
  const index = $derived(
    Math.max(0, Math.min(cooking.session?.currentStepIndex ?? 0, recipe.steps.length - 1))
  );
  const step = $derived(recipe.steps[index]);
  const ready = $derived(cooking.session?.recipeId === recipe.id);
  const last = $derived(index >= recipe.steps.length - 1);
  const scaling = createScaling(
    () => recipe,
    () => cooking.session?.servings ?? recipe.yieldAmount
  );
  const stepTimer = $derived(timers.timers.find((timer) => timer.stepIndex === index));

  // The visible child supplies the clock even while the opener is backgrounded.
  // It shares the timer store's alarm claims, so it cannot ring a second time.
  onMount(() => {
    const child = surface.ownerDocument.defaultView ?? window;
    const stopClock = timers.tick(child);
    child.addEventListener('keydown', keys);
    return () => {
      stopClock();
      child.removeEventListener('keydown', keys);
    };
  });

  $effect(() => {
    const next = timers.nextStep;
    if (next !== undefined && ready && recipe.steps.length) {
      timers.consumeNextStep();
      cooking.moveTo(recipe.id, Math.min(next, recipe.steps.length - 1));
    }
  });

  function move(to: number) {
    if (!ready || to < 0 || to >= recipe.steps.length) return;
    haptics.step();
    cooking.moveTo(recipe.id, to);
  }
  function keys(event: KeyboardEvent) {
    if (event.defaultPrevented || event.ctrlKey || event.altKey || event.metaKey) return;
    // Events come from another Window: instanceof the opener's Element fails.
    const target = event.target as Element | null;
    if (target?.closest?.('button, a, input, select, textarea, [contenteditable]')) return;
    if (['ArrowRight', 'PageDown', 'ArrowLeft', 'PageUp'].includes(event.key)) {
      event.preventDefault();
      move(index + (['ArrowRight', 'PageDown'].includes(event.key) ? 1 : -1));
    }
  }
</script>

<main class="companion" bind:this={surface} tabindex="-1">
  <header>
    <p class="eyebrow">{m['cooking.pip.title']()}</p>
    <h1>{recipe.title}</h1>
    <p class="yield">{scaling.currentYieldLabel}</p>
    <Button size="sm" variant="ghost" onclick={onreturn}>{m['cooking.pip.return']()}</Button>
  </header>
  {#if step}
    <section class="step" aria-labelledby="pip-step">
      <p class="progress" aria-live="polite">
        {m['cooking.stepOf']({ current: index + 1, total: recipe.steps.length })}
      </p>
      <h2 id="pip-step">{step.title ?? m['recipe.step']({ number: index + 1 })}</h2>
      <div class="instructions"><StepText {step} {scaling} interactive={false} /></div>
      {#if step.durationSeconds !== null && !stepTimer}
        <StepTimer
          durationSeconds={step.durationSeconds}
          timer={undefined}
          secondsLeft={0}
          onstart={() =>
            timers.start(
              index,
              step.durationSeconds!,
              step.title ?? m['recipe.step']({ number: index + 1 })
            )}
          ondismiss={() => timers.dismiss(index)}
        />
      {/if}
    </section>
  {/if}
  {#if timers.timers.length}
    <section class="timers" aria-label={m['cooking.pip.timers']()}>
      <h2>{m['cooking.pip.timers']()}</h2>
      {#each timers.timers as timer (timer.stepIndex)}
        <div class="timer">
          <Button
            size="sm"
            variant="ghost"
            disabled={!ready || timer.stepIndex >= recipe.steps.length}
            onclick={() => move(timer.stepIndex)}>{timer.label}</Button
          >
          <StepTimer
            durationSeconds={0}
            {timer}
            secondsLeft={timers.remaining(timer)}
            onstart={() => {}}
            ondismiss={() => timers.dismiss(timer.stepIndex)}
            onpause={() => timers.pause(timer.stepIndex)}
            onresume={() => timers.resume(timer.stepIndex)}
          />
        </div>
      {/each}
    </section>
  {/if}
  <nav aria-label={m['cooking.pip.navigation']()}>
    <IconButton
      label={m['cooking.previous']()}
      size="lg"
      bordered
      disabled={!ready || index === 0}
      onclick={() => move(index - 1)}
    >
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
        ><path d="m14 6-6 6 6 6" stroke-linecap="round" stroke-linejoin="round" /></svg
      >
    </IconButton>
    <Button
      size="lg"
      variant="primary"
      full
      disabled={!ready}
      onclick={() => (last ? onreturn() : move(index + 1))}
    >
      {last ? m['cooking.pip.finish']() : m['cooking.next']()}
    </Button>
  </nav>
</main>

<style>
  .companion {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    min-height: 100dvh;
    padding: var(--space-4);
  }
  header {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-1);
  }
  .eyebrow,
  .yield,
  .progress {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
  h1 {
    font-family: var(--font-editorial);
    font-size: var(--text-xl);
  }
  h2 {
    font-size: var(--text-base);
  }
  .step {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    padding: var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-sunken);
  }
  .instructions {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    font-size: var(--text-lg);
  }
  .timers {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }
  .timer {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2);
    padding-block: var(--space-2);
    border-bottom: 1px solid var(--border);
  }
  nav {
    display: flex;
    gap: var(--space-2);
    margin-top: auto;
    position: sticky;
    bottom: 0;
    padding-block: var(--space-2);
    background: var(--surface);
  }
</style>
