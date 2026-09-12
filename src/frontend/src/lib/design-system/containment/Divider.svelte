<script lang="ts">
  /**
   * A line, for the rare case where space alone is not enough.
   *
   * Spacing separates; a border only encloses. Reach for this when two things
   * are genuinely adjacent and unrelated — not to decorate a gap that is
   * already doing its job.
   */
  interface Props {
    orientation?: 'horizontal' | 'vertical';
  }

  let { orientation = 'horizontal' }: Props = $props();
</script>

<hr class={orientation} aria-orientation={orientation} />

<style>
  hr {
    margin: 0;
    border: none;
    background: var(--border);
  }

  .horizontal {
    block-size: 1px;
    inline-size: 100%;
  }

  .vertical {
    inline-size: 1px;
    block-size: 100%;
  }
</style>
