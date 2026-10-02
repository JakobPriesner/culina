<script lang="ts">
  import type { Snippet } from 'svelte';

  import { untrack } from 'svelte';
  import { cubicOut, linear } from 'svelte/easing';
  import { prefersReducedMotion, Spring, Tween } from 'svelte/motion';
  import { fade } from 'svelte/transition';

  import { ollaSetting } from './setting.svelte';
  import { idleBlinks, poses, restAfter, type Pose } from './poses';

  /**
   * Olla, the pot from Culina's logo, with a face and a chef's hat.
   *
   * Silent on purpose. The page's own words say what happened; Olla only shows
   * how it feels about it, which is why it is hidden from screen readers.
   *
   * Alive on arrival, then still. Arriving in a pose plays one small movement,
   * a blink or two may follow, and by five seconds nothing moves until
   * something happens — a new pose, or a poke. Every part sits on a spring, so
   * a pose that changes halfway through a movement simply heads for its new
   * place from wherever it was; the hat is on a softer spring than the pot, so
   * it lands a beat late.
   *
   * Turned off on this device, it draws `fallback` instead — the plain version
   * of whatever it stands in for — or nothing.
   */
  interface Props {
    pose: Pose;
    size?: 'sm' | 'md' | 'lg';
    /** Never moves: for small, repeated places like a toast. */
    still?: boolean;
    fallback?: Snippet;
  }

  let { pose, size = 'md', still = false, fallback }: Props = $props();

  const uid = $props.id();
  const spec = $derived(poses[pose]);
  const start = poses[untrack(() => pose)];

  const firm = { stiffness: 0.18, damping: 0.55 };
  const bouncy = { stiffness: 0.25, damping: 0.45 };

  const tilt = new Spring(start.tilt, firm);
  const lean = new Spring(0, firm);
  const lift = new Spring(start.sag, { stiffness: 0.15, damping: 0.5 });
  const squashX = new Spring(1, bouncy);
  const squashY = new Spring(1, bouncy);
  const armL = new Spring(start.arms[0], firm);
  const armR = new Spring(start.arms[1], firm);
  const hat = new Spring(start.hatTilt, { stiffness: 0.1, damping: 0.35 });
  const lookX = new Spring(start.look[0], firm);
  const lookY = new Spring(start.look[1], firm);
  const lid = new Tween(1);
  const shown = new Tween(untrack(() => still) ? 1 : 0);
  /** How far the steam of this arrival has risen, from 0 to 1. */
  const steam = new Tween(0);

  // Plain fields, not state: bookkeeping that nothing renders. Reading state
  // here from inside the effect below would make it wake itself up.
  let timers: ReturnType<typeof setTimeout>[] = [];
  let arrived = false;
  let visible = true;
  let taps: number[] = [];
  let svg: SVGSVGElement | undefined = $state();

  const later = (ms: number, run: () => void) => timers.push(setTimeout(run, ms));
  const rest = () => {
    timers.forEach(clearTimeout);
    timers = [];
  };
  const watched = () => visible && !document.hidden;

  function blink(): void {
    if (spec.eyes !== 'open' || !watched()) {
      return;
    }

    // Shut fast, a moment closed, open slowly: the blink of somebody, not of
    // a shutter.
    void lid.set(0.1, { duration: 80, easing: linear });
    later(120, () => void lid.set(1, { duration: 200, easing: cubicOut }));
  }

  function squash(x: number, y: number): void {
    squashX.target = x;
    squashY.target = y;
  }

  /** What each pose does once, on arriving in it. */
  function perform(next: Pose): void {
    switch (next) {
      case 'hello':
        [35, 62, 38, 55].forEach((angle, i) => later(320 + i * 175, () => (armL.target = angle)));
        break;
      case 'reading':
        // Three lines read, then a hold: a loop past five seconds is motion
        // somebody would have to be able to stop.
        for (let i = 0; i < 6; i++) {
          later(i * 600, () => (lookX.target = i % 2 ? 1.5 : -1.5));
        }
        later(3600, () => (lookX.target = 0));
        break;
      case 'celebrating':
        squash(1.04, 0.94);
        later(120, () => {
          lift.target = -10;
          squash(0.96, 1.06);
          hat.target = -6;
        });
        later(320, () => {
          lift.target = 0;
          hat.target = 5;
        });
        later(440, () => squash(1.05, 0.95));
        later(560, () => {
          squash(1, 1);
          hat.target = 0;
        });
        break;
      case 'dozing':
        squash(1.015, 1.015);
        later(1200, () => squash(1, 1));
        break;
    }
  }

  function arrive(next: Pose, animate: boolean): void {
    const target = poses[next];
    const instant = !animate;
    const first = !arrived;

    arrived = true;
    void tilt.set(target.tilt, { instant });
    void armL.set(target.arms[0], { instant });
    void armR.set(target.arms[1], { instant });
    void hat.set(target.hatTilt, { instant });
    void lookX.set(target.look[0], { instant });
    void lookY.set(target.look[1], { instant });
    void lift.set(target.sag, { instant });
    squash(1, 1);

    if (first && !still) {
      // Fading is not motion, so even a reader who asked for less of it sees
      // Olla arrive rather than pop in.
      void shown.set(0, { duration: 0 });
      void shown.set(1, { duration: animate && next !== 'onDuty' ? 320 : 200, easing: cubicOut });

      if (animate && next !== 'onDuty') {
        void lift.set(next === 'peeking' ? 46 : 8 + target.sag, { instant: true });
        lift.target = target.sag;
      }
    }

    void steam.set(0, { duration: 0 });

    if (!animate) {
      // The steam that means something — a question, sleep — still shows.
      void steam.set(target.steam === 'question' || target.steam === 'sleep' ? 1 : 0, {
        duration: 0
      });

      return;
    }

    void steam.set(1, { duration: target.steam === 'sparks' ? 1100 : 750, delay: 150 });

    if (!first) {
      blink();
    }

    if (target.glance) {
      const [x, y] = target.glance;

      later(200, () => {
        lookX.target = target.look[0] + x;
        lookY.target = target.look[1] + y;
      });
    }

    perform(next);
    idleBlinks().forEach((ms) => later(ms, blink));
    later(restAfter, rest);
  }

  $effect(() => {
    const next = pose;
    const animate = !still && !prefersReducedMotion.current;

    untrack(() => arrive(next, animate));

    return rest;
  });

  /** Off screen or in a hidden tab, the blinks and beats are skipped. */
  $effect(() => {
    if (!svg || typeof IntersectionObserver === 'undefined') {
      return;
    }

    const observer = new IntersectionObserver(([entry]) => {
      visible = entry?.isIntersecting ?? true;
    });

    observer.observe(svg);

    return () => observer.disconnect();
  });

  function poke(): void {
    if (still || prefersReducedMotion.current || spec.sombre) {
      return;
    }

    const now = performance.now();

    taps = taps.filter((tap) => now - tap < 3000);

    // Once a second at most, and three pokes are enough.
    if (now - (taps.at(-1) ?? -Infinity) < 1000 || taps.length >= 3) {
      return;
    }

    taps.push(now);
    squash(1.04, 0.94);
    later(120, () => squash(1, 1));
    later(50, () => (hat.target = spec.hatTilt + 4));
    later(200, () => (hat.target = spec.hatTilt));
    blink();
  }

  const finePointer = typeof matchMedia === 'function' && matchMedia('(pointer: fine)').matches;

  function leanTo(towards: number): void {
    if (finePointer && !still && !prefersReducedMotion.current && !spec.sombre) {
      lean.target = towards;
    }
  }

  // The geometry, as SVG transforms: no CSS transform-origin to disagree
  // about between browsers.
  const body = $derived(
    `translate(0 ${lift.current}) rotate(${tilt.current + lean.current} 60 104) ` +
      `translate(60 104) scale(${squashX.current} ${squashY.current}) translate(-60 -104)`
  );
  const eye = (x: number) => `translate(${x} 75) scale(1 ${lid.current}) translate(${-x} -75)`;

  /** Steam: rises 8 units and fades. Kept when it means something. */
  const stays = $derived(spec.steam === 'question' || spec.steam === 'sleep');
  const steamOpacity = $derived(
    stays ? steam.current : Math.sin(Math.min(1, steam.current) * Math.PI) * 0.85
  );
  const steamRise = $derived((1 - steam.current) * 8 - (stays ? 0 : steam.current * 6));
  const sparks = [
    [14, 34, 7],
    [104, 22, 8],
    [110, 60, 5],
    [8, 64, 4.5],
    [60, -6, 5]
  ] as const;
</script>

{#if ollaSetting.shown}
  <svg
    bind:this={svg}
    class="olla {size}"
    viewBox="-10 -14 140 134"
    aria-hidden="true"
    onclick={poke}
    onpointerenter={() => leanTo(2)}
    onpointerleave={() => leanTo(0)}
  >
    <defs>
      <clipPath id="{uid}-shelf"><rect x="-10" y="-14" width="140" height="126" /></clipPath>
    </defs>

    <g opacity={shown.current}>
      <ellipse class="shadow" cx="60" cy="110" rx="30" ry="4" />

      <g class="line">
        {#if spec.prop === 'plug'}
          <g transition:fade={{ duration: 150 }}>
            <path class="none" d="M92 92 C108 96 110 108 98 110 C88 112 84 106 76 110" />
            <g transform="translate(70 110) rotate(-10)">
              <rect class="plug" x="-10" y="-5" width="12" height="10" rx="2" />
              <path d="M-10 -2.5 h-5 M-10 2.5 h-5" />
            </g>
          </g>
        {:else if spec.prop === 'ticket'}
          <g transform="translate(103 80) rotate(8)" transition:fade={{ duration: 150 }}>
            <rect class="paper thin" x="-12" y="-20" width="24" height="34" />
            <path class="paper-line" d="M-7 -12 h14 M-7 -6 h10 M-7 0 h14" />
            <path class="ticket-line" d="M-7 7 h14" />
          </g>
        {/if}

        <g clip-path="url(#{uid}-shelf)">
          <g transform={body}>
            <g transform="translate(27 66)">
              <rect
                class="rim"
                x="-16"
                y="-5"
                width="18"
                height="10"
                rx="5"
                transform="rotate({armL.current})"
              />
            </g>
            <g transform="translate(93 66) scale(-1 1)">
              <rect
                class="rim"
                x="-16"
                y="-5"
                width="18"
                height="10"
                rx="5"
                transform="rotate({armR.current})"
              />
            </g>

            <path class="pot" d="M28 55 H92 V80 A24 24 0 0 1 68 104 H52 A24 24 0 0 1 28 80 Z" />
            <rect class="shine" x="35" y="62" width="6" height="22" rx="3" />
            <rect class="rim" x="22" y="48" width="76" height="9" rx="4.5" />

            <g transform="rotate({hat.current} 60 47)">
              {#each [[45, 29, 12], [60, 22, 14.5], [75, 29, 12]] as [x, y, r] (x)}
                <circle class="hat" cx={x} cy={y} {r} />
              {/each}
              {#each [[45, 29, 12], [60, 22, 14.5], [75, 29, 12]] as [x, y, r] (x)}
                <circle class="hat bare" cx={x} cy={y} {r} />
              {/each}
              <rect class="hat" x="35" y="34" width="50" height="13" rx="3" />
              <path class="hat-shade" d="M39 43 h42" />
            </g>

            <ellipse class="cheek" cx="40" cy="84" rx="4.8" ry="3" />
            <ellipse class="cheek" cx="80" cy="84" rx="4.8" ry="3" />

            <g transform="translate({lookX.current} {lookY.current})">
              {#each [50, 70] as x (x)}
                <g transform={eye(x)}>
                  {#if spec.eyes === 'happy'}
                    <path class="none thick" d="M{x - 5} 76.5 q5 -7 10 0" />
                  {:else if spec.eyes === 'closed'}
                    <path class="none thick" d="M{x - 5} 74.5 q5 5 10 0" />
                  {:else}
                    <ellipse class="ink bare" cx={x} cy="75" rx="4.6" ry="5.8" />
                    <circle class="glint bare" cx={x - 1.5} cy="72.8" r="1.6" />
                  {/if}
                </g>
              {/each}
            </g>

            {#if spec.mouth === 'open'}
              <path class="mouth" d="M55 84 q5 8 10 0 z" />
            {:else if spec.mouth === 'o'}
              <ellipse class="mouth" cx="60" cy="86" rx="2.6" ry="3.2" />
            {:else if spec.mouth === 'flat'}
              <path class="none" d="M56 86 h8" />
            {:else if spec.mouth === 'wobble'}
              <path class="none" d="M54 86.5 q3 -2.5 6 0 q3 2.5 6 0" />
            {:else}
              <path class="none" d="M56 85 q4 4 8 0" />
            {/if}

            {#if spec.brows === 'worried'}
              <path class="none thin" d="M45 67 l8 -3 M75 67 l-8 -3" />
            {:else if spec.brows === 'puzzled'}
              <path class="none thin" d="M66 64 q4 -3 8 0" />
            {/if}

            {#if spec.prop === 'card'}
              <g transform="translate(60 98) rotate(-4)" transition:fade={{ duration: 150 }}>
                <rect class="paper" x="-17" y="-12" width="34" height="22" rx="2.5" />
                <path class="paper-line" d="M-11 -5 h22 M-11 0 h16 M-11 5 h19" />
              </g>
            {/if}
          </g>
        </g>

        <g opacity={steamOpacity} transform="translate(0 {steamRise})">
          {#if spec.steam === 'wisp'}
            <path class="steam" d="M98 44 q-6 -8 0 -16 q6 -8 0 -16" />
          {:else if spec.steam === 'question'}
            <text class="question" x="96" y="34">?</text>
          {:else if spec.steam === 'sleep'}
            <text class="sleep" x="96" y="38" font-size="14">z</text>
            <text class="sleep" x="106" y="26" font-size="10">z</text>
          {:else if spec.steam === 'sparks'}
            {#each sparks as [x, y, s], i (i)}
              <path
                class="spark"
                d="M{x} {y - s} Q{x} {y} {x + s} {y} Q{x} {y} {x} {y + s} Q{x} {y} {x -
                  s} {y} Q{x} {y} {x} {y - s}Z"
              />
            {/each}
          {/if}
        </g>
      </g>
    </g>
  </svg>
{:else}
  {@render fallback?.()}
{/if}

<style>
  .olla {
    display: block;
    flex: none;
    overflow: visible;
  }

  .sm {
    width: 3.5rem;
    height: 3.5rem;
  }

  .md {
    width: 7.5rem;
    height: 7.5rem;
  }

  .lg {
    width: 10rem;
    height: 10rem;
  }

  .shadow {
    fill: var(--mascot-line);
    opacity: 0.08;
  }

  .line {
    stroke: var(--mascot-line);
    stroke-width: 3;
    stroke-linejoin: round;
    stroke-linecap: round;
  }

  .pot {
    fill: var(--mascot-body);
  }

  .shine {
    fill: var(--mascot-shine);
    stroke: none;
  }

  .rim {
    fill: var(--mascot-rim);
  }

  .hat {
    fill: var(--mascot-hat);
  }

  .hat-shade {
    stroke: var(--mascot-hat-shade);
    stroke-width: 2;
  }

  .bare {
    stroke: none;
  }

  .cheek {
    fill: var(--mascot-cheek);
    stroke: none;
    opacity: 0.6;
  }

  .ink {
    fill: var(--mascot-line);
  }

  .glint {
    fill: var(--mascot-hat);
  }

  .none {
    fill: none;
    stroke-width: 2.6;
  }

  .thick {
    stroke-width: 3;
  }

  .thin {
    stroke-width: 2.2;
  }

  .mouth {
    fill: var(--mascot-mouth);
    stroke-width: 2.2;
  }

  .paper {
    fill: var(--mascot-paper);
  }

  .paper.thin {
    stroke-width: 2;
  }

  .paper-line {
    stroke: var(--mascot-paper-line);
    stroke-width: 2;
  }

  .ticket-line {
    stroke: var(--text-danger);
    stroke-width: 2.4;
  }

  .plug {
    fill: var(--mascot-paper-line);
  }

  .steam {
    fill: none;
    stroke: var(--mascot-steam);
    stroke-width: 2.8;
  }

  .question {
    fill: var(--mascot-cheek);
    stroke-width: 1.2;
    font-family: var(--font-editorial);
    font-size: 24px;
  }

  .sleep {
    fill: var(--mascot-line);
    stroke: none;
    font-family: var(--font-editorial);
    font-style: italic;
  }

  .spark {
    fill: var(--mascot-cheek);
    stroke-width: 1.2;
  }
</style>
