<script lang="ts">
  import { Button } from '$ds';

  import { m } from '$shell/i18n';

  interface Props {
    /** The week on screen, worded. */
    range: string;
    /** How many weeks from this one; zero hides the way back. */
    offset: number;
    onstep: (weeks: number) => void;
    onreset: () => void;
  }

  let { range, offset, onstep, onreset }: Props = $props();
</script>

<header class="head">
  <div class="heading">
    <h1 class="title">{m['plan.title']()}</h1>
    <p class="range">{range}</p>
  </div>

  <div class="weeks">
    <Button size="sm" onclick={() => onstep(-1)}>← {m['plan.previous']()}</Button>
    {#if offset !== 0}
      <Button size="sm" onclick={onreset}>{m['plan.thisWeek']()}</Button>
    {/if}
    <Button size="sm" onclick={() => onstep(1)}>{m['plan.next']()} →</Button>
  </div>
</header>

<style>
  .head {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    justify-content: space-between;
    gap: var(--space-4);
    margin-bottom: var(--space-6);
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-2xl);
    font-weight: var(--weight-regular);
  }

  .range {
    color: var(--text-muted);
  }

  .weeks {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }
</style>
