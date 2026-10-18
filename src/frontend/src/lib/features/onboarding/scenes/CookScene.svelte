<script lang="ts">
  import { readAloud } from '$features/cooking/readAloud.svelte';
  import { getLocale } from '$lib/paraglide/runtime';
  import { m } from '$shell/i18n';

  import { moving } from './demo';
  import type { React } from './reaction';

  /**
   * Cook mode in miniature: one big step at a time, its timer (run fast here), the step read aloud by
   * the device's own voice, and "I made it" at the end.
   */
  interface Props {
    onreact: React;
  }

  let { onreact }: Props = $props();

  const steps = [
    {
      words: () =>
        [
          m['tour.steps.demo.before'](),
          `2 ${m['tour.steps.demo.onion']({ count: 2 })}`,
          m['tour.steps.demo.middle'](),
          `20 g ${m['tour.steps.demo.butter.name']()}`,
          m['tour.steps.demo.beforeTime'](),
          m['tour.steps.demo.time'](),
          m['tour.steps.demo.after']()
        ].join(''),
      minutes: 5
    },
    { words: m['tour.cook.step.two'], minutes: 10 },
    { words: m['tour.cook.step.three'], minutes: 6 }
  ];

  /** A real minute takes this long here. */
  const minuteMs = 700;

  let at = $state(0);
  let left = $state<number | null>(null);
  let done = $state(false);
  let timer: ReturnType<typeof setInterval> | undefined;

  const step = $derived(steps[at]!);
  const last = $derived(at === steps.length - 1);

  function stopTimer() {
    clearInterval(timer);
    timer = undefined;
  }

  function startTimer() {
    stopTimer();

    const total = step.minutes * 60;
    const rang = () => {
      stopTimer();
      left = 0;
      onreact('celebrating', m['tour.cook.rang.said']());
    };

    if (!moving()) {
      rang();
      return;
    }

    const started = performance.now();
    left = total;
    timer = setInterval(() => {
      const passed = ((performance.now() - started) / minuteMs) * 60;
      if (passed >= total) rang();
      else left = Math.ceil(total - passed);
    }, 50);
  }

  function next() {
    stopTimer();
    left = null;
    readAloud.stop();

    if (last) {
      done = true;
      onreact('celebrating', m['tour.cook.done.said']());
      return;
    }

    at += 1;
  }

  function speak() {
    readAloud.speak(step.words(), getLocale());
    onreact('watching', m['tour.cook.read.said']());
  }

  $effect(() => () => {
    stopTimer();
    readAloud.stop();
  });

  const clock = (seconds: number) =>
    `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;

  const progress = $derived(left === null ? 0 : 1 - left / (step.minutes * 60));
</script>

<div class="cook">
  <div class="phone">
    {#if done}
      <div class="made">
        <span class="check" aria-hidden="true">✓</span>
        <p class="made-title">{m['tour.cook.made']()}</p>
        <p class="made-what">{m['tour.cook.made.what']()}</p>
      </div>
    {:else}
      <p class="count">{m['tour.cook.stepOf']({ current: at + 1, total: steps.length })}</p>
      {#key at}<p class="words">{step.words()}</p>{/key}

      <div class="tools">
        <button
          type="button"
          class="timer"
          class:running={left !== null && left > 0}
          class:rang={left === 0}
          style:--progress={progress}
          onclick={startTimer}
        >
          <span class="ring" aria-hidden="true"></span>
          {left === null
            ? m['tour.cook.timer']({ minutes: step.minutes })
            : left === 0
              ? m['tour.cook.timer.rang']()
              : clock(left)}
        </button>

        {#if readAloud.supported}
          <button type="button" class="speak" onclick={speak}>
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <path d="M4 10v4h4l5 4V6L8 10Z" />
              <path d="M16.5 9a4 4 0 0 1 0 6M19 6.5a7.5 7.5 0 0 1 0 11" />
            </svg>
            {m['tour.cook.read']()}
          </button>
        {/if}
      </div>

      <button type="button" class="next" onclick={next}>
        {last ? m['cooking.madeIt']() : m['tour.cook.next']()}
      </button>
    {/if}
  </div>

  <p class="caption">{m['tour.cook.what']()}</p>
</div>

<style>
  .cook {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--space-4);
  }

  .phone {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    width: min(20rem, 100%);
    min-height: 17rem;
    padding: var(--space-4);
    border: 6px solid var(--border-strong);
    border-radius: var(--radius-xl);
    background: var(--surface);
  }

  .count {
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-xs);
    font-weight: var(--weight-medium);
  }

  .words {
    flex: 1;
    margin: 0;
    font-size: var(--text-lg);
    line-height: var(--leading-normal);
    animation: slide var(--duration-slow) var(--ease-spatial);
  }

  .tools {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  .timer,
  .speak {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
    min-height: var(--control-sm);
    padding: 0 var(--space-3);
    border: 1px solid var(--border-strong);
    border-radius: var(--radius-full);
    background: var(--surface-raised);
    color: var(--text);
    font: inherit;
    font-size: var(--text-sm);
    font-variant-numeric: tabular-nums;
    cursor: pointer;
  }

  .timer:focus-visible,
  .speak:focus-visible,
  .next:focus-visible {
    outline: 2px solid var(--border-focus);
    outline-offset: 2px;
  }

  .speak svg {
    width: 1.1rem;
    height: 1.1rem;
  }

  .ring {
    width: 1rem;
    height: 1rem;
    border-radius: var(--radius-full);
    background: conic-gradient(var(--accent) calc(var(--progress) * 360deg), var(--border) 0);
  }

  .timer.running {
    border-color: var(--border-accent);
  }

  .timer.rang {
    border-color: var(--accent);
    background: var(--accent);
    color: var(--accent-contrast);
    animation: ring 0.5s var(--ease-out) 2;
  }

  .timer.rang .ring {
    background: var(--accent-contrast);
  }

  .next {
    min-height: var(--control-md);
    border: none;
    border-radius: var(--radius-full);
    background: var(--accent);
    color: var(--accent-contrast);
    font: inherit;
    font-weight: var(--weight-semibold);
    cursor: pointer;
  }

  .made {
    display: flex;
    flex: 1;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: var(--space-2);
    text-align: center;
    animation: slide var(--duration-slow) var(--ease-spatial);
  }

  .check {
    display: grid;
    place-items: center;
    width: 3rem;
    height: 3rem;
    border-radius: var(--radius-full);
    background: var(--success-subtle);
    color: var(--text-success);
    font-size: var(--text-xl);
  }

  .made-title {
    margin: 0;
    font-family: var(--font-editorial);
    font-size: var(--text-lg);
  }

  .made-what {
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .caption {
    max-width: 26rem;
    margin: 0;
    color: var(--text-muted);
    text-align: center;
    line-height: var(--leading-normal);
  }

  @keyframes slide {
    from {
      opacity: 0;
      transform: translateX(var(--space-4));
    }
  }

  @keyframes ring {
    25% {
      rotate: -6deg;
    }

    75% {
      rotate: 6deg;
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .words,
    .made,
    .timer.rang {
      animation: none;
    }
  }
</style>
