import type { PoseSpec } from './poses';
import type { Rig } from './rig.svelte';

const finePointer = typeof matchMedia === 'function' && matchMedia('(pointer: fine)').matches;

/** A poke or hover reaction for Olli; ignored when motion is off or in a sombre pose. */
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
