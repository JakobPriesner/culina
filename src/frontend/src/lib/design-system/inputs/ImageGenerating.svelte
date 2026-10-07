<script lang="ts">
  import type { Snippet } from 'svelte';
  import { MediaQuery } from 'svelte/reactivity';
  import { fade } from 'svelte/transition';

  import GenerationAura from '../feedback/GenerationAura.svelte';
  import GenerationStatus from '../feedback/GenerationStatus.svelte';

  /**
   * What an image field's frame shows while a picture is being drawn.
   *
   * Fills the (positioned) frame it is placed in. The fade-out is global
   * because the field, not this component, decides when it goes.
   */
  interface Props {
    /** What the frame says while it draws. */
    label?: string | undefined;
    /** Optional decoration shown beside the status. */
    art?: Snippet | undefined;
    /** Whether the decorative background may move. */
    animate?: boolean;
  }

  let { label, art, animate = true }: Props = $props();

  const reducedMotion = new MediaQuery('(prefers-reduced-motion: reduce)', false);
</script>

<!-- Over whatever is underneath, because a picture being drawn is about
     to replace it. The frame does the waiting rather than a spinner in a
     button: this is a minute of somebody else's machine working, and a
     spinner that size says "a moment".

     The assistant's four lights drift under the frame while their glow
     runs round its edge. Nothing in it fills up or reaches an end: the
     provider reports elapsed time, not percentage complete, and the
     interface must not invent one. -->
<div
  class="generating-overlay"
  class:paused={!animate}
  out:fade|global={{ duration: reducedMotion.current ? 0 : 260 }}
>
  <div class="drawing" aria-hidden="true">
    <span class="wash wash-one"></span>
    <span class="wash wash-two"></span>
    <span class="wash wash-three"></span>
    <span class="wash wash-four"></span>
  </div>

  <GenerationAura over />

  {#if label}
    <div class="caption">
      <div class="art">
        {@render art?.()}
      </div>
      <GenerationStatus {label} tone="on-media" align="center" indicator={!art} />
    </div>
  {/if}
</div>

<style>
  .generating-overlay,
  .drawing {
    position: absolute;
    inset: 0;
    overflow: hidden;
    border-radius: var(--radius-lg);
  }

  .generating-overlay {
    z-index: 2;
    background: var(--generating-ground);
  }

  /* Light through frosted glass: four soft colours drifting at different
     speeds, so the mix underneath never quite repeats. */
  .drawing {
    background: var(--generating-ground);
  }

  .wash {
    position: absolute;
    border-radius: var(--radius-full);
    filter: blur(32px);
    opacity: 0.7;
    will-change: transform;
  }

  .paused .wash {
    animation-play-state: paused;
  }

  .wash-one {
    top: -30%;
    left: -20%;
    width: 75%;
    height: 90%;
    background: var(--generating-1);
    animation: float-one 9s ease-in-out infinite alternate;
  }

  .wash-two {
    top: -20%;
    right: -25%;
    width: 70%;
    height: 85%;
    background: var(--generating-2);
    animation: float-two 11s ease-in-out infinite alternate;
  }

  .wash-three {
    right: -15%;
    bottom: -35%;
    width: 80%;
    height: 90%;
    background: var(--generating-3);
    animation: float-three 8s ease-in-out infinite alternate;
  }

  .wash-four {
    bottom: -30%;
    left: -20%;
    width: 70%;
    height: 85%;
    background: var(--generating-4);
    animation: float-four 10s ease-in-out infinite alternate;
  }

  /*
   * What it says, over the middle of it.
   *
   * Centred rather than along the bottom edge: the bottom edge is where this
   * field keeps the things you can press, and a sentence there while they are
   * unreachable reads as one of them.
   */
  .caption {
    position: absolute;
    top: 50%;
    right: 0;
    left: 0;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--space-2);
    padding: var(--space-4);
    transform: translateY(-50%);
    pointer-events: none;
  }

  .caption :global(.status) {
    padding: var(--space-3) var(--space-4);
    border: 1px solid var(--generating-panel-border);
    border-radius: var(--radius-lg);
    background: var(--generating-panel);
    box-shadow: var(--generating-panel-shadow);
    backdrop-filter: blur(10px);
  }

  .art {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    pointer-events: auto;
  }
  .art:empty {
    display: none;
  }
  .art :global(.olli) {
    width: clamp(4rem, 33cqw, 9rem);
    height: auto;
  }
  .art :global(.icon-button) {
    color: var(--text-on-media);
    background: var(--generating-panel);
    border-color: var(--generating-panel-border);
  }

  @keyframes float-one {
    to {
      transform: translate3d(30%, 25%, 0) scale(1.2);
    }
  }

  @keyframes float-two {
    to {
      transform: translate3d(-25%, 30%, 0) scale(0.9);
    }
  }

  @keyframes float-three {
    to {
      transform: translate3d(-30%, -20%, 0) scale(1.15);
    }
  }

  @keyframes float-four {
    to {
      transform: translate3d(25%, -25%, 0) scale(1.1);
    }
  }

  /* The wait is still a wait, so the frame still shows the colour and still
     says what it is doing. It simply stops moving. */
  @media (prefers-reduced-motion: reduce) {
    .wash {
      animation: none;
    }
  }
</style>
