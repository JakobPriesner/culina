<script lang="ts">
  /** A restrained static colour edge; the colours stay still so large surfaces do not repaint a moving gradient. */
  interface Props {
    active?: boolean;
    /** Over the content instead, for a frame whose content is itself the backdrop. */
    over?: boolean;
  }

  let { active = true, over = false }: Props = $props();
</script>

<span class="aura" class:active class:over aria-hidden="true">
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
    /* Hidden once faded, so a finished card stops repainting an invisible glow. */
    visibility: hidden;
    opacity: 0;
    transition:
      opacity var(--duration-enter) var(--ease-out),
      visibility 0s var(--duration-enter);
  }

  .active {
    visibility: visible;
    opacity: 1;
    transition: opacity var(--duration-enter) var(--ease-out);
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

  /* Blurred copies of the ring rather than a box-shadow, which takes one colour; the glow needs all four where their light is. */
  .soft {
    filter: blur(6px);
    opacity: 0.3;
  }

  .ring {
    padding: 2px;
    background: linear-gradient(
      135deg,
      var(--generating-1),
      var(--generating-2),
      var(--generating-3),
      var(--generating-4)
    );
    /* Only the padding survives, leaving the gradient as a line that follows the container's corners. */
    mask:
      linear-gradient(currentColor 0 0) content-box,
      linear-gradient(currentColor 0 0);
    mask-composite: exclude;
  }

  .soft .ring {
    padding: 4px;
  }

  @media (prefers-reduced-motion: reduce) {
    .aura,
    .soft {
      transition: none;
      animation: none;
    }
  }
</style>
