import { readFile } from 'node:fs/promises';
import { describe, expect, it } from 'vitest';

import { locales } from './i18n';

/*
 * A message that exists in one language and not the other is not a compile
 * error — Paraglide falls back to the base locale — so the reader simply gets
 * English in the middle of a German page. That is what this catches.
 */
const load = async (locale: string): Promise<Record<string, string>> => {
  const contents: unknown = JSON.parse(await readFile(`messages/${locale}.json`, 'utf8'));
  const { $schema: _schema, ...messages } = contents as Record<string, string>;

  return messages;
};

const catalogues = Object.fromEntries(
  await Promise.all(locales.map(async (locale) => [locale, await load(locale)] as const))
) as Record<string, Record<string, string>>;

const base = locales[0]!;

describe('the message catalogues', () => {
  it('are not empty', () => {
    expect(Object.keys(catalogues[base]!).length).toBeGreaterThan(0);
  });

  it.each(locales.filter((locale) => locale !== base))(
    'give %s exactly the keys the base locale has',
    (locale) => {
      expect(Object.keys(catalogues[locale]!).sort()).toEqual(
        Object.keys(catalogues[base]!).sort()
      );
    }
  );

  it.each(locales)('leave no message in %s empty', (locale) => {
    const blank = Object.entries(catalogues[locale]!)
      .filter(([, value]) => value.trim() === '')
      .map(([key]) => key);

    expect(blank).toEqual([]);
  });

  it.each(locales.filter((locale) => locale !== base))(
    'interpolate the same placeholders in %s as in the base locale',
    (locale) => {
      const placeholders = (value: string) =>
        [...value.matchAll(/\{(\w+)\}/g)].map(([, name]) => name).sort();

      for (const [key, value] of Object.entries(catalogues[base]!)) {
        expect(placeholders(catalogues[locale]![key] ?? ''), key).toEqual(placeholders(value));
      }
    }
  );

  it('namespaces every key by feature', () => {
    const unnamespaced = Object.keys(catalogues[base]!).filter((key) => !key.includes('.'));

    expect(unnamespaced, 'a key like `title` collides the moment a second page wants one').toEqual(
      []
    );
  });
});
