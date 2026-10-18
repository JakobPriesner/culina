<script lang="ts">
  import { m } from '$shell/i18n';
  import NavIcon from '$shell/NavIcon.svelte';
  import type { DestinationIcon } from '$shell/navigation';

  import { moving } from './demo';
  import type { React } from './reaction';

  /**
   * Culina's loop as a ring of four stations, a marker going round it on its own until one is chosen.
   * Each station names the tab it lives in, so the tour's picture is the app's map.
   */
  interface Props {
    onreact: React;
  }

  let { onreact }: Props = $props();

  type Station = 'collect' | 'plan' | 'shop' | 'cook';

  const stations: readonly { id: Station; icon: DestinationIcon | 'cook' }[] = [
    { id: 'collect', icon: 'recipes' },
    { id: 'plan', icon: 'plan' },
    { id: 'shop', icon: 'shopping' },
    { id: 'cook', icon: 'cook' }
  ];

  const names: Record<Station, () => string> = {
    collect: m['tour.loop.collect'],
    plan: m['tour.loop.plan.name'],
    shop: m['tour.loop.shop'],
    cook: m['tour.loop.cook']
  };

  const where: Record<Station, () => string> = {
    collect: m['tour.loop.collect.where'],
    plan: m['tour.loop.plan.where'],
    shop: m['tour.loop.shop.where'],
    cook: m['tour.loop.cook.where']
  };

  const what: Record<Station, () => string> = {
    collect: m['tour.loop.collect.what'],
    plan: m['tour.loop.plan.what'],
    shop: m['tour.loop.shop.what'],
    cook: m['tour.loop.cook.what']
  };

  let at = $state(0);
  /** The marker's angle only ever grows: it goes round the way the loop does. */
  let turned = $state(0);
  let chosen = $state(false);

  function go(next: number) {
    turned += ((next - at + stations.length) % stations.length) * 90;
    at = next;
  }

  // Round by itself until someone picks a station.
  $effect(() => {
    if (chosen || !moving()) return;

    const timer = setInterval(() => go((at + 1) % stations.length), 2600);

    return () => clearInterval(timer);
  });

  const station = $derived(stations[at]!);
</script>

<div class="loop">
  <div class="ring">
    <div class="track" style:--turned="{turned}deg" aria-hidden="true">
      <span class="marker"></span>
    </div>

    {#each stations as one, index (one.id)}
      <button
        type="button"
        class="station s{index}"
        aria-pressed={index === at}
        onclick={() => {
          if (!chosen) onreact('idea', m['tour.loop.picked.said']());
          chosen = true;
          go(index);
        }}
      >
        <span class="icon" aria-hidden="true">
          {#if one.icon === 'cook'}
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="1.6"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <path d="M5 11h14v3a6 6 0 0 1-6 6h-2a6 6 0 0 1-6-6Z" />
              <path d="M3.5 11h17M9 8c0-1.5 1-1.5 1-3M14 8c0-1.5 1-1.5 1-3" />
            </svg>
          {:else}
            <NavIcon icon={one.icon} current={index === at} />
          {/if}
        </span>
        <span class="name">{names[one.id]()}</span>
      </button>
    {/each}

    <p class="centre" aria-live="polite">
      <strong>{names[station.id]()}</strong>
      <span>{where[station.id]()}</span>
    </p>
  </div>

  {#key station.id}
    <p class="what">{what[station.id]()}</p>
  {/key}
</div>

<style>
  .loop {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--space-4);
  }

  .ring {
    --size: min(17rem, 72vw);
    position: relative;
    width: var(--size);
    height: var(--size);
  }

  .track {
    position: absolute;
    inset: 12%;
    border: 2px dashed var(--border-strong);
    border-radius: var(--radius-full);
    transform: rotate(var(--turned));
    transition: transform 1s var(--ease-spatial);
  }

  .marker {
    position: absolute;
    top: -0.45rem;
    left: calc(50% - 0.45rem);
    width: 0.9rem;
    height: 0.9rem;
    border: 3px solid var(--surface-overlay);
    border-radius: var(--radius-full);
    background: var(--accent);
  }

  .station {
    position: absolute;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--space-1);
    width: 5.5rem;
    margin: 0;
    padding: var(--space-2) var(--space-1);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
    color: var(--text-muted);
    font: inherit;
    font-size: var(--text-xs);
    font-weight: var(--weight-medium);
    cursor: pointer;
    translate: -50% -50%;
    transition:
      color var(--duration-base) var(--ease-out),
      border-color var(--duration-base) var(--ease-out),
      scale var(--duration-base) var(--ease-out);
  }

  .station:focus-visible {
    outline: 2px solid var(--border-focus);
    outline-offset: 2px;
  }

  .station[aria-pressed='true'] {
    border-color: var(--border-accent);
    background: var(--surface-accent-subtle);
    color: var(--accent);
    scale: 1.08;
  }

  .s0 {
    top: 12%;
    left: 50%;
  }

  .s1 {
    top: 50%;
    left: 88%;
  }

  .s2 {
    top: 88%;
    left: 50%;
  }

  .s3 {
    top: 50%;
    left: 12%;
  }

  .icon {
    width: var(--space-6);
    height: var(--space-6);
  }

  .centre {
    position: absolute;
    inset: 32%;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    margin: 0;
    text-align: center;
    font-size: var(--text-xs);
    color: var(--text-muted);
  }

  .centre strong {
    color: var(--text);
    font-family: var(--font-editorial);
    font-size: var(--text-lg);
    font-weight: var(--weight-medium);
  }

  .what {
    max-width: 26rem;
    min-height: 3lh;
    margin: 0;
    text-align: center;
    line-height: var(--leading-normal);
    animation: appear var(--duration-slow) var(--ease-out);
  }

  @keyframes appear {
    from {
      opacity: 0;
      transform: translateY(var(--space-1));
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .track,
    .station {
      transition: none;
    }

    .what {
      animation: none;
    }
  }
</style>
