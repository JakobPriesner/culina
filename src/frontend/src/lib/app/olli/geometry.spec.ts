import { describe, expect, it } from 'vitest';
import { bodyTransform, brushGrip, eyeTransform, pencilGrip, rightHandlePath } from './geometry';

describe("Olli's geometry", () => {
  it('rotates and squashes the pot about its base', () => {
    expect(bodyTransform(2, 3, 1, 1.04, 0.94)).toBe(
      'translate(0 2) rotate(4 60 104) translate(60 104) scale(1.04 0.94) translate(-60 -104)'
    );
  });

  it('shuts an eye about its own centre', () => {
    expect(eyeTransform(50, 0.1)).toBe('translate(50 75) scale(1 0.1) translate(-50 -75)');
  });

  it('holds the pencil at its resting place and follows its lift', () => {
    const [x, y] = pencilGrip(3, 7, 0);
    expect(x).toBeCloseTo(57 + 12.9 * 0.9962 + 2 * 0.0872);
    expect(y).toBeCloseTo(96 - 12.9 * 0.0872 + 2 * 0.9962);

    const [, lifted] = pencilGrip(3, 7, 3);
    expect(lifted).toBeLessThan(y);
  });

  it('holds the brush above and beside its tip', () => {
    expect(brushGrip(100, 80)).toEqual([109, 66]);
  });

  it('draws a different handle for the brush and for everything else', () => {
    const brush = rightHandlePath(true, [109, 66]);
    const other = rightHandlePath(false, [109, 66]);

    expect(brush).toContain('M91 62 C101 62 109 63 109 62');
    expect(other).toContain('M91 62 C110 62 116 89 113 70');
  });
});
