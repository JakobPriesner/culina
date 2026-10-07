<script lang="ts">
  /** A line for when space alone isn't enough: spacing separates, a border only encloses; not for decorating a gap that already works. */
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
