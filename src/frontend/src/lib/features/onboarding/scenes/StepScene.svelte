<script lang="ts">
  import { Stepper } from '$ds';
  import { formatNumber, m } from '$shell/i18n';

  import { moving, play, typing, type Beat } from './demo';
  import type { React } from './reaction';

  /**
   * How to write a step, shown being written: `@` picks an ingredient, which then stands in the step at
   * the amount for the servings, and a wait named in minutes becomes a timer. Then a pointer presses "+"
   * twice and every amount visibly follows (step, ingredient line, ×2), before the cook tries it. The three
   * rules under it are the ones the editor's tip repeats.
   */
  interface Props {
    onreact: React;
  }

  let { onreact }: Props = $props();

  type Ingredient = 'onion' | 'butter';

  type Piece =
    | { kind: 'text'; text: string }
    | { kind: 'ingredient'; which: Ingredient; typed: string; picked: boolean }
    | { kind: 'time'; text: string; marked: boolean };

  const names: Record<Ingredient, () => string> = {
    onion: m['tour.steps.demo.onion.name'],
    butter: m['tour.steps.demo.butter.name']
  };

  let pieces = $state<Piece[]>([]);
  let servings = $state(2);
  let written = $state(false);
  let scaled = false;

  /** The demo's own pointer, in px from the scene's corner; never in the way of a real one. */
  let pointer = $state({ x: 0, y: 0, shown: false, pressing: false });
  let stage: HTMLElement | undefined = $state();
  let text: HTMLElement | undefined = $state();
  let stepper: HTMLElement | undefined = $state();

  /** Moves the pointer onto the middle of `target`. */
  function aim(target: Element | null | undefined) {
    if (!stage || !target) return;

    const scene = stage.getBoundingClientRect();
    const box = target.getBoundingClientRect();

    pointer.x = box.left - scene.left + box.width / 2;
    pointer.y = box.top - scene.top + box.height / 2;
  }

  const more = () => stepper?.querySelector(`[aria-label="${m['tour.steps.more']()}"]`);

  /** The pointer presses "+" twice: the servings double, and so does every amount. */
  function demonstrate(at: number): Beat[] {
    const press = (when: number, next: number): Beat[] => [
      [
        when,
        () => {
          pointer.pressing = true;
          servings = next;
        }
      ],
      [when + 200, () => (pointer.pressing = false)]
    ];

    return [
      [
        at,
        () => {
          aim(text);
          pointer.shown = true;
        }
      ],
      [at + 250, () => aim(more())],
      ...press(at + 1100, 3),
      ...press(at + 1900, 4),
      [at + 2200, () => onreact('celebrating', m['tour.steps.doubled.said']())],
      [at + 3200, () => (pointer.shown = false)]
    ];
  }

  /** As the read view writes it: the amount in weight, then the name, both following the servings. */
  const amountOf = (which: Ingredient) =>
    which === 'onion'
      ? { amount: String(servings), name: m['tour.steps.demo.onion']({ count: servings }) }
      : { amount: `${servings * 10} g`, name: names.butter() };

  /** The step's words, appended a letter at a time; `@` mentions pause on their suggestion before it is picked. */
  function script(): Beat[] {
    const beats: Beat[] = [];
    let at = 400;
    let count = 0;

    /** Appends a piece at the current time; returns a way to change it later. */
    const add = (piece: Piece) => {
      const index = count++;
      beats.push([at, () => void pieces.push(piece)]);

      return (update: (piece: Piece) => void) => () => {
        const current = pieces[index];
        if (current) update(current);
      };
    };

    const words = (kind: 'text' | 'time', written: string) => {
      const change = add(kind === 'text' ? { kind, text: '' } : { kind, text: '', marked: false });
      const typed = typing(written, at, (shown) =>
        change((piece) => {
          if (piece.kind !== 'ingredient') piece.text = shown;
        })()
      );
      beats.push(...typed.beats);
      at = typed.end;

      return change;
    };

    const mention = (which: Ingredient) => {
      const change = add({ kind: 'ingredient', which, typed: '@', picked: false });
      const typed = typing(
        names[which]().slice(0, 3),
        at + 120,
        (shown) =>
          change((piece) => {
            if (piece.kind === 'ingredient') piece.typed = `@${shown}`;
          })(),
        110
      );
      beats.push(...typed.beats);
      at = typed.end + 650;
      beats.push([
        at,
        change((piece) => {
          if (piece.kind === 'ingredient') piece.picked = true;
        })
      ]);
      at += 250;
    };

    words('text', m['tour.steps.demo.before']());
    mention('onion');
    words('text', m['tour.steps.demo.middle']());
    mention('butter');
    words('text', m['tour.steps.demo.beforeTime']());
    const wait = words('time', m['tour.steps.demo.time']());
    words('text', m['tour.steps.demo.after']());

    beats.push([
      at + 300,
      wait((piece) => {
        if (piece.kind === 'time') piece.marked = true;
      })
    ]);
    // Where nothing moves by itself, the cook is asked to press "+" instead of being shown.
    const shown = moving();

    beats.push([
      at + 900,
      () => {
        written = true;
        onreact('idea', shown ? m['tour.steps.written.watch']() : m['tour.steps.written.said']());
      }
    ]);

    if (shown) beats.push(...demonstrate(at + 1700));

    return beats;
  }

  $effect(() => play(script()));

  function rescale(next: number) {
    servings = next;

    if (scaled) return;

    scaled = true;
    onreact('celebrating', m['tour.steps.scaled.said']());
  }

  const timed = $derived(pieces.some((piece) => piece.kind === 'time' && piece.marked));
</script>

<div class="steps" bind:this={stage}>
  <figure class="editor">
    <figcaption class="label">{m['tour.steps.demo.label']()}</figcaption>
    <p class="text" aria-hidden={!written} bind:this={text}>
      {#each pieces as piece, index (index)}
        {#if piece.kind === 'text'}{piece.text}{:else if piece.kind === 'time'}<span
            class="time"
            class:marked={piece.marked}>{piece.text}</span
          >{:else if piece.picked}<span class="ingredient"
            >{#key servings}<span class="amount">{amountOf(piece.which).amount}</span>{/key}
            {amountOf(piece.which).name}</span
          >{:else}<span class="typing"
            >{piece.typed}<span class="suggest" aria-hidden="true"
              ><span class="option">{names[piece.which]()}</span></span
            ></span
          >{/if}
      {/each}{#if !written}<span class="caret"></span>{/if}
    </p>

    {#if written}
      <p class="uses">
        {m['tour.steps.demo.uses']({ count: servings })}
        {#each ['onion', 'butter'] as const as which (which)}
          {#key servings}<span class="use"
              ><strong>{amountOf(which).amount}</strong> {amountOf(which).name}</span
            >{/key}
        {/each}
      </p>
    {/if}

    {#if timed}
      <span class="timer">
        <svg
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.8"
          stroke-linecap="round"
          aria-hidden="true"
        >
          <circle cx="12" cy="13" r="7" />
          <path d="M12 9v4l2.5 1.5M10 3h4" />
        </svg>
        {m['tour.steps.demo.timer']()}
      </span>
    {/if}
  </figure>

  <div class="servings" class:shown={written} bind:this={stepper}>
    <span class="servings-label" aria-hidden="true">{m['tour.steps.servings']()}</span>
    <Stepper
      id="tour-servings"
      value={servings}
      label={m['tour.steps.servings']()}
      decreaseLabel={m['tour.steps.fewer']()}
      increaseLabel={m['tour.steps.more']()}
      min={1}
      max={12}
      disabled={!written}
      onchange={rescale}
    />
    <!-- Always there, hidden at the recipe's own servings: appearing would shift the stepper under the pointer. -->
    {#key servings}<span class="factor" class:same={servings === 2}
        >×{formatNumber(servings / 2)}</span
      >{/key}
  </div>

  <span
    class="pointer"
    class:shown={pointer.shown}
    class:pressing={pointer.pressing}
    style:translate="{pointer.x}px {pointer.y}px"
    aria-hidden="true"
  >
    <svg viewBox="0 0 24 24">
      <path d="M5 3l14 8-6 1.6L10 19Z" />
    </svg>
  </span>

  <ul class="rules">
    <li>
      <strong>{m['tour.steps.rule.one']()}</strong>
      <span>{m['tour.steps.rule.one.why']()}</span>
    </li>
    <li>
      <strong>{m['tour.steps.rule.short']()}</strong>
      <span>{m['tour.steps.rule.short.why']()}</span>
    </li>
    <li>
      <strong>{m['tour.steps.rule.wait']()}</strong>
      <span>{m['tour.steps.rule.wait.why']()}</span>
    </li>
  </ul>
</div>

<style>
  .steps {
    position: relative;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--space-4);
  }

  .editor {
    position: relative;
    width: min(26rem, 100%);
    min-height: 9rem;
    margin: 0;
    padding: var(--space-3) var(--space-4) var(--space-4);
    border: 1px solid var(--border-strong);
    border-radius: var(--radius-md);
    background: var(--surface);
  }

  .label {
    margin-bottom: var(--space-2);
    color: var(--text-muted);
    font-size: var(--text-xs);
    font-weight: var(--weight-medium);
  }

  .text {
    margin: 0;
    font-size: var(--text-lg);
    line-height: 1.6;
  }

  /* The read view's own mark (StepInline): a highlighter stroke, the amount in weight. */
  .ingredient {
    padding: 0 0.12em;
    margin: 0 -0.12em;
    border-radius: var(--radius-sm);
    background-image: linear-gradient(var(--surface-highlight), var(--surface-highlight));
    background-position: 0 100%;
    background-repeat: no-repeat;
    background-size: 100% 0.6em;
    animation: mark var(--duration-slow) var(--ease-out);
  }

  .amount {
    display: inline-block;
    font-weight: var(--weight-semibold);
    font-variant-numeric: tabular-nums;
    animation: changed 0.6s var(--ease-out);
  }

  .uses {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    gap: var(--space-1) var(--space-2);
    margin: var(--space-3) 0 0;
    padding-top: var(--space-2);
    border-top: 1px dashed var(--border);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .use {
    display: inline-block;
    padding: 0 var(--space-2);
    border-radius: var(--radius-full);
    background: var(--surface-sunken);
    color: var(--text);
    animation: changed 0.6s var(--ease-out);
  }

  .factor.same {
    visibility: hidden;
  }

  .factor {
    padding: var(--space-1) var(--space-2);
    border-radius: var(--radius-full);
    background: var(--accent);
    color: var(--accent-contrast);
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
    font-variant-numeric: tabular-nums;
    animation: changed 0.6s var(--ease-out);
  }

  /* The demo's pointer: its tip sits on the point aimed at. */
  .pointer {
    position: absolute;
    top: 0;
    left: 0;
    z-index: 2;
    width: 1.6rem;
    height: 1.6rem;
    /* The arrow's tip, not the box's corner, on the point. */
    margin: -0.2rem 0 0 -0.33rem;
    opacity: 0;
    pointer-events: none;
    transition:
      translate 0.75s var(--ease-spatial),
      opacity var(--duration-base) var(--ease-out);
  }

  .pointer svg {
    width: 100%;
    height: 100%;
    fill: var(--text);
    stroke: var(--surface);
    stroke-width: 1.5;
    stroke-linejoin: round;
    filter: drop-shadow(0 1px 2px var(--scrim));
    transition: scale var(--duration-fast) var(--ease-out);
    transform-origin: 20% 10%;
  }

  .pointer.shown {
    opacity: 1;
  }

  .pointer.pressing svg {
    scale: 0.82;
  }

  /* A ring where it pressed, like a tap shown in a screen recording. */
  .pointer.pressing::before {
    content: '';
    position: absolute;
    top: -0.9rem;
    left: -0.9rem;
    width: 1.8rem;
    height: 1.8rem;
    border: 2px solid var(--accent);
    border-radius: var(--radius-full);
    animation: tap 0.4s var(--ease-out) forwards;
  }

  .typing {
    position: relative;
    color: var(--accent);
    font-weight: var(--weight-medium);
  }

  .suggest {
    position: absolute;
    top: 100%;
    inset-inline-start: 0;
    z-index: 1;
    padding: var(--space-1);
    border: 1px solid var(--border);
    border-radius: var(--radius-sm);
    background: var(--surface-overlay);
    box-shadow: var(--shadow-overlay);
    font-size: var(--text-sm);
    animation: drop var(--duration-base) var(--ease-out);
  }

  .option {
    display: block;
    padding: var(--space-1) var(--space-2);
    border-radius: var(--radius-sm);
    background: var(--surface-selected);
    color: var(--text);
    white-space: nowrap;
  }

  .time.marked {
    text-decoration: underline dotted var(--accent);
    text-underline-offset: 0.2em;
  }

  .caret {
    display: inline-block;
    width: 2px;
    height: 1.1em;
    margin-inline-start: 1px;
    vertical-align: text-bottom;
    background: var(--accent);
    animation: blink 1s steps(1) infinite;
  }

  .timer {
    display: inline-flex;
    align-items: center;
    gap: var(--space-1);
    margin-top: var(--space-3);
    padding: var(--space-1) var(--space-3);
    border: 1px solid var(--border-accent);
    border-radius: var(--radius-full);
    color: var(--accent);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    animation: drop var(--duration-slow) var(--ease-spatial);
  }

  .timer svg {
    width: 1rem;
    height: 1rem;
  }

  .servings {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    opacity: 0.35;
    transition: opacity var(--duration-slow) var(--ease-out);
  }

  .servings-label {
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
  }

  .servings.shown {
    opacity: 1;
  }

  .rules {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr));
    gap: var(--space-2);
    width: 100%;
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .rules li {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    padding: var(--space-3);
    border-radius: var(--radius-md);
    background: var(--surface-sunken);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  .rules span {
    color: var(--text-muted);
    font-size: var(--text-xs);
  }

  /* An amount that just changed: bigger and in the accent for a moment, so the eye goes to it. */
  @keyframes changed {
    from {
      color: var(--accent);
      scale: 1.35;
    }
  }

  @keyframes tap {
    from {
      opacity: 1;
      scale: 0.4;
    }

    to {
      opacity: 0;
      scale: 1.4;
    }
  }

  @keyframes mark {
    from {
      background-size: 0 0.6em;
    }
  }

  @keyframes drop {
    from {
      opacity: 0;
      transform: translateY(calc(var(--space-1) * -1));
    }
  }

  @keyframes blink {
    50% {
      opacity: 0;
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .amount,
    .use,
    .factor,
    .ingredient,
    .suggest,
    .timer,
    .caret {
      animation: none;
    }
  }
</style>
