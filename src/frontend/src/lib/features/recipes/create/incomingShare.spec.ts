import { describe, expect, it } from 'vitest';

import { readIncomingShare } from './incomingShare';

const read = (query: string) => readIncomingShare(new URLSearchParams(query));

describe('readIncomingShare', () => {
  it('takes the address from url when it is one', () => {
    expect(read('url=https://example.com/soup&text=Soup').url).toBe('https://example.com/soup');
  });

  it('finds an address inside the shared text when url is not one', () => {
    const shared = read('url=nonsense&text=Look%20at%20https://example.com/soup%20now');

    expect(shared.url).toBe('https://example.com/soup');
  });

  it('has no address when nothing looks like one', () => {
    expect(read('text=Just%20words')).toEqual({ title: '', url: '', text: 'Just words' });
  });

  it('trims the title', () => {
    expect(read('title=%20Soup%20').title).toBe('Soup');
  });
});
