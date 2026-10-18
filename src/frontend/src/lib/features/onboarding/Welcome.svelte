<script lang="ts">
  import type { Component } from 'svelte';
  import { fly } from 'svelte/transition';

  import { Button, Stage } from '$ds';
  import { createStepGestures } from '$features/cooking/stepGestures';
  import { m } from '$shell/i18n';
  import type { Pose } from '$shell/olli/poses';

  import OlliSays from './OlliSays.svelte';
  import CaptureScene from './scenes/CaptureScene.svelte';
  import CookScene from './scenes/CookScene.svelte';
  import { moving } from './scenes/demo';
  import LoopScene from './scenes/LoopScene.svelte';
  import PlanScene from './scenes/PlanScene.svelte';
  import type { React } from './scenes/reaction';
  import StartScene from './scenes/StartScene.svelte';
  import StepScene from './scenes/StepScene.svelte';

  /**
   * Olli shows a new cook round: six short scenes, each a small working picture of the real thing to
   * try, with Olli saying one thing and answering what was tried. About a minute; skippable throughout;
   * it ends on the first recipe. See docs/onboarding-design.md.
   */
  interface Props {
    open: boolean;
    /** Closed, skipped or finished alike: not offered again. */
    onclose: () => void;
  }

  let { open, onclose }: Props = $props();

  type SceneId = 'hello' | 'capture' | 'steps' | 'plan' | 'cook' | 'start';

  interface Scene {
    id: SceneId;
    pose: Pose;
    title: () => string;
    says: () => string;
    demo: Component<{ onreact: React }> | null;
  }

  const scenes: readonly Scene[] = [
    {
      id: 'hello',
      pose: 'hello',
      title: m['tour.hello.title'],
      says: m['tour.hello.says'],
      demo: LoopScene
    },
    {
      id: 'capture',
      pose: 'reading',
      title: m['tour.capture.title'],
      says: m['tour.capture.says'],
      demo: CaptureScene
    },
    {
      id: 'steps',
      pose: 'writing',
      title: m['tour.steps.title'],
      says: m['tour.steps.says'],
      demo: StepScene
    },
    {
      id: 'plan',
      pose: 'thinking',
      title: m['tour.plan.title'],
      says: m['tour.plan.says'],
      demo: PlanScene
    },
    {
      id: 'cook',
      pose: 'onDuty',
      title: m['tour.cook.title'],
      says: m['tour.cook.says'],
      demo: CookScene
    },
    {
      id: 'start',
      pose: 'celebrating',
      title: m['tour.start.title'],
      says: m['tour.start.says'],
      demo: null
    }
  ];

  let index = $state(0);
  let welcome: HTMLElement | undefined = $state();
  /** Which way the last move went, so the next scene comes in from that side. */
  let direction = $state(1);
  /** What Olli answers to something tried in this scene; gone with the scene. */
  let reaction = $state<{ pose: Pose; say: string } | null>(null);

  const scene = $derived(scenes[index]!);
  const last = $derived(index === scenes.length - 1);

  function go(next: number) {
    if (next < 0 || next >= scenes.length) return;

    direction = next >= index ? 1 : -1;
    index = next;
    reaction = null;
    // A scene starts at its top, wherever the last one was scrolled to.
    welcome?.scrollIntoView({ block: 'start' });
  }

  const react: React = (pose, say) => (reaction = { pose, say });

  // As in cook mode: arrow keys, or a swipe across the scene; a touch on a control is left to the control.
  const gestures = createStepGestures({
    ready: () => open,
    currentStep: () => index,
    move: go,
    advance: () => go(index + 1)
  });

  const travel = $derived(moving() ? 32 : 0);
</script>

<svelte:window
  onkeydown={gestures.keydown}
  ontouchstart={gestures.touchstart}
  ontouchend={gestures.touchend}
/>

<Stage {open} hideTitle title={m['tour.label']()} closeLabel={m['tour.skip']()} {onclose}>
  <div class="welcome" bind:this={welcome}>
    {#key scene.id}
      <section
        class="scene"
        aria-labelledby="tour-scene-title"
        in:fly={{ x: travel * direction, duration: travel ? 320 : 0 }}
      >
        <h3 class="title" id="tour-scene-title">
          <span class="number">{index + 1}</span>
          {scene.title()}
        </h3>

        <div class="olli">
          <OlliSays pose={reaction?.pose ?? scene.pose} size="md" live>
            {#key reaction?.say ?? scene.id}<p>{reaction?.say ?? scene.says()}</p>{/key}
          </OlliSays>
        </div>

        {#if scene.demo}
          <scene.demo onreact={react} />
        {:else}
          <StartScene {onclose} />
        {/if}
      </section>
    {/key}
  </div>

  {#snippet footer()}
    <nav class="progress" aria-label={m['tour.progress.label']()}>
      {#each scenes as one, at (one.id)}
        <button
          type="button"
          class="dot"
          class:on={at === index}
          aria-label={m['tour.progress']({ current: at + 1, total: scenes.length })}
          aria-current={at === index ? 'step' : undefined}
          onclick={() => go(at)}
        ></button>
      {/each}
    </nav>

    {#if index > 0}
      <Button onclick={() => go(index - 1)}>{m['tour.back']()}</Button>
    {:else}
      <Button variant="ghost" onclick={onclose}>{m['tour.skip.short']()}</Button>
    {/if}

    {#if last}
      <Button onclick={onclose}>{m['tour.start.browse']()}</Button>
    {:else}
      <Button variant="primary" onclick={() => go(index + 1)}>{m['tour.next']()}</Button>
    {/if}
  {/snippet}
</Stage>

<style>
  .welcome {
    display: flex;
    flex-direction: column;
    min-height: 100%;
    touch-action: pan-y;
  }

  .scene {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    width: min(36rem, 100%);
    margin: auto;
    padding-block: var(--space-2) var(--space-4);
  }

  .title {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.02em;
  }

  .number {
    display: grid;
    place-items: center;
    width: 1.5rem;
    height: 1.5rem;
    border-radius: var(--radius-full);
    background: var(--accent);
    color: var(--accent-contrast);
    font-size: var(--text-xs);
  }

  .olli {
    align-self: flex-start;
    max-width: 30rem;
  }

  /* A short screen keeps its height for the demo. */
  @media (max-height: 44rem) {
    .olli :global(.olli.md) {
      width: 5rem;
      height: 5rem;
    }
  }

  .progress {
    display: flex;
    align-items: center;
    gap: var(--space-1);
    margin-inline-end: auto;
  }

  .dot {
    width: 0.6rem;
    height: 0.6rem;
    padding: 0;
    border: none;
    border-radius: var(--radius-full);
    background: var(--border-strong);
    cursor: pointer;
    transition:
      width var(--duration-base) var(--ease-out),
      background-color var(--duration-base) var(--ease-out);
  }

  /* A bigger target than it looks: 24px, as WCAG 2.5.8 asks, without bigger dots. */
  .dot {
    position: relative;
  }

  .dot::after {
    content: '';
    position: absolute;
    inset: -0.45rem -0.15rem;
  }

  .dot.on {
    width: 1.5rem;
    background: var(--accent);
  }

  .dot:focus-visible {
    outline: 2px solid var(--border-focus);
    outline-offset: 2px;
  }

  @media (prefers-reduced-motion: reduce) {
    .dot {
      transition: none;
    }
  }
</style>
