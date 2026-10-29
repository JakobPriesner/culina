import { describe, expect, it } from 'vitest';

import { matchPlaces, type Place } from './places';

const places: Place[] = [
  {
    key: 'shopping',
    label: 'Einkauf',
    href: '/week/shopping',
    keywords: ['Einkaufsliste', 'Liste']
  },
  { key: 'plan', label: 'Woche', href: '/week/plan', keywords: ['Plan'] },
  { key: 'paste', label: 'Rezept einfügen', href: '/recipes/new?start=paste' },
  { key: 'item-1', label: 'Zucchini', detail: 'Auf der Einkaufsliste', href: '/week/shopping' }
];

const keys = (typed: string) => matchPlaces(typed, places).map((place) => place.key);

describe('places a typed word can mean', () => {
  it('finds a page by the start of its name or another word for it', () => {
    expect(keys('eink')).toEqual(['shopping']);
    expect(keys('liste')).toEqual(['shopping']);
  });

  it('needs every typed word, folded', () => {
    expect(keys('rezept einf')).toEqual(['paste']);
    expect(keys('rezept wo')).toEqual([]);
    expect(keys('EINFUGEN')).toEqual(['paste']);
  });

  it('finds what is on the list', () => {
    expect(keys('zucch')).toEqual(['item-1']);
  });

  it('says nothing for a single letter', () => {
    expect(keys('z')).toEqual([]);
  });
});
