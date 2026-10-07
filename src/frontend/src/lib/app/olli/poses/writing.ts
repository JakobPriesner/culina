import { cubicOut, linear } from 'svelte/easing';

import type { Performance } from '../stage';

/** Three lines written one after the other, the pencil lifting between them. */
export const writing: Performance = ({ rig, later }) => {
  void rig.pencil.set(0, { duration: 0 });
  [0, 1, 2].forEach((line) => {
    const start = line * 1150;
    later(start, () => void rig.penLift.set(3, { duration: 80 }));
    later(start + 90, () => {
      void rig.pencilX.set(-15, { duration: 180, easing: cubicOut });
      void rig.pencilY.set(line * 6 - 5, { duration: 180, easing: cubicOut });
    });
    later(start + 280, () => void rig.penLift.set(0, { duration: 100 }));
    later(start + 400, () => {
      void rig.pencil.set(line + 1, { duration: 650, easing: linear });
      void rig.pencilX.set(line === 2 ? 3 : 14, { duration: 650, easing: linear });
    });
  });
  later(3400, () => void rig.penLift.set(2, { duration: 150 }));
  later(3650, () => (rig.lookY.target = 1));
};
