import { describe, expect, it } from 'vitest';

import { durationsIn } from './stepDurations';

const minutes = (...values: number[]) => values.map((value) => value * 60);

describe('the waits a step names', () => {
  it('finds each wait in the order written, a range from its start', () => {
    const chili =
      'Fleisch in je 2 El heißem Öl rundherum 5–10 Min. scharf anbraten. 1 El Öl zugeben und 4 Min. braten. ' +
      'Zugedeckt bei mittlerer Hitze 11⁄2 Std. garen.';

    expect(durationsIn(chili)).toEqual(minutes(5, 4, 90));
  });

  it.each([
    ['20 Minuten backen', 20],
    ['20-25 Min backen', 20],
    ['10 bis 15 Minuten ziehen lassen', 10],
    ['1 Stunde gehen lassen', 60],
    ['1,5 Stunden schmoren', 90],
    ['1½ Std. kochen', 90],
    ['½ Stunde ruhen lassen', 30],
    ['1 1/2 hours', 90],
    ['simmer for 25 mins', 25],
    ['bake 40 minutes', 40],
    ['rest 2 hrs', 120],
    ['about 1h', 60]
  ])('reads "%s" as %i minutes', (text, expected) => {
    expect(durationsIn(text)).toEqual(minutes(expected));
  });

  it('names each wait once', () => {
    expect(durationsIn('5 Min. rühren, dann nochmals 5 Minuten')).toEqual(minutes(5));
  });

  it('leaves lengths, temperatures, counts and words that only start like a unit', () => {
    expect(
      durationsIn('11⁄2 cm groß würfeln, bei 180 Grad, 2 Minzblätter, 3 Mineralwasser, 4 h-Milch')
    ).toEqual([]);
  });

  it('offers nothing shorter than a minute or longer than a day', () => {
    expect(durationsIn('0,5 Min. 30 Std.')).toEqual([]);
  });
});
