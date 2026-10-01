<script lang="ts">
  /**
   * A quiet, recognisable sign that generated work is arriving.
   *
   * Kept separate from a spinner: a spinner means "nothing to see yet", while
   * Culina's assistant usually has a stream behind it and is already sending
   * useful pieces. A small orb of the assistant's four lights, and the same
   * lights passing through the words, read as "the assistant is thinking"
   * without pretending to know a percentage that the model never reports.
   */
  interface Props {
    label: string;
    tone?: 'default' | 'on-media';
    align?: 'start' | 'center';
  }

  let { label, tone = 'default', align = 'start' }: Props = $props();
</script>

<div
  class="status"
  class:on-media={tone === 'on-media'}
  class:centered={align === 'center'}
  role="status"
  aria-live="polite"
  aria-atomic="true"
>
  <span class="orb" aria-hidden="true"></span>
  <span class="label">{label}</span>
</div>

<style>
  .status {
    /* The words' own colour, named because the label paints itself with a
       gradient and its `currentColor` is transparent. */
    --ink: var(--text-muted);

    display: inline-flex;
    align-items: center;
    gap: var(--space-3);
    max-width: 100%;
    color: var(--ink);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  .centered {
    flex-direction: column;
    gap: var(--space-2);
    justify-content: center;
    text-align: center;
  }

  .on-media {
    --ink: var(--text-on-media);
  }

  /* A drop of the four lights, turning and gently changing shape, with a
     blurred copy of itself behind it as the glow. */
  .orb {
    position: relative;
    flex: 0 0 auto;
    width: 1rem;
    height: 1rem;
    animation:
      turn 4s linear infinite,
      breathe 2.4s ease-in-out infinite;
  }

  .orb::before,
  .orb::after {
    position: absolute;
    inset: 0;
    border-radius: var(--radius-full);
    background: conic-gradient(
      from var(--generating-turn),
      var(--generating-1),
      var(--generating-2),
      var(--generating-3),
      var(--generating-4),
      var(--generating-1)
    );
    content: '';
    animation: morph 6s ease-in-out infinite;
  }

  .orb::before {
    filter: blur(6px);
    opacity: 0.8;
  }

  /* The lights passing through the words, the way a sentence shimmers while
     it is being rewritten. Every point of the sweep stays readable; only the
     tint moves. */
  .label {
    background-image: linear-gradient(
      90deg,
      var(--ink) 0%,
      var(--ink) 36%,
      var(--generating-1) 42%,
      var(--generating-2) 47%,
      var(--generating-3) 53%,
      var(--generating-4) 58%,
      var(--ink) 64%,
      var(--ink) 100%
    );
    background-size: 300% 100%;
    background-clip: text;
    -webkit-background-clip: text;
    color: transparent;
    font-weight: var(--weight-medium);
    animation: glide 2.8s linear infinite;
  }

  @keyframes turn {
    to {
      --generating-turn: 360deg;
    }
  }

  @keyframes breathe {
    0%,
    100% {
      transform: scale(0.88);
    }

    50% {
      transform: scale(1.08);
    }
  }

  @keyframes morph {
    0%,
    100% {
      border-radius: 50%;
    }

    33% {
      border-radius: 58% 42% 52% 48% / 46% 56% 44% 54%;
    }

    66% {
      border-radius: 44% 56% 46% 54% / 56% 44% 58% 42%;
    }
  }

  @keyframes glide {
    from {
      background-position: 100% 0;
    }

    to {
      background-position: 0% 0;
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .orb,
    .orb::before,
    .orb::after,
    .label {
      animation: none;
    }

    .label {
      background: none;
      color: inherit;
    }
  }

  /* Forced colours drop the gradient; the words must not drop with it. */
  @media (forced-colors: active) {
    .label {
      background: none;
      color: inherit;
    }
  }
</style>
