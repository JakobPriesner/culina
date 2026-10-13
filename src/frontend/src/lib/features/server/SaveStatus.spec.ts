import { screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import SaveStatus from './SaveStatus.svelte';
import { renderWithProviders } from '$lib/test/render';

const cause = (detail: string) => ({ field: null, code: 'settings.invalid', detail });

describe('a failed save', () => {
  it('lists every reason, even two that read the same', () => {
    renderWithProviders(SaveStatus, {
      props: {
        phase: 'idle',
        onretry: () => {},
        outcome: {
          kind: 'failed',
          error: {
            code: 'settings.invalid',
            detail: 'Those settings cannot be used.',
            status: 400,
            requestId: 'req-1',
            retryAfterSeconds: null,
            fields: [cause('Must be at least 1.'), cause('Must be at least 1.')]
          }
        }
      }
    });

    expect(screen.getAllByText('Must be at least 1.')).toHaveLength(2);
  });
});
