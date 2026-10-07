<script lang="ts">
  import { untrack } from 'svelte';
  import { cubicOut, linear } from 'svelte/easing';
  import { prefersReducedMotion } from 'svelte/motion';

  import { createArrival } from './arrive';
  import { bodyTransform, brushGrip, eyeTransform, pencilGrip, rightHandlePath } from './geometry';
  import OlliHeldProp from './OlliHeldProp.svelte';
  import OlliSteam from './OlliSteam.svelte';
  import OlliTrailingProp from './OlliTrailingProp.svelte';
  import { olliSetting } from './setting.svelte';
  import { poses, type Pose } from './poses';
  import { createRig } from './rig.svelte';

  /**
   * Olli, the pot from Culina's logo, with a face and a chef's hat.
   *
   * Silent on purpose. The page's own words say what happened; Olli only shows
   * how it feels about it, which is why it is hidden from screen readers.
   *
   * Alive on arrival, then still. Arriving in a pose plays one small movement,
   * a blink or two may follow, and by five seconds nothing moves until
   * something happens — a new pose, or a poke. Every part sits on a spring, so
   * a pose that changes halfway through a movement simply heads for its new
   * place from wherever it was; the hat is on a softer spring than the pot, so
   * it lands a beat late.
   *
   * Always visible. Settings and the system motion preference stop movement.
   */
  interface Props {
    pose: Pose;
    size?: 'sm' | 'md' | 'lg';
    /** Never moves: for small, repeated places like a toast. */
    still?: boolean;
    /** Repeat meaningful work while processing; motion is controlled in Settings. */
    working?: boolean;
  }

  let { pose, size = 'md', still = false, working = false }: Props = $props();

  const motion = $derived(olliSetting.animated && !still && !prefersReducedMotion.current);
  const uid = $props.id();
  const spec = $derived(poses[pose]);
  const rig = createRig(
    poses[untrack(() => pose)],
    untrack(() => still)
  );

  // Plain fields, not state: bookkeeping that nothing renders. Reading state
  // here from inside the effect below would make it wake itself up.
  let timers: ReturnType<typeof setTimeout>[] = [];
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
    void rig.lid.set(0.1, { duration: 80, easing: linear });
    later(120, () => void rig.lid.set(1, { duration: 200, easing: cubicOut }));
  }

  const arrive = createArrival({ rig, later, rest, blink, watched, working: () => working });

  $effect(() => {
    const next = pose;
    const animate = motion;
    void working;

    untrack(() => arrive(next, animate));

    return rest;
  });

  /** Off screen or in a hidden tab, the blinks and beats are skipped. */
  $effect(() => {
    if (!svg || typeof IntersectionObserver === 'undefined') {
      return;
    }

    const observer = new IntersectionObserver(([entry]) => {
      const wasVisible = visible;
      visible = entry?.isIntersecting ?? true;
      if (working && motion && visible !== wasVisible) {
        rest();
        if (watched()) untrack(() => arrive(pose, true));
      }
    });

    observer.observe(svg);

    return () => observer.disconnect();
  });

  $effect(() => {
    if (!working || !motion) return;
    const resume = () => {
      if (!document.hidden && visible) {
        rest();
        untrack(() => arrive(pose, true));
      } else rest();
    };
    document.addEventListener('visibilitychange', resume);
    return () => document.removeEventListener('visibilitychange', resume);
  });

  function poke(): void {
    if (!motion || spec.sombre) {
      return;
    }

    const now = performance.now();

    taps = taps.filter((tap) => now - tap < 3000);

    // Once a second at most, and three pokes are enough.
    if (now - (taps.at(-1) ?? -Infinity) < 1000 || taps.length >= 3) {
      return;
    }

    taps.push(now);
    rig.squash(1.04, 0.94);
    later(120, () => rig.squash(1, 1));
    later(50, () => (rig.hat.target = spec.hatTilt + 4));
    later(200, () => (rig.hat.target = spec.hatTilt));
    blink();
  }

  const finePointer = typeof matchMedia === 'function' && matchMedia('(pointer: fine)').matches;

  function leanTo(towards: number): void {
    if (finePointer && motion && !spec.sombre) {
      rig.lean.target = towards;
    }
  }

  const body = $derived(
    bodyTransform(
      rig.lift.current,
      rig.tilt.current,
      rig.lean.current,
      rig.squashX.current,
      rig.squashY.current
    )
  );
  const eye = (x: number) => eyeTransform(x, rig.lid.current);

  // A single pair of green handles in every pose. When holding a prop, the
  // handle bends toward it; no second set of line-drawn limbs is added.
  const usingPencil = $derived(spec.prop === 'pencil');
  const usingBrush = $derived(spec.prop === 'brush');
  const holdingPhone = $derived(spec.prop === 'phone');
  const hand = $derived(
    usingPencil
      ? pencilGrip(rig.pencilX.current, rig.pencilY.current, rig.penLift.current)
      : usingBrush
        ? brushGrip(rig.brushX.current, rig.brushY.current)
        : ([87, 83] as const)
  );
  const rightHandle = $derived(rightHandlePath(usingBrush, hand));

  const hatPuffs = [
    [45, 29, 12],
    [60, 22, 14.5],
    [75, 29, 12]
  ] as const;
  const eyesAt = [50, 70];
</script>

<svg
  bind:this={svg}
  data-phase={pose === 'drawing' ? rig.drawingPhase : undefined}
  class="olli {size}"
  viewBox="-10 -14 140 134"
  aria-hidden="true"
  onclick={poke}
  onpointerenter={() => leanTo(2)}
  onpointerleave={() => leanTo(0)}
>
  <defs>
    <clipPath id="{uid}-shelf"><rect x="-10" y="-14" width="140" height="126" /></clipPath>
  </defs>

  <g opacity={rig.shown.current}>
    <ellipse class="shadow" cx="60" cy="110" rx="30" ry="4" />

    <g class="line">
      <OlliTrailingProp prop={spec.prop} {motion} />

      <g clip-path="url(#{uid}-shelf)">
        <g transform={body}>
          <path class="pot" d="M28 55 H92 V80 A24 24 0 0 1 68 104 H52 A24 24 0 0 1 28 80 Z" />
          <rect class="shine" x="35" y="62" width="6" height="22" rx="3" />
          <rect class="rim" x="22" y="48" width="76" height="9" rx="4.5" />

          <g transform="rotate({rig.hat.current} 60 47)">
            {#each hatPuffs as [x, y, r] (x)}
              <circle class="hat" cx={x} cy={y} {r} />
            {/each}
            {#each hatPuffs as [x, y, r] (x)}
              <circle class="hat bare" cx={x} cy={y} {r} />
            {/each}
            <rect class="hat" x="35" y="34" width="50" height="13" rx="3" />
            <path class="hat-shade" d="M39 43 h42" />
          </g>

          <g class="handle handle-left">
            {#if usingPencil || usingBrush || spec.prop === 'card'}
              <path
                class="rim"
                d="M29 65 C13 64 12 90 31 97 L39 98 Q45 96 39 92 L32 90 C23 86 24 75 31 73 Z"
              />
            {:else}
              <rect
                class="rim"
                x="11"
                y="61"
                width="18"
                height="10"
                rx="5"
                transform="rotate({rig.armL.current} 27 66)"
              />
            {/if}
          </g>
          {#if !usingBrush}
            <g class="handle handle-right">
              {#if usingPencil || holdingPhone}
                <path class="rim" d={rightHandle} />
              {:else}
                <rect
                  class="rim"
                  x="91"
                  y="61"
                  width="18"
                  height="10"
                  rx="5"
                  transform="rotate({-rig.armR.current} 93 66)"
                />
              {/if}
            </g>
          {/if}

          <ellipse class="cheek" cx="40" cy="84" rx="4.8" ry="3" />
          <ellipse class="cheek" cx="80" cy="84" rx="4.8" ry="3" />

          <g transform="translate({rig.lookX.current} {rig.lookY.current})">
            {#each eyesAt as x (x)}
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

          <OlliHeldProp prop={spec.prop} {motion} {rig} {rightHandle} />
        </g>
      </g>

      <OlliSteam steam={spec.steam} progress={rig.steam.current} />
    </g>
  </g>
</svg>

<style>
  /* The drawing is split across components — the props live in their own — so
     its rules reach every part from the root through `:global`. Each is still
     scoped to this one svg, and they keep their order and their weight
     relative to one another. */
  .olli {
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

  .olli :global(.shadow) {
    fill: var(--mascot-line);
    opacity: 0.08;
  }

  .olli :global(.line) {
    stroke: var(--mascot-line);
    stroke-width: 3;
    stroke-linejoin: round;
    stroke-linecap: round;
  }

  .olli :global(.pot) {
    fill: var(--mascot-body);
  }

  .olli :global(.shine) {
    fill: var(--mascot-shine);
    stroke: none;
  }

  .olli :global(.rim) {
    fill: var(--mascot-rim);
  }

  .olli :global(.hat) {
    fill: var(--mascot-hat);
  }

  .olli :global(.hat-shade) {
    stroke: var(--mascot-hat-shade);
    stroke-width: 2;
  }

  .olli :global(.bare) {
    stroke: none;
  }

  .olli :global(.cheek) {
    fill: var(--mascot-cheek);
    stroke: none;
    opacity: 0.6;
  }

  .olli :global(.ink) {
    fill: var(--mascot-line);
  }

  .olli :global(.glint) {
    fill: var(--mascot-hat);
  }

  .olli :global(.none) {
    fill: none;
    stroke-width: 2.6;
  }

  .olli :global(.thick) {
    stroke-width: 3;
  }

  .olli :global(.thin) {
    stroke-width: 2.2;
  }

  .olli :global(.mouth) {
    fill: var(--mascot-mouth);
    stroke-width: 2.2;
  }

  .olli :global(.phone) {
    fill: var(--mascot-line);
  }
  .olli :global(.screen) {
    fill: var(--mascot-hat);
  }
  .olli :global(.play),
  .olli :global(.pencil),
  .olli :global(.bulb) {
    fill: var(--mascot-cheek);
  }
  .olli :global(.bulb-rays) {
    stroke: var(--mascot-cheek);
  }

  .olli :global(.paint-plate) {
    fill: var(--mascot-hat);
  }
  .olli :global(.paint-food.warm) {
    fill: var(--mascot-cheek);
  }

  .olli :global(.paint-food) {
    fill: var(--mascot-rim);
  }
  .olli :global(.paint-detail) {
    stroke: var(--mascot-cheek);
  }

  .olli :global(.paper) {
    fill: var(--mascot-paper);
  }

  .olli :global(.paper.thin) {
    stroke-width: 2;
  }

  .olli :global(.paper-line) {
    stroke: var(--mascot-paper-line);
    stroke-width: 2;
  }

  .olli :global(.ticket-line) {
    stroke: var(--text-danger);
    stroke-width: 2.4;
  }

  .olli :global(.plug) {
    fill: var(--mascot-paper-line);
  }

  .olli :global(.steam) {
    fill: none;
    stroke: var(--mascot-steam);
    stroke-width: 2.8;
  }

  .olli :global(.question) {
    fill: var(--mascot-cheek);
    stroke-width: 1.2;
    font-family: var(--font-editorial);
    font-size: 24px;
  }

  .olli :global(.sleep) {
    fill: var(--mascot-line);
    stroke: none;
    font-family: var(--font-editorial);
    font-style: italic;
  }

  .olli :global(.spark) {
    fill: var(--mascot-cheek);
    stroke-width: 1.2;
  }
</style>
