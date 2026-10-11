<script lang="ts">
  import { Button, IconButton } from '$ds';

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

  <!-- Calendar's order: back, today, forward. "Today" only when away from this week, so it never does nothing. -->
  <div class="weeks">
    <IconButton bordered label={m['plan.previous']()} onclick={() => onstep(-1)}>
      <svg
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="2"
        stroke-linecap="round"
        stroke-linejoin="round"
      >
        <path d="m15 5-7 7 7 7" />
      </svg>
    </IconButton>
    {#if offset !== 0}
      <Button size="sm" onclick={onreset}>{m['plan.today']()}</Button>
    {/if}
    <IconButton bordered label={m['plan.next']()} onclick={() => onstep(1)}>
      <svg
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="2"
        stroke-linecap="round"
        stroke-linejoin="round"
      >
        <path d="m9 5 7 7-7 7" />
      </svg>
    </IconButton>
  </div>
</header>

<style>
  .head {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
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
    align-items: center;
    gap: var(--space-2);
  }

  .weeks :global(.button) {
    border-radius: var(--radius-full);
  }
</style>
