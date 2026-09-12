<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * Content that is being refreshed, without taking it away.
   *
   * The rule this exists to enforce: a refetch of something already on screen
   * keeps showing it. Replacing a list you are reading with a skeleton loses
   * your place, loses your scroll position, and tells you less than the stale
   * list did — the old data is almost always still the right answer.
   *
   * So the content stays, and a hairline at the top of the region says work is
   * happening. `aria-busy` is the announcement; the bar is the glance.
   */
  interface Props {
    children: Snippet;
    busy: boolean;
    /** Names the region for assistive technology while it is busy. */
    label?: string;
  }

  let { children, busy, label }: Props = $props();
</script>

<div class="region" aria-busy={busy} aria-label={busy ? label : undefined}>
  {#if busy}
    <span class="bar" aria-hidden="true"></span>
  {/if}

  <div class="content" class:busy>{@render children()}</div>
</div>

<style>
  .region {
    position: relative;
  }

  .bar {
    position: absolute;
    inset-block-start: 0;
    inset-inline: 0;
    height: 2px;
    overflow: hidden;
    border-radius: var(--radius-full);
    background: var(--surface-sunken);
  }

  .bar::after {
    content: '';
    position: absolute;
    inset-block: 0;
    inline-size: 40%;
    border-radius: inherit;
    background: var(--accent);
    animation: sweep 1.1s var(--ease-spatial) infinite;
  }

  /* Dimmed rather than hidden: still readable, visibly not current. */
  .content.busy {
    opacity: 0.7;
    transition: opacity var(--duration-base) var(--ease-out);
  }

  @keyframes sweep {
    from {
      transform: translateX(-100%);
    }
    to {
      transform: translateX(250%);
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .bar::after {
      inline-size: 100%;
      animation: none;
      opacity: 0.5;
    }
  }
</style>
