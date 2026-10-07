import { describe, expect, it } from 'vitest';

import { createSubmissionKey } from './submissionKey';

describe('a submission key', () => {
  it('is the same while the material is the same', () => {
    const key = createSubmissionKey();

    expect(key(['soup', 1])).toBe(key(['soup', 1]));
  });

  it('changes when the material does', () => {
    const key = createSubmissionKey();
    const first = key(['soup']);

    expect(key(['stew'])).not.toBe(first);
  });
});
