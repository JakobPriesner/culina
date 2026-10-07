<script lang="ts">
  import type { Snippet } from 'svelte';

  /** Content being refreshed without taking it away: a refetch of what is on screen keeps showing it (a skeleton loses place and scroll) under a hairline that says work is happening. `aria-busy` announces; the bar is the glance. */
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
    animation: reveal var(--duration-base) var(--ease-out) 150ms both;
  }

  .bar::after {
    content: '';
    position: absolute;
    inset-block: 0;
    inline-size: 40%;
    border-radius: inherit;
    background: var(--accent);
    animation: sweep 1.4s var(--ease-spatial) infinite;
  }

  /* On the content itself so it also eases back when the refresh lands. */
  .content {
    transition: opacity var(--duration-base) var(--ease-out);
  }

  /* Dimmed rather than hidden: still readable, visibly not current. */
  .content.busy {
    opacity: 0.7;
  }

  /* Held back like every loading hint, so a quick refresh shows nothing. */
  @keyframes reveal {
    from {
      opacity: 0;
    }
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
