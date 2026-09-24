import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

import { afterEach, describe, expect, it } from 'vitest';

import { clientError, ErrorCodes } from '$api';

import en from '../../../messages/en.json';
import de from '../../../messages/de.json';

import { explain } from './explain';
import { applyLocale } from './i18n';

const backend = join(process.cwd(), '../backend/src');

/** `new(` or `new FieldError(`, an optional field, then the code and its description. */
const errorCode = /new(?: \w*Error)?\(\s*(?:(?:"[^"]*"|\w+),\s*)?"([a-z]+\.[a-z_]+)",\s*\$?"/g;

/** Every code the API can answer with, read from its source. */
function serverCodes(): string[] {
  const codes = new Set<string>();

  for (const file of readdirSync(backend, { recursive: true, encoding: 'utf8' })) {
    if (!file.endsWith('.cs') || /[/\\](bin|obj)[/\\]/.test(file)) {
      continue;
    }

    const source = readFileSync(join(backend, file), 'utf8');

    for (const [, code] of source.matchAll(errorCode)) {
      codes.add(code!);
    }
  }

  return [...codes].sort();
}

describe('explaining a failure', () => {
  afterEach(() => applyLocale('en'));

  it('has something to say, in every language, for every code the server sends', () => {
    const codes = serverCodes();

    // A guard that the scan itself still finds the catalogue.
    expect(codes).toContain('cookbooks.invalid_name');
    expect(codes.length).toBeGreaterThan(100);

    for (const catalogue of [en, de] as Record<string, unknown>[]) {
      expect(codes.filter((code) => !(`problem.${code}` in catalogue))).toEqual([]);
    }
  });

  it('says what the server said in the reader’s language, not the server’s', () => {
    const failure = clientError(
      'households.not_owner',
      'Only an owner of this household can do that.'
    );

    applyLocale('de');

    expect(explain(failure)).toBe('Das kann nur ein Eigentümer dieses Haushalts.');
  });

  it('says a failure on one field the same way as one on the whole form', () => {
    applyLocale('de');

    expect(explain({ code: 'users.weak_password', detail: 'At least 12.' })).toBe(
      'Ein Passwort muss mindestens 12 Zeichen haben.'
    );
  });

  it('translates the failures the client invents for itself', () => {
    applyLocale('de');

    expect(explain(clientError(ErrorCodes.offline, 'You appear to be offline.'))).toMatch(
      /Du bist offline/
    );
  });

  it('falls back to the server’s words for a code newer than this build', () => {
    expect(explain(clientError('pantry.out_of_flour', 'The flour ran out.'))).toBe(
      'The flour ran out.'
    );
  });
});
