<script lang="ts">
  import { m } from '$shell/i18n';
  interface Props {
    remaining: number;
    done: number;
    total: number;
  }
  let { remaining, done, total }: Props = $props();
</script>

<header class="head">
  <h1 class="title">{m['shopping.title']()}</h1>
  <div class="status">
    <p>
      {remaining > 0 ? m['shopping.remaining']({ count: remaining }) : m['shopping.subtitle']()}
    </p>
    {#if total > 0}<p>{m['shopping.progress']({ done, total })}</p>{/if}
  </div>
</header>

<style>
  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-3xl);
    font-weight: var(--weight-regular);
    letter-spacing: -0.03em;
  }
  .status {
    display: flex;
    flex-wrap: wrap;
    justify-content: space-between;
    gap: var(--space-2) var(--space-4);
    margin-top: var(--space-3);
    color: var(--text-muted);
    font-size: var(--text-xs);
    font-variant-numeric: tabular-nums;
  }
</style>
