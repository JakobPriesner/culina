<script lang="ts">
  /**
   * Light flowing round the edge of something the assistant is generating.
   *
   * The language of Apple Intelligence: four colours turning slowly round a
   * frame, a crisp line on the edge and a soft glow either side of it. It says
   * "the assistant is working on this" about a place on the page, which a
   * status line beside it cannot do — and it needs no progress it does not
   * have.
   *
   * Drop it into a container that is `position: relative`; it takes the
   * container's corner radius. By default it sits behind the container's
   * content, so the glow tints the ground under the words and never the words
   * themselves — the container needs `isolation: isolate` for that.
   *
   * It fades in and out with `active` in CSS rather than with a Svelte
   * transition, so it can stay mounted beside content that should disappear
   * the moment the work ends — an outro would hold its whole block on screen
   * for as long as the fade lasts.
   */
  interface Props {
    /** Whether the work is running. */
    active?: boolean;
    /** Over the content instead, for a frame whose content is itself the backdrop. */
    over?: boolean;
  }

  let { active = true, over = false }: Props = $props();
</script>

<span class="aura" class:active class:over aria-hidden="true">
  <span class="layer wide"><span class="ring"></span></span>
  <span class="layer soft"><span class="ring"></span></span>
  <span class="layer"><span class="ring"></span></span>
</span>

<style>
  .aura {
    position: absolute;
    z-index: -1;
    inset: -1px;
    border-radius: inherit;
    pointer-events: none;
    /* Hidden once faded, so a finished card stops repainting a glow that
       nobody can see. */
    visibility: hidden;
    opacity: 0;
    transition:
      opacity 480ms var(--ease-out),
      visibility 0s 480ms;
    animation: turn 5s linear infinite;
  }

  .active {
    visibility: visible;
    opacity: 1;
    transition: opacity 480ms var(--ease-out);
  }

  @starting-style {
    .active {
      opacity: 0;
    }
  }

  .over {
    z-index: auto;
  }

  .layer,
  .ring {
    position: absolute;
    inset: 0;
    border-radius: inherit;
  }

  /* Blurred copies of the same ring rather than a box-shadow: a shadow takes
     one colour, and the point is that the glow has all four, each where its
     own light is on the edge. */
  .soft {
    filter: blur(6px);
    opacity: 0.85;
  }

  .wide {
    filter: blur(22px);
    opacity: 0.6;
    animation: breathe 3.2s ease-in-out infinite;
  }

  .ring {
    padding: 2px;
    background: conic-gradient(
      from var(--generating-turn),
      var(--generating-1),
      var(--generating-2),
      var(--generating-3),
      var(--generating-4),
      var(--generating-1)
    );
    /* Everything but the padding is cut away, which leaves the gradient as a
       line that follows the container's own corners. */
    mask:
      linear-gradient(currentColor 0 0) content-box,
      linear-gradient(currentColor 0 0);
    mask-composite: exclude;
  }

  .soft .ring {
    padding: 4px;
  }

  .wide .ring {
    padding: 8px;
  }

  @keyframes turn {
    to {
      --generating-turn: 360deg;
    }
  }

  @keyframes breathe {
    0%,
    100% {
      opacity: 0.45;
    }

    50% {
      opacity: 0.75;
    }
  }

  /* Still lit, no longer moving: the edge keeps saying where the work is. */
  @media (prefers-reduced-motion: reduce) {
    .aura,
    .wide {
      transition: none;
      animation: none;
    }
  }
</style>
