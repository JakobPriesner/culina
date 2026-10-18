<script lang="ts">
  import { resolve } from '$app/paths';
  import { Button, Sheet } from '$ds';
  import { m } from '$shell/i18n';
  import NavIcon from '$shell/NavIcon.svelte';
  import type { DestinationIcon } from '$shell/navigation';
  import Olli from '$shell/olli/Olli.svelte';
  import type { Pose } from '$shell/olli/poses';

  /**
   * Five cards on how Culina's loop fits together (collect, plan, shop, cook), about a minute, skippable on
   * every card. Each has Olli and a small picture of the real thing, so the words can be few; the step card
   * carries the writing rules the editor's tip repeats. See docs/onboarding-design.md.
   */
  interface Props {
    open: boolean;
    /** Closed, skipped or finished: the tour is not offered again. */
    onclose: () => void;
  }

  let { open, onclose }: Props = $props();

  type Card = 'loop' | 'capture' | 'steps' | 'plan' | 'cook';

  const cards: readonly { id: Card; pose: Pose }[] = [
    { id: 'loop', pose: 'hello' },
    { id: 'capture', pose: 'reading' },
    { id: 'steps', pose: 'writing' },
    { id: 'plan', pose: 'thinking' },
    { id: 'cook', pose: 'celebrating' }
  ];

  let index = $state(0);

  // Every opening starts at the beginning.
  $effect(() => {
    if (open) index = 0;
  });

  const card = $derived(cards[index]!);
  const last = $derived(index === cards.length - 1);

  const titles: Record<Card, () => string> = {
    loop: m['tour.loop.title'],
    capture: m['tour.capture.title'],
    steps: m['tour.steps.title'],
    plan: m['tour.plan.title'],
    cook: m['tour.cook.title']
  };

  const bodies: Record<Card, () => string> = {
    loop: m['tour.loop.body'],
    capture: m['tour.capture.body'],
    steps: m['tour.steps.body'],
    plan: m['tour.plan.body'],
    cook: m['tour.cook.body']
  };

  const points: Record<Exclude<Card, 'loop'>, readonly (() => string)[]> = {
    capture: [m['tour.capture.paste'], m['tour.capture.photo'], m['tour.capture.title.only']],
    steps: [
      m['tour.steps.one'],
      m['tour.steps.length'],
      m['tour.steps.mention'],
      m['tour.steps.timer']
    ],
    plan: [m['tour.plan.days'], m['tour.plan.list'], m['tour.plan.leftovers']],
    cook: [m['tour.cook.steps'], m['tour.cook.timers'], m['tour.cook.remember']]
  };

  const tabs: readonly { icon: DestinationIcon; label: () => string; what: () => string }[] = [
    { icon: 'recipes', label: m['nav.recipes'], what: m['tour.loop.recipes'] },
    { icon: 'cookbooks', label: m['library.cookbooks'], what: m['tour.loop.cookbooks'] },
    { icon: 'plan', label: m['nav.plan'], what: m['tour.loop.plan'] },
    { icon: 'shopping', label: m['nav.shopping'], what: m['tour.loop.shopping'] }
  ];

  function onkeydown(event: KeyboardEvent) {
    if (!open) return;

    if (event.key === 'ArrowRight' && !last) index += 1;
    if (event.key === 'ArrowLeft' && index > 0) index -= 1;
  }
</script>

<svelte:window {onkeydown} />

<Sheet {open} title={titles[card.id]()} closeLabel={m['tour.skip']()} {onclose}>
  <div class="card">
    <div class="art" aria-hidden="true">
      {#key card.id}<Olli pose={card.pose} size="md" />{/key}
    </div>

    <p class="body">{bodies[card.id]()}</p>

    {#if card.id === 'loop'}
      <ul class="tabs">
        {#each tabs as tab (tab.icon)}
          <li>
            <span class="icon" aria-hidden="true"><NavIcon icon={tab.icon} current={false} /></span>
            <span class="tab"><strong>{tab.label()}</strong> {tab.what()}</span>
          </li>
        {/each}
      </ul>
    {:else}
      {#if card.id === 'steps'}
        <!-- The rule shown working: written with @, cooked with the amounts filled in and a timer offered. -->
        <figure class="example" aria-label={m['tour.steps.example.label']()}>
          <p class="written">{m['tour.steps.example.written']()}</p>
          <p class="cooked">
            {m['tour.steps.example.before']()}
            <span class="pill">{m['tour.steps.example.onion']()}</span>
            {m['tour.steps.example.middle']()}
            <span class="pill">{m['tour.steps.example.butter']()}</span>
            {m['tour.steps.example.after']()}
            <span class="timer">⏱ {m['tour.steps.example.timer']()}</span>
          </p>
        </figure>
      {/if}

      <ul class="points">
        {#each points[card.id] as point (point)}
          <li>{point()}</li>
        {/each}
      </ul>
    {/if}
  </div>

  {#snippet footer()}
    <p class="progress" aria-live="polite">
      <span class="dots" aria-hidden="true">
        {#each cards as one, at (one.id)}<span class="dot" class:on={at === index}></span>{/each}
      </span>
      <span class="ds-clipped"
        >{m['tour.progress']({ current: index + 1, total: cards.length })}</span
      >
    </p>

    {#if index > 0}
      <Button onclick={() => (index -= 1)}>{m['tour.back']()}</Button>
    {:else}
      <Button variant="ghost" onclick={onclose}>{m['tour.skip']()}</Button>
    {/if}

    {#if last}
      <Button href={resolve('/(app)/recipes/new')} onclick={onclose}
        >{m['tour.firstRecipe']()}</Button
      >
      <Button variant="primary" onclick={onclose}>{m['tour.done']()}</Button>
    {:else}
      <Button variant="primary" onclick={() => (index += 1)}>{m['tour.next']()}</Button>
    {/if}
  {/snippet}
</Sheet>

<style>
  .card {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .art {
    display: grid;
    place-items: center;
    padding: var(--space-2);
    border-radius: var(--radius-lg);
    background: radial-gradient(ellipse, var(--surface-accent-subtle), transparent 72%);
  }

  .art :global(.olli) {
    width: 8rem;
    height: 8rem;
  }

  .body {
    margin: 0;
    font-size: var(--text-lg);
    line-height: var(--leading-normal);
  }

  .tabs,
  .points {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .tabs li {
    display: flex;
    align-items: flex-start;
    gap: var(--space-3);
  }

  .icon {
    flex: none;
    width: var(--space-6);
    height: var(--space-6);
    color: var(--accent);
  }

  .tab {
    color: var(--text-muted);
  }

  .tab strong {
    display: block;
    color: var(--text);
    font-weight: var(--weight-semibold);
  }

  .points li {
    position: relative;
    padding-inline-start: var(--space-4);
    line-height: var(--leading-normal);
  }

  .points li::before {
    content: '';
    position: absolute;
    inset-inline-start: 0;
    top: 0.6em;
    width: 0.4rem;
    height: 0.4rem;
    border-radius: var(--radius-full);
    background: var(--accent);
  }

  .example {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    margin: 0;
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-md);
    background: var(--surface-sunken);
    font-size: var(--text-sm);
  }

  .written {
    margin: 0;
    color: var(--text-muted);
    font-family: ui-monospace, monospace;
  }

  .cooked {
    margin: 0;
    line-height: 2;
  }

  .pill {
    padding: 0.1em 0.5em;
    border-radius: var(--radius-full);
    background: var(--surface-accent-subtle);
    font-weight: var(--weight-medium);
    white-space: nowrap;
  }

  .timer {
    margin-inline-start: var(--space-2);
    padding: 0.1em 0.5em;
    border: 1px solid var(--border);
    border-radius: var(--radius-full);
    color: var(--text-muted);
    white-space: nowrap;
  }

  .progress {
    margin: 0 auto 0 0;
  }

  .dots {
    display: inline-flex;
    gap: var(--space-1);
  }

  .dot {
    width: 0.5rem;
    height: 0.5rem;
    border-radius: var(--radius-full);
    background: var(--border-strong, var(--border));
    transition: background-color var(--duration-fast) var(--ease-out);
  }

  .dot.on {
    background: var(--accent);
  }
</style>
