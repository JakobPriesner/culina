<script lang="ts">
  import { m } from '$shell/i18n';
  /** What is left says how the shop is going; done-of-total is the progress bar's to show, not a second line of numbers. */
  interface Props {
    remaining: number;
    /** `h2` where the list is the pane beside the week: named as the place's second half, never above the page's own heading. */
    heading?: 'h1' | 'h2';
  }
  let { remaining, heading = 'h1' }: Props = $props();
</script>

<header class="head">
  <svelte:element this={heading} class="title"
    >{heading === 'h2' ? m['nav.shopping']() : m['shopping.title']()}</svelte:element
  >
  <div class="status">
    <p>
      {remaining > 0 ? m['shopping.remaining']({ count: remaining }) : m['shopping.subtitle']()}
    </p>
  </div>
</header>

<style>
  /* The week's heading size: plan and list are two views of one place, and beside each other they share a baseline. */
  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-2xl);
    font-weight: var(--weight-regular);
  }
  .status {
    display: flex;
    flex-wrap: wrap;
    justify-content: space-between;
    gap: var(--space-2) var(--space-4);
    color: var(--text-muted);
    font-variant-numeric: tabular-nums;
  }
</style>
