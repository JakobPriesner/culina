<script lang="ts">
  import { untrack } from 'svelte';
  import { cubicOut, linear } from 'svelte/easing';
  import { prefersReducedMotion } from 'svelte/motion';

  import { createArrival } from './arrive';
  import { createAudience } from './audience.svelte';
  import { bodyTransform } from './geometry';
  import OlliFace from './OlliFace.svelte';
  import OlliHat from './OlliHat.svelte';
  import OlliHandles from './OlliHandles.svelte';
  import OlliSteam from './OlliSteam.svelte';
  import { heldParts, trailingParts } from './poses/parts';
  import { olliSetting } from './setting.svelte';
  import { poses, type Pose } from './poses';
  import { createRig } from './rig.svelte';
  import { createTouch } from './touch';

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
  // The props a pose carries live in their own components, beside the pot or in its hands.
  const Trailing = $derived(trailingParts[pose]);
  const Held = $derived(heldParts[pose]);
  const rig = createRig(
    poses[untrack(() => pose)],
    untrack(() => still)
  );

  // Plain field, not state: bookkeeping that nothing renders.
  let timers: ReturnType<typeof setTimeout>[] = [];
  let svg: SVGSVGElement | undefined = $state();

  const later = (ms: number, run: () => void) => timers.push(setTimeout(run, ms));
  const rest = () => {
    timers.forEach(clearTimeout);
    timers = [];
  };

  function blink(): void {
    if (spec.eyes !== 'open' || !audience.watched()) {
      return;
    }

    // Shut fast, a moment closed, open slowly: the blink of somebody, not of
    // a shutter.
    void rig.lid.set(0.1, { duration: 80, easing: linear });
    later(120, () => void rig.lid.set(1, { duration: 200, easing: cubicOut }));
  }

  const audience = createAudience({
    element: () => svg,
    active: () => working && motion,
    rest,
    restart: () => untrack(() => arrive(pose, true))
  });
  const arrive = createArrival({
    rig,
    later,
    rest,
    blink,
    watched: audience.watched,
    working: () => working
  });
  const { poke, leanTo } = createTouch({
    rig,
    spec: () => spec,
    motion: () => motion,
    later,
    blink
  });

  $effect(() => {
    const next = pose;
    const animate = motion;
    void working;

    untrack(() => arrive(next, animate));

    return rest;
  });

  const body = $derived(
    bodyTransform(
      rig.lift.current,
      rig.tilt.current,
      rig.lean.current,
      rig.squashX.current,
      rig.squashY.current
    )
  );
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
      {#if Trailing}<Trailing {motion} {rig} />{/if}

      <g clip-path="url(#{uid}-shelf)">
        <g transform={body}>
          <path class="pot" d="M28 55 H92 V80 A24 24 0 0 1 68 104 H52 A24 24 0 0 1 28 80 Z" />
          <rect class="shine" x="35" y="62" width="6" height="22" rx="3" />
          <rect class="rim" x="22" y="48" width="76" height="9" rx="4.5" />

          <OlliHat tilt={rig.hat.current} />

          <OlliHandles prop={spec.prop} {rig} />

          <OlliFace {spec} {rig} />

          {#if Held}<Held {motion} {rig} />{/if}
        </g>
      </g>

      <OlliSteam steam={spec.steam} progress={rig.steam.current} />
    </g>
  </g>
</svg>

<style>
  /* The drawing is split across components, so the rules every pose shares
     reach them from the root through `:global`, scoped to this one svg. What
     only one part uses is styled by that part. */
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

  .olli :global(.bare) {
    stroke: none;
  }

  .olli :global(.ink) {
    fill: var(--mascot-line);
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

  .olli :global(.pencil) {
    fill: var(--mascot-cheek);
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
</style>
