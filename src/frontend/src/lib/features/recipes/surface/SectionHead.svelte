<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * The heading of one of the recipe's two regions, with room beside it for a
   * control.
   *
   * One component for both so that they line up across the columns whether or
   * not either has a control: the same wrapper, the same height. The control
   * sits on the heading's own line, and stays put when the arrangement changes,
   * because the head keeps the width of the ingredient column even where its
   * section has grown past it.
   */
  interface Props {
    title: string;
    children?: Snippet;
  }

  let { title, children }: Props = $props();
</script>

<div class="section-head">
  <h2 class="section">{title}</h2>

  {@render children?.()}
</div>

<style>
  .section-head {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2);
    /* Capped, not stretched: the switch then sits in the same place whether
       the section is the column or the whole width of the page. */
    max-width: var(--side-column);
    /*
     * One height for both headings, whether or not a switch is sitting beside
     * this one. Without it the control makes its own row taller and its
     * heading rides down the middle of it, half a line below the heading in
     * the next column — the kind of misalignment that is invisible in a
     * component and obvious on the page.
     */
    min-height: var(--control-lg);
    margin-bottom: var(--space-3);
  }

  .section {
    font-family: var(--font-editorial);
    font-size: var(--text-2xl);
    font-weight: var(--weight-regular);
    letter-spacing: -0.025em;
    color: var(--text);
    margin-bottom: 0;
  }

  @media screen and (width < 64rem) {
    .section-head {
      max-width: none;
    }
  }

  @media print {
    .section-head {
      min-height: 0;
    }
  }
</style>
