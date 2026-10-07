import { describe, expect, it } from 'vitest';

import { clientError, ErrorCodes, type AppError } from '$api';

import { describeSave, type SaveFacts } from './describeSave';

const at = (facts: Partial<SaveFacts>) =>
  describeSave({
    failure: null,
    saving: false,
    saved: false,
    recovered: false,
    unsent: false,
    ...facts
  }).tone;

const conflict: AppError = { ...clientError(ErrorCodes.unexpected, 'x'), status: 409 };
const refused: AppError = { ...clientError(ErrorCodes.unexpected, 'x'), status: 500 };
const offline: AppError = clientError(ErrorCodes.offline, 'x');

describe('describeSave', () => {
  it('says the recipe saves itself at rest', () => {
    expect(at({})).toBe('idle');
  });

  it('says saved once a save has gone through', () => {
    expect(at({ saved: true })).toBe('saved');
  });

  it('never lets saving or saved mask a conflict', () => {
    expect(at({ failure: conflict, saving: true, saved: true })).toBe('conflict');
  });

  it('says saving while a save is in the air', () => {
    expect(at({ saving: true, unsent: true })).toBe('saving');
  });

  it('calls a refused save failed', () => {
    expect(at({ failure: refused })).toBe('failed');
  });

  it('reads a lost connection as where the work is, not as a failure', () => {
    expect(at({ failure: offline, unsent: true })).toBe('local');
  });

  it('never reads Saved for work that only exists on this device', () => {
    expect(at({ unsent: true, saved: true })).toBe('local');
    expect(at({ recovered: true, saved: true })).toBe('local');
  });
});
