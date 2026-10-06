<script lang="ts">
  import { untrack } from 'svelte';
  import { cubicOut, linear } from 'svelte/easing';
  import { prefersReducedMotion, Spring, Tween } from 'svelte/motion';
  import { fade } from 'svelte/transition';

  import { olliSetting } from './setting.svelte';
  import { idleBlinks, poses, restAfter, type Pose } from './poses';

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
  const pencil = new Tween(0);
  const pencilX = new Tween(-15);
  const pencilY = new Tween(-5);
  const penLift = new Tween(0);
  const brushX = new Tween(97);
  const brushY = new Tween(78);
  const paint = new Tween(0);
  let drawingPhase = $state('inspecting');
  let warmBrush = $state(false);

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
      case 'watching':
        later(700, () => (hat.target = 3));
        later(1300, () => (hat.target = 0));
        later(2100, blink);
        break;
      case 'thinking':
        later(650, () => {
          lookX.target = 2;
          lookY.target = -3;
        });
        later(1800, () => {
          tilt.target = 3;
          hat.target = 1;
        });
        later(2900, () => {
          lookX.target = -2;
          tilt.target = -4;
          hat.target = -2;
        });
        break;
      case 'writing':
        void pencil.set(0, { duration: 0 });
        [0, 1, 2].forEach((line) => {
          const start = line * 1150;
          later(start, () => void penLift.set(3, { duration: 80 }));
          later(start + 90, () => {
            void pencilX.set(-15, { duration: 180, easing: cubicOut });
            void pencilY.set(line * 6 - 5, { duration: 180, easing: cubicOut });
          });
          later(start + 280, () => void penLift.set(0, { duration: 100 }));
          later(start + 400, () => {
            void pencil.set(line + 1, { duration: 650, easing: linear });
            void pencilX.set(line === 2 ? 3 : 14, { duration: 650, easing: linear });
          });
        });
        later(3400, () => void penLift.set(2, { duration: 150 }));
        later(3650, () => (lookY.target = 1));
        break;
      case 'drawing': {
        // A complete work phrase, with brisk strokes and purposeful holds.
        // Keep the painting on later cycles: Olli refines it, never wipes it.
        const stroke = (at: number, x: number, y: number, progress: number) =>
          later(at, () => {
            drawingPhase = 'painting';
            void brushX.set(x, { duration: 550, easing: cubicOut });
            void brushY.set(y, { duration: 550, easing: cubicOut });
            void paint.set(Math.max(paint.current, progress), { duration: 550, easing: linear });
            lookX.target = 3;
            lookY.target = 2;
          });
        const inspect = (at: number) =>
          later(at, () => {
            drawingPhase = 'inspecting';
            void brushX.set(111, { duration: 300, easing: cubicOut });
            void brushY.set(70, { duration: 300, easing: cubicOut });
            tilt.target = -4;
            lookY.target = 0;
          });
        const colour = (at: number, warm: boolean) =>
          later(at, () => {
            drawingPhase = 'choosing-colour';
            warmBrush = warm;
            void brushX.set(80, { duration: 400, easing: cubicOut });
            void brushY.set(94, { duration: 400, easing: cubicOut });
            lookX.target = -3;
            lookY.target = 3;
            tilt.target = -2;
          });
        colour(0, false);
        stroke(1200, 106, 75, 0.4);
        stroke(2100, 98, 83, 0.7);
        stroke(3000, 108, 87, 1);
        inspect(4100);
        if (working) {
          colour(6500, true);
          stroke(8500, 102, 78, 1.3);
          stroke(9600, 107, 84, 1.7);
          stroke(10800, 98, 82, 2);
          inspect(12200);
          colour(15000, false);
          stroke(17200, 108, 80, 2.3);
          stroke(18300, 100, 85, 2.6);
          stroke(19400, 105, 77, 3);
          inspect(20700);
          stroke(22800, 108, 83, 3);
          inspect(24000);
          later(25700, blink);
        } else {
          stroke(3800, 108, 87, 3);
        }
        break;
      }
      case 'idea':
        later(120, () => {
          lift.target = -3;
          hat.target = -4;
        });
        later(650, () => {
          lift.target = 0;
          hat.target = 0;
        });
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
    void squashX.set(1, { instant });
    void squashY.set(1, { instant });
    void lean.set(0, { instant });

    if (!animate) void shown.set(1, { duration: 0 });
    if (first && animate) {
      // Fading is not motion, so even a reader who asked for less of it sees
      // Olli arrive rather than pop in.
      void shown.set(0, { duration: 0 });
      void shown.set(1, { duration: animate && next !== 'onDuty' ? 320 : 200, easing: cubicOut });

      if (animate && next !== 'onDuty') {
        void lift.set(next === 'peeking' ? 46 : 8 + target.sag, { instant: true });
        lift.target = target.sag;
      }
    }

    void steam.set(0, { duration: 0 });

    if (!animate) {
      drawingPhase = 'still';
      warmBrush = false;
      void lid.set(1, { duration: 0 });
      // The steam that means something — a question, sleep — still shows.
      void steam.set(
        target.steam === 'bulb'
          ? 0.5
          : target.steam === 'question' || target.steam === 'sleep'
            ? 1
            : 0,
        {
          duration: 0
        }
      );

      void pencil.set(next === 'writing' ? 3 : 0, { duration: 0 });
      void pencilX.set(3, { duration: 0 });
      void pencilY.set(7, { duration: 0 });
      void penLift.set(0, { duration: 0 });
      void brushX.set(108, { duration: 0 });
      void brushY.set(87, { duration: 0 });
      void paint.set(next === 'drawing' ? 3 : 0, { duration: 0 });
      return;
    }

    void steam.set(1, {
      duration: target.steam === 'bulb' ? 2400 : target.steam === 'sparks' ? 1100 : 750,
      delay: 150
    });

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
    later(next === 'drawing' && working ? 28000 : restAfter, () => {
      rest();
      if (working && watched()) later(900, () => arrive(next, true));
    });
  }

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
    squash(1.04, 0.94);
    later(120, () => squash(1, 1));
    later(50, () => (hat.target = spec.hatTilt + 4));
    later(200, () => (hat.target = spec.hatTilt));
    blink();
  }

  const finePointer = typeof matchMedia === 'function' && matchMedia('(pointer: fine)').matches;

  function leanTo(towards: number): void {
    if (finePointer && motion && !spec.sombre) {
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
  // Keep the green handle attached as the tip crosses each line. The paper and
  // pencil use different rotations, so apply both to the grip's position.
  const gripLocalX = $derived(pencilX.current + 9.9);
  const gripLocalY = $derived(pencilY.current - penLift.current - 5);
  const gripX = $derived(57 + gripLocalX * 0.9962 + gripLocalY * 0.0872);
  const gripY = $derived(96 - gripLocalX * 0.0872 + gripLocalY * 0.9962);
  const brushGripX = $derived(brushX.current + 9);
  const brushGripY = $derived(brushY.current - 14);

  // A single pair of green handles in every pose. When holding a prop, the
  // handle bends toward it; no second set of line-drawn limbs is added.
  const usingPencil = $derived(spec.prop === 'pencil');
  const usingBrush = $derived(spec.prop === 'brush');
  const holdingPhone = $derived(spec.prop === 'phone');
  const handX = $derived(usingPencil ? gripX : usingBrush ? brushGripX : 87);
  const handY = $derived(usingPencil ? gripY : usingBrush ? brushGripY : 83);
  const rightHandle = $derived(
    usingBrush
      ? `M91 62 C101 62 109 63 ${handX} ${handY - 4}
       Q${handX + 6} ${handY} ${handX} ${handY + 4}
       C105 70 102 72 91 71 Z`
      : `M91 62 C110 62 116 89 ${handX + 4} ${handY + 4}
       Q${handX - 2} ${handY + 7} ${handX - 3} ${handY + 1}
       Q${handX - 4} ${handY - 3} ${handX + 2} ${handY - 4}
       C102 82 102 74 91 71 Z`
  );

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

<svg
  bind:this={svg}
  data-phase={pose === 'drawing' ? drawingPhase : undefined}
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

  <g opacity={shown.current}>
    <ellipse class="shadow" cx="60" cy="110" rx="30" ry="4" />

    <g class="line">
      {#if spec.prop === 'plug'}
        <g transition:fade={{ duration: motion ? 150 : 0 }}>
          <path class="none" d="M92 92 C108 96 110 108 98 110 C88 112 84 106 76 110" />
          <g transform="translate(70 110) rotate(-10)">
            <rect class="plug" x="-10" y="-5" width="12" height="10" rx="2" />
            <path d="M-10 -2.5 h-5 M-10 2.5 h-5" />
          </g>
        </g>
      {:else if spec.prop === 'ticket'}
        <g transform="translate(103 80) rotate(8)" transition:fade={{ duration: motion ? 150 : 0 }}>
          <rect class="paper thin" x="-12" y="-20" width="24" height="34" />
          <path class="paper-line" d="M-7 -12 h14 M-7 -6 h10 M-7 0 h14" />
          <path class="ticket-line" d="M-7 7 h14" />
        </g>
      {/if}

      <g clip-path="url(#{uid}-shelf)">
        <g transform={body}>
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
                transform="rotate({armL.current} 27 66)"
              />
            {/if}
          </g>
          {#if !usingBrush}
            <g class="handle handle-right">
              {#if usingPencil || usingBrush || holdingPhone}
                <path class="rim" d={rightHandle} />
              {:else}
                <rect
                  class="rim"
                  x="91"
                  y="61"
                  width="18"
                  height="10"
                  rx="5"
                  transform="rotate({-armR.current} 93 66)"
                />
              {/if}
            </g>
          {/if}

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

          {#if spec.prop === 'phone'}
            <g
              transform="translate(99 76) rotate(-12)"
              transition:fade={{ duration: motion ? 180 : 0 }}
            >
              <rect class="phone" x="-11" y="-20" width="22" height="38" rx="4" />
              <rect class="screen bare" x="-7" y="-14" width="14" height="23" rx="1.5" />
              <path class="play bare" d="M-3 -9 l7 5 -7 5 Z" />
              <path class="paper-line" d="M-2 13 h4" />
            </g>
          {:else if spec.prop === 'pencil'}
            <g
              transform="translate(57 96) rotate(-5)"
              transition:fade={{ duration: motion ? 180 : 0 }}
            >
              <rect class="paper thin" x="-22" y="-12" width="45" height="25" rx="2" />
              {#each [0, 1, 2] as line (line)}
                <path
                  class="paper-line"
                  d="M-15 {line * 6 - 5} h{Math.min(1, Math.max(0, pencil.current - line)) *
                    (line === 2 ? 18 : 29)}"
                />
              {/each}
              <g
                transform="translate({pencilX.current} {pencilY.current -
                  penLift.current}) rotate(27)"
              >
                <path class="pencil thin" d="M0 0 l-2 -7 v-20 h5 v20 Z" />
                <path class="ink bare" d="M0 0 l-1 -3 h3 Z" />
                <path class="hat thin" d="M-2 -27 v-4 q2.5 -3 5 0 v4 Z" />
              </g>
            </g>
          {:else if spec.prop === 'brush'}
            <g class="palette" transform="translate(34 96) rotate(-12)">
              <ellipse class="paper thin" rx="14" ry="8" />
              <ellipse class="pot thin" cx="7" cy="1" rx="3" ry="2.5" />
              <circle class="paint-food bare" cx="-7" cy="-1" r="2.5" />
              <circle class="pencil bare" cx="-1" cy="-3" r="2.5" />
            </g>
            <g class="easel" transition:fade={{ duration: motion ? 180 : 0 }}>
              <path class="none" d="M91 103 l-3 8 M108 103 l5 8 M101 102 v9" />
              <rect class="paper" x="86" y="65" width="29" height="36" rx="2" />
              <path class="paper-line" d="M84 102 h33" />
              <g class="painting">
                <ellipse
                  class="paint-plate thin"
                  cx="101"
                  cy="86"
                  rx="10"
                  ry="6"
                  opacity={Math.min(1, paint.current)}
                />
                <path
                  class="paint-food thin"
                  d="M94 86 q0 -8 6 -7 q7 -3 8 5 q-4 5 -14 2 Z"
                  opacity={Math.min(1, Math.max(0, paint.current - 1))}
                />
                <path
                  class="paint-detail none"
                  d="M97 81 l3 3 M103 80 l-2 5 M106 85 l-3 2"
                  opacity={Math.min(1, Math.max(0, paint.current - 2))}
                />
              </g>
            </g>
            <g class="handle handle-right"><path class="rim" d={rightHandle} /></g>
            <g class="brush" transform="translate({brushX.current} {brushY.current}) rotate(32)">
              <path class="pencil thin" d="M-2 -26 h4 v20 h-4 Z" />
              <path class="hat thin" d="M-2 -6 h4 v4 h-4 Z" />
              <path
                class="paint-food thin"
                class:warm={warmBrush}
                d="M-2 -2 q-3 3 2 6 q5 -3 2 -6 Z"
              />
            </g>
          {/if}

          {#if spec.prop === 'card'}
            <g
              transform="translate(60 98) rotate(-4)"
              transition:fade={{ duration: motion ? 150 : 0 }}
            >
              <rect class="paper" x="-17" y="-12" width="34" height="22" rx="2.5" />
              <path class="paper-line" d="M-11 -5 h22 M-11 0 h16 M-11 5 h19" />
            </g>
          {/if}
        </g>
      </g>

      <g opacity={steamOpacity} transform="translate(0 {steamRise})">
        {#if spec.steam === 'bulb'}
          <g transform="translate(103 23)">
            <path class="bulb thin" d="M-8 0 a8 8 0 1 1 16 0 q0 4 -4 7 v4 h-8 v-4 q-4 -3 -4 -7 Z" />
            <path class="none thin" d="M-3 14 h6 M0 6 v-6 M-3 -1 l3 3 3 -3" />
            <path
              class="bulb-rays none"
              d="M0 -15 v-4 M-13 -8 l-3 -2 M13 -8 l3 -2 M-14 4 h-4 M14 4 h4"
            />
          </g>
        {:else if spec.steam === 'wisp'}
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

<style>
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

  .phone {
    fill: var(--mascot-line);
  }
  .screen {
    fill: var(--mascot-hat);
  }
  .play,
  .pencil,
  .bulb {
    fill: var(--mascot-cheek);
  }
  .bulb-rays {
    stroke: var(--mascot-cheek);
  }

  .paint-plate {
    fill: var(--mascot-hat);
  }
  .paint-food.warm {
    fill: var(--mascot-cheek);
  }

  .paint-food {
    fill: var(--mascot-rim);
  }
  .paint-detail {
    stroke: var(--mascot-cheek);
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
