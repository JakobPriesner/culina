import type { PoseSpec } from './poses';
import type { Rig } from './rig.svelte';

const finePointer = typeof matchMedia === 'function' && matchMedia('(pointer: fine)').matches;

/**
 * What a poke or a hovering pointer does to Olli. Nothing while it is still,
 * and nothing where somebody is stuck or refused.
 */
export function createTouch({
  rig,
  spec,
  motion,
  later,
  blink
}: {
  rig: Rig;
  spec: () => PoseSpec;
  motion: () => boolean;
  later: (ms: number, run: () => void) => unknown;
  blink: () => void;
}) {
  // Plain field, not state: bookkeeping that nothing renders.
  let taps: number[] = [];

  function poke(): void {
    if (!motion() || spec().sombre) {
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
    later(50, () => (rig.hat.target = spec().hatTilt + 4));
    later(200, () => (rig.hat.target = spec().hatTilt));
    blink();
  }

  function leanTo(towards: number): void {
    if (finePointer && motion() && !spec().sombre) {
      rig.lean.target = towards;
    }
  }

  return { poke, leanTo };
}
