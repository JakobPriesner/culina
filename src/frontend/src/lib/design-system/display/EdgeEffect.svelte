<script lang="ts">
  /**
   * Frosts the page under a floating bar like iOS's scroll edge effect: tinted and blurred at the bar's edge of the
   * screen, easing into the sharp page with no line, so a photo runs on under the bar and the bar's controls stay
   * legible. One blur faded out shows a blurred copy over sharp text halfway; stacked blurs, each weaker and reaching
   * further, compound into a smooth ramp instead. The tint eases out too, so a dark card gets no pale band.
   *
   * The parent positions itself (sticky or relative); this fills it and reaches `--space-6` past its inner edge.
   * With Reduce Transparency or Increase Contrast it is a solid band with a hairline instead.
   */
  let { edge }: { edge: 'top' | 'bottom' } = $props();
</script>

<div class="edge {edge}" aria-hidden="true"><i></i><i></i><i></i><i></i></div>

<style>
  .edge {
    --toward: to bottom;

    position: absolute;
    inset: 0 0 calc(-1 * var(--space-6));
    pointer-events: none;
  }

  /* The tint, fading out over the same reach as the blurs. */
  .edge::before {
    position: absolute;
    inset: 0;
    background: var(--surface-bar-glass);
    mask-image: linear-gradient(var(--toward), black, transparent);
    content: '';
  }

  .bottom {
    --toward: to top;

    inset: calc(-1 * var(--space-6)) 0 0;
  }

  i {
    position: absolute;
    inset: 0;
  }

  i:nth-child(1) {
    backdrop-filter: blur(1px);
    -webkit-backdrop-filter: blur(1px);
    mask-image: linear-gradient(var(--toward), black 60%, transparent 100%);
  }

  i:nth-child(2) {
    backdrop-filter: blur(3px);
    -webkit-backdrop-filter: blur(3px);
    mask-image: linear-gradient(var(--toward), black 40%, transparent 80%);
  }

  i:nth-child(3) {
    backdrop-filter: blur(6px);
    -webkit-backdrop-filter: blur(6px);
    mask-image: linear-gradient(var(--toward), black 20%, transparent 60%);
  }

  i:nth-child(4) {
    backdrop-filter: blur(12px) saturate(180%);
    -webkit-backdrop-filter: blur(12px) saturate(180%);
    mask-image: linear-gradient(var(--toward), black 0%, transparent 40%);
  }

  /* Solid, as iOS draws its bars when transparency is turned down: no see-through ramp, a hairline where it ends. */
  :global(html[data-transparency='reduced']) .edge,
  :global(html[data-contrast='more']) .edge {
    inset: 0;
    background: var(--surface);
    border-block-end: 1px solid var(--border);
  }

  :global(html[data-transparency='reduced']) .bottom,
  :global(html[data-contrast='more']) .bottom {
    border-block-end: 0;
    border-block-start: 1px solid var(--border);
  }

  :global(html[data-transparency='reduced']) .edge::before,
  :global(html[data-contrast='more']) .edge::before,
  :global(html[data-transparency='reduced']) i,
  :global(html[data-contrast='more']) i {
    display: none;
  }
</style>
