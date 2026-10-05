import { describe, expect, it } from 'vitest';

import { idleBlinks, poses, restAfter } from './poses';

describe("Olli's idle blinks", () => {
  it.each([0, 0.3, 0.59, 0.61, 0.99])(
    'are all over before the rest, however the dice fall (%s)',
    (n) => {
      const blinks = idleBlinks(() => n);

      expect(blinks.length).toBeGreaterThan(0);
      expect(Math.min(...blinks)).toBeGreaterThanOrEqual(1500);
      expect(Math.max(...blinks)).toBeLessThan(restAfter);
    }
  );

  it('never blinks twice in quick succession', () => {
    const [first, second] = idleBlinks(() => 0.5);

    expect(second).toBeDefined();
    expect(second! - first!).toBeGreaterThanOrEqual(600);
  });
});

describe("Olli's poses", () => {
  it('stay calm where somebody is stuck or refused', () => {
    expect(poses.unplugged.sombre).toBe(true);
    expect(poses.onDuty.sombre).toBe(true);
    expect(poses.dozing.sombre).toBe(true);
  });

  it('keep every raised handle in front of the rim', () => {
    for (const pose of Object.values(poses)) {
      expect(Math.max(...pose.arms)).toBeLessThanOrEqual(60);
    }
  });

  it('let no steam rise from a pot that is unplugged', () => {
    expect(poses.unplugged.steam).toBe('none');
  });
});
