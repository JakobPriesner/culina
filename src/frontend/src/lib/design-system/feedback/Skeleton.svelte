<script lang="ts">
  /**
   * One placeholder block.
   *
   * A skeleton rather than a spinner: it shows the shape of what is coming, so
   * the layout does not jump when the content lands and the wait reads as
   * "nearly there" rather than "something is happening somewhere".
   *
   * Every feature skeleton is built from this. A bespoke one is how two screens
   * end up shimmering at different speeds.
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

<!--
  Hidden from assistive technology: the container that holds these carries
  aria-busy, which is the one announcement worth making. A screen reader
  listing twelve empty boxes is worse than silence.
-->
<span class="skeleton {shape}" aria-hidden="true" style:width style:height={height ?? defaultHeight}
></span>

<style>
  .skeleton {
    position: relative;
    overflow: hidden;
    display: block;
    background-color: var(--skeleton-base);
    animation: pulse 2s cubic-bezier(0.4, 0, 0.6, 1) infinite;
  }

  .skeleton::after {
    position: absolute;
    inset: 0;
    transform: translateX(-100%);
    background-image: linear-gradient(
      90deg,
      transparent 0%,
      var(--skeleton-highlight) 50%,
      transparent 100%
    );
    opacity: 0.6;
    animation: shimmer 1.8s cubic-bezier(0.4, 0, 0.2, 1) infinite;
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

  @keyframes shimmer {
    100% {
      transform: translateX(100%);
    }
  }

  @keyframes pulse {
    0%,
    100% {
      opacity: 1;
    }
    50% {
      opacity: 0.7;
    }
  }

  /* A static tint rather than a slower shimmer: the point of the animation is
     "this is coming", and a flat block says that too. */
  @media (prefers-reduced-motion: reduce) {
    .skeleton {
      animation: none;
    }

    .skeleton::after {
      display: none;
    }
  }
</style>
