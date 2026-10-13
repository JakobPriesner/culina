import { afterEach, describe, expect, it, vi } from 'vitest';

import { watch } from './events';
import { handleSessionExpiry } from './session';

/* `watch` reads with a plain `fetch`, past the typed client's middleware, so what that middleware does is asserted again here. */

afterEach(() => {
  handleSessionExpiry(() => {});
});

describe('watching a stream', () => {
  it('ends the session once when the server says it is over', async () => {
    const expired = vi.fn();
    const failed = Promise.withResolvers<unknown>();

    handleSessionExpiry(expired);
    vi.stubGlobal(
      'fetch',
      vi.fn(() =>
        Promise.resolve(
          new Response(JSON.stringify({ code: 'auth.not_authenticated', detail: 'Sign in.' }), {
            status: 401,
            headers: { 'Content-Type': 'application/problem+json' }
          })
        )
      )
    );

    watch('/api/v1/imports/i1/events', { message: vi.fn(), failed: failed.resolve });

    expect(await failed.promise).toMatchObject({ code: 'auth.not_authenticated' });
    expect(expired).toHaveBeenCalledTimes(1);
  });
});
