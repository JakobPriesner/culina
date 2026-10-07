<script lang="ts">
  /**
   * One placeholder block: a skeleton shows the shape of what is coming so the layout doesn't jump.
   * Every feature skeleton is built from this, so shimmers stay in sync.
   */
  interface Props {
    /** Any CSS length. Defaults to filling the row. */
    width?: string;
    height?: string;
    /** Matches the shape it stands in for: text, a thumbnail, an avatar. */
    shape?: 'text' | 'block' | 'circle';
  }

  let { width = '100%', height, shape = 'text' }: Props = $props();

  const defaultHeight = $derived(shape === 'text' ? '1em' : '100%');
</script>

<!-- Hidden from assistive technology: the container carries aria-busy, the one announcement worth making. -->
<span class="skeleton {shape}" aria-hidden="true" style:width style:height={height ?? defaultHeight}
></span>

<style>
  /* Fades in: it only appears after a wait outlasts the loading delay, and snapping in reads as a glitch. */
  .skeleton {
    position: relative;
    overflow: hidden;
    display: block;
    background-color: var(--skeleton-base);
    animation: appear var(--duration-slow) var(--ease-out) both;
  }

  /* One slanted band then a rest; a single unhurried pass reads calm (the old pulse against a
     different-period shimmer flickered). Every block mounts in one frame, so they sweep together. */
  .skeleton::after {
    position: absolute;
    inset: 0;
    width: 200%;
    background-image: linear-gradient(
      105deg,
      transparent 30%,
      var(--skeleton-highlight) 50%,
      transparent 70%
    );
    transform: translateX(-70%);
    animation: sweep 2.4s var(--ease-out) infinite;
    content: '';
    pointer-events: none;
  }

  .text {
    border-radius: var(--radius-sm);
  }

  .block {
    border-radius: var(--radius-lg);
  }

  .circle {
    border-radius: var(--radius-full);
    aspect-ratio: 1;
  }

  @keyframes appear {
    from {
      opacity: 0;
    }
  }

  @keyframes sweep {
    0% {
      transform: translateX(-70%);
    }

    70%,
    100% {
      transform: translateX(50%);
    }
  }

  /* A static tint: a flat block says "coming" too. */
  @media (prefers-reduced-motion: reduce) {
    .skeleton {
      animation: none;
    }

    .skeleton::after {
      display: none;
    }
  }
</style>
