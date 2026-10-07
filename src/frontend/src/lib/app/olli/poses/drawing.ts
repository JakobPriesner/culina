import { cubicOut, linear } from 'svelte/easing';

import type { Performance } from '../stage';

/** A full painting phrase; later cycles refine the painting rather than wiping it. */
export const drawing: Performance = ({ rig, later, blink, working }) => {
  const stroke = (at: number, x: number, y: number, progress: number) =>
    later(at, () => {
      rig.drawingPhase = 'painting';
      void rig.brushX.set(x, { duration: 550, easing: cubicOut });
      void rig.brushY.set(y, { duration: 550, easing: cubicOut });
      void rig.paint.set(Math.max(rig.paint.current, progress), { duration: 550, easing: linear });
      rig.lookX.target = 3;
      rig.lookY.target = 2;
    });
  const inspect = (at: number) =>
    later(at, () => {
      rig.drawingPhase = 'inspecting';
      void rig.brushX.set(111, { duration: 300, easing: cubicOut });
      void rig.brushY.set(70, { duration: 300, easing: cubicOut });
      rig.tilt.target = -4;
      rig.lookY.target = 0;
    });
  const colour = (at: number, warm: boolean) =>
    later(at, () => {
      rig.drawingPhase = 'choosing-colour';
      rig.warmBrush = warm;
      void rig.brushX.set(80, { duration: 400, easing: cubicOut });
      void rig.brushY.set(94, { duration: 400, easing: cubicOut });
      rig.lookX.target = -3;
      rig.lookY.target = 3;
      rig.tilt.target = -2;
    });

  colour(0, false);
  stroke(1200, 106, 75, 0.4);
  stroke(2100, 98, 83, 0.7);
  stroke(3000, 108, 87, 1);
  inspect(4100);

  if (!working) {
    stroke(3800, 108, 87, 3);
    return;
  }

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
};
