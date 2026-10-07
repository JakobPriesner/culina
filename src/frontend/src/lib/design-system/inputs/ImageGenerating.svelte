<script lang="ts">
  import type { Snippet } from 'svelte';
  import { MediaQuery } from 'svelte/reactivity';
  import { fade } from 'svelte/transition';

  import GenerationAura from '../feedback/GenerationAura.svelte';
  import GenerationStatus from '../feedback/GenerationStatus.svelte';

  /** The image frame's overlay while a picture is drawn. The fade-out is global because the field decides when it goes. */
  interface Props {
    label?: string | undefined;
    art?: Snippet | undefined;
    animate?: boolean;
  }

  let { label, art, animate = true }: Props = $props();

  const reducedMotion = new MediaQuery('(prefers-reduced-motion: reduce)', false);
</script>

<!-- No progress bar: the provider reports elapsed time, not percentage, and none may be invented. -->
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

  /* Centred, not at the bottom edge, where the field keeps its pressable controls. */
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

  @media (prefers-reduced-motion: reduce) {
    .wash {
      animation: none;
    }
  }
</style>
