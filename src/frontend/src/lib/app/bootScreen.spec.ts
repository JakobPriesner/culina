import { readFileSync } from 'node:fs';
import { join } from 'node:path';

import { describe, expect, it } from 'vitest';

/* No framework checks the document yet it is the first thing seen: the production policy blocks style attributes (dev sets none), which once shipped a white page. */
// Read from the project root rather than relative to this module: under jsdom
// `import.meta.url` is not a file URL.
const document = readFileSync(join(process.cwd(), 'src/app.html'), 'utf8');

describe('the document', () => {
  it('styles nothing with an attribute, because the policy blocks every one', () => {
    // A nonce cannot be attached to a style attribute, so it is blocked however the policy is written.
    expect(document).not.toMatch(/\sstyle="/);
  });

  it('nonces both inline blocks, so the backend can substitute them', () => {
    const nonces = document.match(/nonce="__CULINA_NONCE__"/g) ?? [];

    // One for the theme script, one for the boot screen's styles.
    expect(nonces).toHaveLength(2);
  });
});

describe('the boot screen', () => {
  it('is a skeleton of the page, not a word on a background', () => {
    expect(document).toMatch(/id="boot"/);
    expect(document.match(/class="boot-block/g)?.length ?? 0).toBeGreaterThan(3);
  });

  it('holds both shapes, because the document cannot know which one is coming', () => {
    expect(document).toMatch(/boot-library/);
    expect(document).toMatch(/boot-signin/);
    expect(document).toMatch(/culina\.boot/);
  });

  it('picks the sign-in skeleton by address, ahead of the stored guess', () => {
    const script =
      document.match(/<script nonce="__CULINA_NONCE__">([\s\S]*?)<\/script>/)?.[1] ?? '';

    const bootFor = (path: string, stored: string | null) => {
      const root = { dataset: {} as Record<string, string> };
      const storage = { getItem: (key: string) => (key === 'culina.boot' ? stored : null) };

      new Function('document', 'localStorage', 'matchMedia', 'location', script)(
        { documentElement: root },
        storage,
        () => ({ matches: false }),
        { pathname: path }
      );

      return root.dataset.boot;
    };

    for (const path of ['/login', '/register', '/setup', '/join/abc123', '/password-reset']) {
      expect(bootFor(path, 'app')).toBe('auth');
      expect(bootFor(path, null)).toBe('auth');
    }
    expect(bootFor('/', null)).toBe('app');
    expect(bootFor('/', 'auth')).toBe('auth');
    expect(bootFor('/recipes/login', 'app')).toBe('app');
    expect(bootFor('/loginx', null)).toBe('app');
  });

  it('waits before it appears, so a fast boot never flashes a skeleton', () => {
    // The same delay every other loading state in the app uses.
    expect(document).toMatch(/animation: boot-in [\d]+ms [^;]*150ms/);
  });

  it('announces itself once rather than as a list of empty boxes', () => {
    expect(document).toMatch(/role="status"/);
    expect(document.match(/aria-hidden="true"/g)?.length ?? 0).toBeGreaterThan(2);
  });
});
