import { afterEach, describe, expect, it } from 'vitest';

import { applyLocale } from '$shell/i18n';

import { addDays, dayLabel, kcalText, portionsText } from './format';

describe('diary words', () => {
  afterEach(() => applyLocale('en'));

  it('says portions as a cook does', () => {
    expect(portionsText(1)).toBe('1');
    expect(portionsText(0.5)).toBe('½');
    expect(portionsText(1.5)).toBe('1½');
    expect(portionsText(2.25)).toBe('2¼');
  });

  it('marks a lower bound and an estimate in words, a lower bound rounded down', () => {
    applyLocale('de');

    expect(kcalText(520.9, { atLeast: true, estimated: false })).toBe('mind. 520 kcal');
    expect(kcalText(520.4, { atLeast: false, estimated: true })).toBe('ca. 520 kcal');
    expect(kcalText(520.5, { atLeast: false, estimated: false })).toBe('521 kcal');
  });

  it('counts days across a month and names the near ones', () => {
    applyLocale('de');

    expect(addDays('2026-10-31', 1)).toBe('2026-11-01');
    expect(addDays('2026-03-01', -1)).toBe('2026-02-28');
    expect(dayLabel('2026-10-22', '2026-10-22')).toBe('Heute');
    expect(dayLabel('2026-10-21', '2026-10-22')).toBe('Gestern');
  });
});
