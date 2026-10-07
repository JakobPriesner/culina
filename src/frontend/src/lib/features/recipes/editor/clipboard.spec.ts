import { afterEach, describe, expect, it, vi } from 'vitest';

import { readClipboardRecipe } from './clipboard';

function clipboardHolds(read: () => Promise<string>) {
  Object.defineProperty(navigator, 'clipboard', {
    configurable: true,
    value: { readText: read }
  });
}

afterEach(() => {
  Reflect.deleteProperty(navigator, 'clipboard');
});

describe('reading a recipe off the clipboard', () => {
  it('finds the address in what was copied', async () => {
    clipboardHolds(() => Promise.resolve('  Look: https://example.test/soup now  '));

    // The capability is decided when the module loads, so read it afresh.
    vi.resetModules();
    const { readClipboardRecipe: read } = await import('./clipboard');

    expect(await read()).toEqual({
      text: 'Look: https://example.test/soup now',
      url: 'https://example.test/soup'
    });
  });

  it('gives only the text when there is no address', async () => {
    clipboardHolds(() => Promise.resolve('2 eggs'));
    vi.resetModules();
    const { readClipboardRecipe: read } = await import('./clipboard');

    expect(await read()).toEqual({ text: '2 eggs' });
  });

  it('gives nothing when the browser refuses', async () => {
    clipboardHolds(() => Promise.reject(new Error('denied')));
    vi.resetModules();
    const { readClipboardRecipe: read } = await import('./clipboard');

    expect(await read()).toBeNull();
  });

  it('gives nothing when there is no clipboard to read', async () => {
    expect(await readClipboardRecipe()).toBeNull();
  });
});
