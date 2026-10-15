import { describe, expect, it } from 'vitest';

import { asSentence, spokenText } from './spokenText';

describe('fractions', () => {
  it('writes a lone fraction glyph as words', () => {
    expect(spokenText('Add ½ onion', 'en')).toBe('Add one half onion');
    expect(spokenText('Add ¾ cup', 'en')).toBe('Add three quarters cup');
  });

  it('joins a whole number to its fraction', () => {
    expect(spokenText('1½ cups', 'en')).toBe('1 and one half cups');
    expect(spokenText('2 ½ Zwiebeln', 'de')).toBe('2 und ein halb Zwiebeln');
  });

  it('reads slash fractions that a person would say, and leaves other numbers alone', () => {
    expect(spokenText('1/2 teaspoon salt', 'en')).toBe('one half teaspoon salt');
    expect(spokenText('1 1/2 hours', 'en')).toBe('1 and one half hours');
    expect(spokenText('Serves 3/11 people', 'en')).toBe('Serves 3/11 people');
    expect(spokenText('Made on 12/10/2026', 'en')).toBe('Made on 12/10/2026');
  });

  it('uses the language of the recipe', () => {
    expect(spokenText('¼ l Milch', 'de')).toBe('ein Viertel Liter Milch');
  });
});

describe('units', () => {
  it('spells out spoons, singular for one and plural otherwise', () => {
    expect(spokenText('1 tbsp oil, 2 tbsp butter, 1 tsp salt', 'en')).toBe(
      '1 tablespoon oil, 2 tablespoons butter, 1 teaspoon salt'
    );
  });

  it('takes a fraction before the unit as one', () => {
    expect(spokenText('½ tsp salt', 'en')).toBe('one half teaspoon salt');
    expect(spokenText('1½ tbsp honey', 'en')).toBe('1 and one half tablespoons honey');
  });

  it('spells out metric units after a number, with or without a space', () => {
    expect(spokenText('250 g flour, 1,5 kg potatoes, 200ml milk, 2 l water', 'en')).toBe(
      '250 grams flour, 1,5 kilograms potatoes, 200 milliliters milk, 2 liters water'
    );
  });

  it('spells out German abbreviations', () => {
    expect(spokenText('2 EL Öl, 1 TL Salz, 250 g Mehl, 10 Min.', 'de')).toBe(
      '2 Esslöffel Öl, 1 Teelöffel Salz, 250 Gramm Mehl, 10 Minuten.'
    );
  });

  it('spells out times', () => {
    expect(spokenText('Bake 20 min, rest 1 h', 'en')).toBe('Bake 20 minutes, rest 1 hour');
  });

  it('leaves letters that only look like units', () => {
    expect(spokenText('Add 2 gloves of garlic and a g-string', 'en')).toBe(
      'Add 2 gloves of garlic and a g-string'
    );
    expect(spokenText('Take 3 large eggs', 'en')).toBe('Take 3 large eggs');
    expect(spokenText('Use the g button', 'en')).toBe('Use the g button');
  });
});

describe('signs', () => {
  it('speaks temperatures', () => {
    expect(spokenText('Heat to 180°C', 'en')).toBe('Heat to 180 degrees Celsius');
    expect(spokenText('Heat to 350 °F', 'en')).toBe('Heat to 350 degrees Fahrenheit');
    expect(spokenText('Auf 180 °C vorheizen', 'de')).toBe('Auf 180 Grad Celsius vorheizen');
  });

  it('speaks ranges, percentages, approximations and ampersands', () => {
    expect(spokenText('Cook 5–7 min', 'en')).toBe('Cook 5 to 7 minutes');
    expect(spokenText('Cook 5-7 Min', 'de')).toBe('Cook 5 bis 7 Minuten');
    expect(spokenText('Cream with 35% fat', 'en')).toBe('Cream with 35 percent fat');
    expect(spokenText('~200 g', 'en')).toBe('about 200 grams');
    expect(spokenText('Salt & pepper', 'de')).toBe('Salt und pepper');
  });

  it('does not turn dates and phone-like digits into ranges', () => {
    expect(spokenText('Since 2026-10-15', 'en')).toBe('Since 2026-10-15');
  });

  it('turns a spaced dash into a pause', () => {
    expect(spokenText('Stir – then wait', 'en')).toBe('Stir, then wait');
  });
});

describe('markdown', () => {
  it('drops emphasis, code, links, headings and addresses', () => {
    expect(spokenText('**Melt** the _butter_ and `stir`', 'en')).toBe('Melt the butter and stir');
    expect(spokenText('See [the guide](https://example.com/x) now', 'en')).toBe(
      'See the guide now'
    );
    expect(spokenText('More at https://example.com/x today', 'en')).toBe('More at today');
    expect(spokenText('## Sauce', 'en')).toBe('Sauce');
  });

  it('drops pictures', () => {
    expect(spokenText('Serve 🍝 hot', 'en')).toBe('Serve hot');
  });
});

describe('asSentence', () => {
  it('ends a sentence unless it already ends', () => {
    expect(asSentence('Stir well')).toBe('Stir well.');
    expect(asSentence('Stir well!')).toBe('Stir well!');
    expect(asSentence('  ')).toBe('');
  });
});
