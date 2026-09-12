import { getLocale, locales, setLocale, type Locale } from '$lib/paraglide/runtime';

/**
 * Everything the app needs to say something in the reader's language.
 *
 * Messages are compiled to functions, so a key that does not exist fails the
 * build instead of rendering an empty string in production. Nothing in a
 * component is ever a literal string a person can read.
 *
 * **Append new keys to `messages/*.json`; never re-sort the files.** The
 * compiler falls over with "No Lix transaction is active" on some reorderings,
 * and — worse — it can emit correct output *and* exit non-zero, so a working
 * app is not evidence that the build passed. Appending has always worked.
 */
export { m } from '$lib/paraglide/messages';
export { locales, type Locale };

export const isLocale = (value: unknown): value is Locale =>
  typeof value === 'string' && (locales as readonly string[]).includes(value);

/**
 * The language before a session is known: the last choice on this device, then
 * the browser's own preference, then English. A signed-in person's server
 * setting replaces it as soon as it arrives.
 */
export const detectLocale = (): Locale => getLocale();

/** Switches language without a reload, and tells assistive technology. */
export function applyLocale(locale: Locale): void {
  setLocale(locale, { reload: false });

  const root = globalThis.document?.documentElement;

  if (root) {
    root.lang = locale;
  }
}

/**
 * Intl formatters, made once per locale.
 *
 * Constructing one is expensive enough that doing it inside a list of two
 * hundred ingredient rows is measurable, and the result is immutable, so it is
 * cached.
 */
const numberFormats = new Map<string, Intl.NumberFormat>();
const dateFormats = new Map<string, Intl.DateTimeFormat>();

/** `1.5` in English, `1,5` in German — the same number, read correctly. */
export function formatNumber(value: number, options: Intl.NumberFormatOptions = {}): string {
  return formatter(
    numberFormats,
    options,
    (locale) => new Intl.NumberFormat(locale, options)
  ).format(value);
}

export function formatDate(value: Date, options: Intl.DateTimeFormatOptions = {}): string {
  return formatter(
    dateFormats,
    options,
    (locale) => new Intl.DateTimeFormat(locale, options)
  ).format(value);
}

function formatter<TFormat>(
  cache: Map<string, TFormat>,
  options: object,
  create: (locale: string) => TFormat
): TFormat {
  const locale = getLocale();
  const key = `${locale}:${JSON.stringify(options)}`;
  const existing = cache.get(key);

  if (existing) {
    return existing;
  }

  const made = create(locale);

  cache.set(key, made);

  return made;
}
