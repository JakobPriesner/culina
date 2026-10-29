<script lang="ts">
  import { formatDate, m } from '$shell/i18n';
  import NavIcon from '$shell/NavIcon.svelte';

  import { moving, play, type Beat } from './demo';
  import type { React } from './reaction';

  /**
   * A recipe put on a day, and the list writing itself from it: tap a day (or wait, and it is done for
   * you), then tick lines off as in a shop.
   */
  interface Props {
    onreact: React;
  }

  let { onreact }: Props = $props();

  const today = new Date();
  const sinceMonday = (today.getDay() + 6) % 7;

  const days = Array.from({ length: 7 }, (_, i) => {
    const date = new Date(today.getFullYear(), today.getMonth(), today.getDate() - sinceMonday + i);

    return {
      short: formatDate(date, { weekday: 'short' }),
      long: formatDate(date, { weekday: 'long' })
    };
  });

  const items = [
    m['tour.sample.eggs'],
    m['tour.sample.tomatoes'],
    m['tour.sample.pepper'],
    m['tour.sample.onion'],
    m['tour.sample.feta']
  ];

  let day = $state<number | null>(null);
  let listed = $state(0);
  let ticked = $state<readonly number[]>([]);
  let stop = () => {};

  function place(index: number) {
    if (day !== null) return;

    day = index;

    const beats: Beat[] = items.map((_, i) => [350 + i * 200, () => (listed = i + 1)]);
    beats.push([
      350 + items.length * 200 + 200,
      () => onreact('celebrating', m['tour.plan.listed.said']())
    ]);
    stop = play(beats);
  }

  function tick(index: number) {
    const first = ticked.length === 0;

    ticked = ticked.includes(index) ? ticked.filter((one) => one !== index) : [...ticked, index];

    if (first) onreact('watching', m['tour.plan.ticked.said']());
  }

  // Nobody tapping: it plans Wednesday itself, so the point is made either way. A still screen waits for a tap.
  $effect(() => {
    if (!moving()) return () => stop();

    const timer = setTimeout(() => place(2), 3800);

    return () => {
      clearTimeout(timer);
      stop();
    };
  });
</script>

<div class="plan">
  <div class="recipe" class:placed={day !== null}>
    <span class="title">{m['tour.sample.title']()}</span>
    <span class="meta"
      >{day === null
        ? m['tour.plan.where']()
        : m['tour.plan.placed']({ day: days[day]!.long })}</span
    >
  </div>

  <div class="week" role="group" aria-label={m['tour.plan.week']()}>
    {#each days as one, index (index)}
      <button
        type="button"
        class="day"
        class:chosen={day === index}
        aria-label={`${one.short}, ${one.long}`}
        aria-pressed={day === index}
        disabled={day !== null && day !== index}
        onclick={() => place(index)}
      >
        <span class="weekday">{one.short}</span>
        {#if day === index}<span class="meal" aria-hidden="true"></span>{/if}
      </button>
    {/each}
  </div>

  <section class="list" aria-label={m['nav.shopping']()}>
    <header class="head">
      <span class="icon" aria-hidden="true"><NavIcon icon="shopping" current={false} /></span>
      {m['nav.shopping']()}
      {#if listed > 0}<span class="count">{listed - ticked.length}</span>{/if}
    </header>

    {#if listed === 0}
      <p class="empty">{m['tour.plan.empty']()}</p>
    {:else}
      <ul class="lines">
        {#each items.slice(0, listed) as item, index (index)}
          <li>
            <button
              type="button"
              class="line"
              aria-pressed={ticked.includes(index)}
              onclick={() => tick(index)}
            >
              <span class="box" aria-hidden="true"></span>
              {item()}
            </button>
          </li>
        {/each}
      </ul>
    {/if}
  </section>
</div>

<style>
  .plan {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--space-3);
    width: min(26rem, 100%);
    margin-inline: auto;
  }

  .recipe {
    display: flex;
    flex-direction: column;
    align-items: center;
    padding: var(--space-2) var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-md);
    background: var(--surface-raised);
    box-shadow: var(--shadow-card);
    transition:
      opacity var(--duration-slow) var(--ease-out),
      transform var(--duration-slow) var(--ease-spatial);
  }

  .recipe.placed {
    border-color: var(--border-accent);
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-lg);
  }

  .meta {
    color: var(--text-muted);
    font-size: var(--text-xs);
  }

  .week {
    display: grid;
    grid-template-columns: repeat(7, minmax(0, 1fr));
    gap: var(--space-1);
    width: 100%;
  }

  .day {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--space-1);
    min-height: 4rem;
    padding: var(--space-2) 0;
    border: 1px dashed var(--border-strong);
    border-radius: var(--radius-md);
    background: transparent;
    color: var(--text-muted);
    font: inherit;
    font-size: var(--text-xs);
    cursor: pointer;
    transition:
      border-color var(--duration-base) var(--ease-out),
      background-color var(--duration-base) var(--ease-out);
  }

  .day:not(:disabled):hover {
    border-color: var(--border-accent);
    background: var(--surface-hover);
  }

  .day:disabled {
    cursor: default;
    opacity: 0.6;
  }

  .day:focus-visible {
    outline: 2px solid var(--border-focus);
    outline-offset: 2px;
  }

  .day.chosen {
    border-style: solid;
    border-color: var(--border-accent);
    background: var(--surface-accent-subtle);
    color: var(--accent);
    font-weight: var(--weight-semibold);
  }

  .meal {
    width: 70%;
    height: 1.4rem;
    border-radius: var(--radius-sm);
    background: var(--accent);
    animation: land var(--duration-slow) var(--ease-spatial);
  }

  .list {
    width: 100%;
    min-height: 12rem;
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-md);
    background: var(--surface-sunken);
  }

  .head {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    margin-bottom: var(--space-2);
    font-weight: var(--weight-semibold);
  }

  .icon {
    width: 1.25rem;
    height: 1.25rem;
    color: var(--accent);
  }

  .count {
    min-width: 1.4rem;
    padding: 0 var(--space-1);
    border-radius: var(--radius-full);
    background: var(--accent);
    color: var(--accent-contrast);
    font-size: var(--text-xs);
    text-align: center;
  }

  .empty {
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .lines {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .lines li {
    animation: land var(--duration-base) var(--ease-spatial);
  }

  .line {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    width: 100%;
    padding: var(--space-1) 0;
    border: none;
    background: none;
    color: var(--text);
    font: inherit;
    font-size: var(--text-sm);
    text-align: start;
    cursor: pointer;
  }

  .line:focus-visible {
    outline: 2px solid var(--border-focus);
    outline-offset: 2px;
  }

  .box {
    flex: none;
    width: 1.1rem;
    height: 1.1rem;
    border: 1.5px solid var(--border-strong);
    border-radius: var(--radius-full);
    transition: background-color var(--duration-fast) var(--ease-out);
  }

  .line[aria-pressed='true'] {
    color: var(--text-muted);
    text-decoration: line-through;
  }

  .line[aria-pressed='true'] .box {
    border-color: var(--accent);
    background: var(--accent);
  }

  @keyframes land {
    from {
      opacity: 0;
      transform: translateY(calc(var(--space-2) * -1)) scale(0.9);
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .meal,
    .lines li {
      animation: none;
    }

    .recipe {
      transition: none;
    }
  }
</style>
