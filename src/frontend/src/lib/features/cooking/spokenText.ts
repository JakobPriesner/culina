import type { Locale } from '$shell/i18n';

/**
 * Turns a recipe step into what a speech engine should say. Engines read "½ tbsp" as symbols and letters, so fractions, units and signs are written out in words, per language.
 * Keyed by `Locale`, so a language added to the app fails the build here until it has its words.
 */
interface Words {
  /** Joins a whole number to its fraction: "1 and one half". */
  readonly and: string;
  readonly ampersand: string;
  readonly percent: string;
  readonly approximately: string;
  /** Between the ends of a range: "5 to 7". */
  readonly to: string;
  readonly degrees: string;
  readonly celsius: string;
  readonly fahrenheit: string;
  /** Spelled as the language says them, keyed by numerator then denominator. */
  readonly fractions: Readonly<Record<string, string>>;
  /** Written unit (lower case, without a trailing dot) to its singular and plural. */
  readonly units: Readonly<Record<string, readonly [one: string, other: string]>>;
}

const words: Record<Locale, Words> = {
  en: {
    and: 'and',
    ampersand: 'and',
    percent: 'percent',
    approximately: 'about',
    to: 'to',
    degrees: 'degrees',
    celsius: 'Celsius',
    fahrenheit: 'Fahrenheit',
    fractions: {
      '1/2': 'one half',
      '1/3': 'one third',
      '2/3': 'two thirds',
      '1/4': 'one quarter',
      '3/4': 'three quarters',
      '1/5': 'one fifth',
      '2/5': 'two fifths',
      '3/5': 'three fifths',
      '4/5': 'four fifths',
      '1/6': 'one sixth',
      '5/6': 'five sixths',
      '1/8': 'one eighth',
      '3/8': 'three eighths',
      '5/8': 'five eighths',
      '7/8': 'seven eighths'
    },
    units: {
      tbsp: ['tablespoon', 'tablespoons'],
      tbs: ['tablespoon', 'tablespoons'],
      tsp: ['teaspoon', 'teaspoons'],
      g: ['gram', 'grams'],
      kg: ['kilogram', 'kilograms'],
      mg: ['milligram', 'milligrams'],
      ml: ['milliliter', 'milliliters'],
      cl: ['centiliter', 'centiliters'],
      dl: ['deciliter', 'deciliters'],
      l: ['liter', 'liters'],
      oz: ['ounce', 'ounces'],
      'fl oz': ['fluid ounce', 'fluid ounces'],
      lb: ['pound', 'pounds'],
      lbs: ['pound', 'pounds'],
      min: ['minute', 'minutes'],
      mins: ['minute', 'minutes'],
      h: ['hour', 'hours'],
      hr: ['hour', 'hours'],
      hrs: ['hour', 'hours'],
      sec: ['second', 'seconds'],
      secs: ['second', 'seconds']
    }
  },
  de: {
    and: 'und',
    ampersand: 'und',
    percent: 'Prozent',
    approximately: 'etwa',
    to: 'bis',
    degrees: 'Grad',
    celsius: 'Celsius',
    fahrenheit: 'Fahrenheit',
    fractions: {
      '1/2': 'ein halb',
      '1/3': 'ein Drittel',
      '2/3': 'zwei Drittel',
      '1/4': 'ein Viertel',
      '3/4': 'drei Viertel',
      '1/5': 'ein Fünftel',
      '2/5': 'zwei Fünftel',
      '3/5': 'drei Fünftel',
      '4/5': 'vier Fünftel',
      '1/6': 'ein Sechstel',
      '5/6': 'fünf Sechstel',
      '1/8': 'ein Achtel',
      '3/8': 'drei Achtel',
      '5/8': 'fünf Achtel',
      '7/8': 'sieben Achtel'
    },
    units: {
      el: ['Esslöffel', 'Esslöffel'],
      tl: ['Teelöffel', 'Teelöffel'],
      tbsp: ['Esslöffel', 'Esslöffel'],
      tsp: ['Teelöffel', 'Teelöffel'],
      g: ['Gramm', 'Gramm'],
      kg: ['Kilogramm', 'Kilogramm'],
      mg: ['Milligramm', 'Milligramm'],
      ml: ['Milliliter', 'Milliliter'],
      cl: ['Zentiliter', 'Zentiliter'],
      dl: ['Deziliter', 'Deziliter'],
      l: ['Liter', 'Liter'],
      oz: ['Unze', 'Unzen'],
      'fl oz': ['Flüssigunze', 'Flüssigunzen'],
      lb: ['Pfund', 'Pfund'],
      lbs: ['Pfund', 'Pfund'],
      min: ['Minute', 'Minuten'],
      h: ['Stunde', 'Stunden'],
      std: ['Stunde', 'Stunden'],
      sek: ['Sekunde', 'Sekunden']
    }
  }
};

/** Unicode fraction glyphs, as numerator/denominator. */
const glyphs: Readonly<Record<string, string>> = {
  '½': '1/2',
  '⅓': '1/3',
  '⅔': '2/3',
  '¼': '1/4',
  '¾': '3/4',
  '⅕': '1/5',
  '⅖': '2/5',
  '⅗': '3/5',
  '⅘': '4/5',
  '⅙': '1/6',
  '⅚': '5/6',
  '⅛': '1/8',
  '⅜': '3/8',
  '⅝': '5/8',
  '⅞': '7/8'
};

const glyphClass = `[${Object.keys(glyphs).join('')}]`;

/** A number as written in a step: `1`, `2.5`, `2,5`, `1½`, `½`. */
const amount = `(?:\\d+(?:[.,]\\d+)?\\s?${glyphClass}?|${glyphClass})`;

const escape = (text: string) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/** Longest first, so `fl oz` wins over `oz` and `kg` over `g`. */
const unitPattern = (locale: Locale) =>
  Object.keys(words[locale].units)
    .sort((a, b) => b.length - a.length)
    .map(escape)
    .join('|');

const unitMatchers = new Map<Locale, RegExp>();

function unitMatcher(locale: Locale): RegExp {
  let matcher = unitMatchers.get(locale);

  if (!matcher) {
    // A number (or fraction), optional space, then a known unit that is not the start of a longer word.
    matcher = new RegExp(`(${amount})\\s?(${unitPattern(locale)})(?![\\p{L}\\p{N}])`, 'giu');
    unitMatchers.set(locale, matcher);
  }

  return matcher;
}

/** Spoons and the like written alone after a number or a fraction. */
function expandUnits(text: string, locale: Locale): string {
  const spoken = words[locale];

  return text.replace(unitMatcher(locale), (match, quantity: string, written: string) => {
    const entry = spoken.units[written.toLowerCase()];

    if (!entry) {
      return match;
    }

    // Singular for one and for a lone fraction ("one half teaspoon"); 1.5 and 2 take the plural.
    const count = quantity.trim();
    const name = count === '1' || count in glyphs ? entry[0] : entry[1];

    return `${quantity.trim()} ${name}`;
  });
}

/** `1½` and `½` as words: "1 and one half" ("1 und ein halb"). */
function expandGlyphs(text: string, locale: Locale): string {
  const spoken = words[locale];

  return text.replace(
    new RegExp(`(?:(?<![\\d.,])(\\d+)\\s?)?(${glyphClass})`, 'gu'),
    (_, whole, glyph: string) => {
      const fraction = spoken.fractions[glyphs[glyph]!] ?? glyph;

      return whole ? `${whole} ${spoken.and} ${fraction}` : fraction;
    }
  );
}

/** `1/2` and `1 1/2`, only where the fraction is one a person would say; `3/12` and dates are left alone. */
function expandSlashes(text: string, locale: Locale): string {
  const spoken = words[locale];

  return text.replace(
    /(?<![\d/.,])(?:(\d+)\s)?(\d{1,2})\/(\d{1,2})(?![\d/])/g,
    (match, whole: string | undefined, top: string, bottom: string) => {
      const fraction = spoken.fractions[`${top}/${bottom}`];

      if (!fraction) {
        return match;
      }

      return whole ? `${whole} ${spoken.and} ${fraction}` : fraction;
    }
  );
}

function expandSigns(text: string, locale: Locale): string {
  const spoken = words[locale];

  return (
    text
      // 180°C, 180 ° C, 350 °F and a bare °.
      .replace(/\s?°\s?C\b/g, ` ${spoken.degrees} ${spoken.celsius}`)
      .replace(/\s?°\s?F\b/g, ` ${spoken.degrees} ${spoken.fahrenheit}`)
      .replace(/\s?°/g, ` ${spoken.degrees}`)
      .replace(/\s?%/g, ` ${spoken.percent}`)
      // Ranges between two numbers: 5–7, 5-7.
      .replace(/(?<![\d–-])(\d{1,3})\s?[–-]\s?(?=\d{1,3}(?![\d–-]))/g, `$1 ${spoken.to} `)
      .replace(/[~≈]\s?/g, `${spoken.approximately} `)
      .replace(/\s&\s/g, ` ${spoken.ampersand} `)
      // A dash between words is a pause.
      .replace(/\s[–—]\s/g, ', ')
  );
}

/** What Markdown leaves behind in a step after it was parsed: emphasis marks, code ticks, link syntax, bare addresses and pictures. */
function stripMarkup(text: string): string {
  return text
    .replace(/!\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/https?:\/\/\S+/g, '')
    .replace(/[*_`]+|~~/g, '')
    .replace(/^\s{0,3}(?:#{1,6}|>)\s+/gm, '')
    .replace(/\p{Extended_Pictographic}️?/gu, '');
}

/** Words in the language's own order for one chunk of text; callers join the chunks into sentences. */
export function spokenText(text: string, locale: Locale): string {
  const withUnits = expandUnits(stripMarkup(text), locale);
  const withFractions = expandSlashes(expandGlyphs(withUnits, locale), locale);

  return expandSigns(withFractions, locale)
    .replace(/\s+/g, ' ')
    .replace(/\s+([,.;:!?])/g, '$1')
    .trim();
}

const sentenceEnd = /[.!?…:;]$/;

/** A heading, list item or paragraph as one sentence, so the engine pauses between them. */
export function asSentence(text: string): string {
  const trimmed = text.trim();

  return !trimmed || sentenceEnd.test(trimmed) ? trimmed : `${trimmed}.`;
}
