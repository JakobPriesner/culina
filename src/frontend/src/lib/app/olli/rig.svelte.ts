import { Spring, Tween } from 'svelte/motion';

import type { PoseSpec } from './poses';

const firm = { stiffness: 0.18, damping: 0.55 };
const bouncy = { stiffness: 0.25, damping: 0.45 };

export type DrawingPhase = 'inspecting' | 'painting' | 'choosing-colour' | 'still';

/** Everything about Olli that moves, each on its own spring or tween; a pose is only a set of targets, so any pose can follow any other. */
export function createRig(start: PoseSpec, still: boolean) {
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
  const shown = new Tween(still ? 1 : 0);
  const steam = new Tween(0);
  const pencil = new Tween(0);
  const pencilX = new Tween(-15);
  const pencilY = new Tween(-5);
  const penLift = new Tween(0);
  const brushX = new Tween(97);
  const brushY = new Tween(78);
  const paint = new Tween(0);
  let drawingPhase = $state<DrawingPhase>('inspecting');
  let warmBrush = $state(false);

  return {
    tilt,
    lean,
    lift,
    squashX,
    squashY,
    armL,
    armR,
    hat,
    lookX,
    lookY,
    lid,
    shown,
    steam,
    pencil,
    pencilX,
    pencilY,
    penLift,
    brushX,
    brushY,
    paint,
    get drawingPhase() {
      return drawingPhase;
    },
    set drawingPhase(phase: DrawingPhase) {
      drawingPhase = phase;
    },
    get warmBrush() {
      return warmBrush;
    },
    set warmBrush(warm: boolean) {
      warmBrush = warm;
    },
    squash(x: number, y: number): void {
      squashX.target = x;
      squashY.target = y;
    }
  };
}

export type Rig = ReturnType<typeof createRig>;
