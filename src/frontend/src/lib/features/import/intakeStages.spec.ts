import { describe, expect, it } from 'vitest';

import { poseFor, stepOf } from './intakeStages';

describe('stepOf', () => {
  it('is before the first stage while queued', () => {
    expect(stepOf('queued')).toBe(-1);
  });

  it('counts saving as writing', () => {
    expect(stepOf('saving')).toBe(stepOf('writing'));
  });

  it('is the position of a stage in the walk', () => {
    expect(stepOf('reading')).toBe(0);
    expect(stepOf('ready')).toBe(3);
  });
});

describe('poseFor', () => {
  it('has an idea when ready and is puzzled when failed', () => {
    expect(poseFor('ready')).toBe('idea');
    expect(poseFor('failed')).toBe('puzzled');
  });

  it('watches while there is nothing else to show', () => {
    expect(poseFor('queued')).toBe('watching');
    expect(poseFor(undefined)).toBe('watching');
  });
});
