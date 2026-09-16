import { readFileSync } from 'node:fs';
import { join } from 'node:path';

import { describe, expect, it } from 'vitest';

/*
 * The document is the one file no framework checks and no component test
 * renders, and it is the first thing anybody sees. Both of the rules below were
 * broken in a version that shipped: the boot screen was styled with a style
 * attribute, which the production policy blocks outright, so what people opened
 * the app to was the word "Culina" in the corner of a white page — while the
 * dev server, which sets no policy, looked perfect.
 */
// Read from the project root rather than relative to this module: under jsdom
// `import.meta.url` is not a file URL.
const document = readFileSync(join(process.cwd(), 'src/app.html'), 'utf8');

describe('the document', () => {
  it('styles nothing with an attribute, because the policy blocks every one', () => {
    // `style-src 'self' 'nonce-…'` allows a <style> element carrying the
    // nonce. A nonce cannot be attached to an attribute, so a style attribute
    // is blocked however the policy is written.
    expect(document).not.toMatch(/\sstyle="/);
  });

  it('nonces both inline blocks, so the backend can substitute them', () => {
    const nonces = document.match(/nonce="__CULINA_NONCE__"/g) ?? [];

    // One for the script that stamps the theme before first paint, one for the
    // boot screen's styles.
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
    // Chosen before the first paint from what this device saw last time.
    expect(document).toMatch(/culina\.boot/);
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
