/** Olli's geometry as SVG transforms and paths, avoiding cross-browser CSS transform-origin differences. */

export type Point = readonly [x: number, y: number];

export function bodyTransform(
  lift: number,
  tilt: number,
  lean: number,
  squashX: number,
  squashY: number
): string {
  return (
    `translate(0 ${lift}) rotate(${tilt + lean} 60 104) ` +
    `translate(60 104) scale(${squashX} ${squashY}) translate(-60 -104)`
  );
}

export function eyeTransform(x: number, lid: number): string {
  return `translate(${x} 75) scale(1 ${lid}) translate(${-x} -75)`;
}

/** Where the handle holds the pencil; paper and pencil rotate differently, so both apply to the grip. */
export function pencilGrip(pencilX: number, pencilY: number, penLift: number): Point {
  const localX = pencilX + 9.9;
  const localY = pencilY - penLift - 5;

  return [57 + localX * 0.9962 + localY * 0.0872, 96 - localX * 0.0872 + localY * 0.9962];
}

export function brushGrip(brushX: number, brushY: number): Point {
  return [brushX + 9, brushY - 14];
}

export function rightHandlePath(usingBrush: boolean, [handX, handY]: Point): string {
  return usingBrush
    ? `M91 62 C101 62 109 63 ${handX} ${handY - 4}
       Q${handX + 6} ${handY} ${handX} ${handY + 4}
       C105 70 102 72 91 71 Z`
    : `M91 62 C110 62 116 89 ${handX + 4} ${handY + 4}
       Q${handX - 2} ${handY + 7} ${handX - 3} ${handY + 1}
       Q${handX - 4} ${handY - 3} ${handX + 2} ${handY - 4}
       C102 82 102 74 91 71 Z`;
}
