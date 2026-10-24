import { afterEach, describe, expect, it } from 'vitest';

import { applyLocale } from '$shell/i18n';

import {
  addDays,
  amountText,
  dayDateLine,
  dayLabel,
  dayTitle,
  kcalText,
  mondayOfDate,
  portionsText,
  precisionMark,
  precisionOf,
  precisionWord
} from './format';
import type { DiaryEntry } from './types';

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

  it('titles the day by how near it is, and dates it without saying the weekday twice', () => {
    applyLocale('de');

    expect(dayTitle('2026-10-24', '2026-10-24')).toBe('Heute');
    expect(dayDateLine('2026-10-24', '2026-10-24')).toBe('Samstag, 24. Oktober');
    expect(dayTitle('2026-10-23', '2026-10-24')).toBe('Gestern');
    expect(dayTitle('2026-10-21', '2026-10-24')).toBe('Mittwoch');
    expect(dayDateLine('2026-10-21', '2026-10-24')).toBe('21. Oktober');
    expect(mondayOfDate('2026-10-25')).toBe('2026-10-19');
  });

  it('marks how far to trust a figure, and says the mark as a word', () => {
    applyLocale('de');

    expect(precisionMark[precisionOf({ atLeast: true, estimated: true })]).toBe('≥');
    expect(precisionMark[precisionOf({ atLeast: false, estimated: true })]).toBe('≈');
    expect(precisionMark[precisionOf({ atLeast: false, estimated: false })]).toBe('');
    expect(precisionWord('estimated')).toBe('etwa');
    expect(precisionWord('atLeast')).toBe('mindestens');
  });

  it('leaves out the grams of a counted food when they are not known', () => {
    applyLocale('de');
    const apple = {
      entryId: 'e',
      date: '2026-10-24',
      meal: 'snack',
      source: 'food',
      label: 'Apfel',
      amount: 1,
      unit: 'piece',
      figures: { kcal: 72 },
      atLeast: false,
      estimated: true,
      version: 1
    } satisfies DiaryEntry;

    expect(amountText({ ...apple, grams: null })).toBe('1 Stück');
    expect(amountText({ ...apple, grams: 135 })).toBe('1 Stück · ca. 135 g');
  });
});
