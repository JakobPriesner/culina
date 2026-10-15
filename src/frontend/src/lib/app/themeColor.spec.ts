import { beforeEach, describe, expect, it } from 'vitest';

import { followMode } from './themeColor';

/* A light app under a dark status bar, because the phone is dark and the cook chose light, reads as a broken page. */
const metas = () =>
  [...document.querySelectorAll<HTMLMetaElement>('meta[name="theme-color"]')].map((meta) => [
    meta.content,
    meta.getAttribute('media')
  ]);

beforeEach(() => {
  document.head.innerHTML =
    '<meta name="theme-color" content="#fdfbf7" media="(prefers-color-scheme: light)" />' +
    '<meta name="theme-color" content="#0d140d" media="(prefers-color-scheme: dark)" />';
});

describe('the colour around the page', () => {
  it('is the chosen mode, whatever the system says', () => {
    followMode('light');

    expect(metas()).toEqual([
      ['#fdfbf7', 'all'],
      ['#0d140d', 'not all']
    ]);
  });

  it('follows the system again once the choice is the system', () => {
    followMode('dark');
    followMode(null);

    expect(metas()).toEqual([
      ['#fdfbf7', '(prefers-color-scheme: light)'],
      ['#0d140d', '(prefers-color-scheme: dark)']
    ]);
  });
});
