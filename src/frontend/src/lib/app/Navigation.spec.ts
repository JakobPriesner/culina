import { screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { session } from '$features/auth/session.svelte';
import { renderWithProviders } from '$lib/test/render';

import Navigation from './Navigation.svelte';

const page = vi.hoisted(() => ({ url: new URL('http://culina.test/') }));

vi.mock('$app/state', () => ({ page }));

/* Every destination belongs to a household; without one they would only lead back to /welcome. */
afterEach(() => vi.restoreAllMocks());

describe('the destinations', () => {
  it('are offered once there is a household to go to', () => {
    vi.spyOn(session, 'activeHouseholdId', 'get').mockReturnValue('h1');

    renderWithProviders(Navigation, { props: { placement: 'bottom' } });

    expect(screen.getByRole('navigation')).toBeInTheDocument();
  });

  it('stay away while the account has no household yet', () => {
    vi.spyOn(session, 'activeHouseholdId', 'get').mockReturnValue(null);

    renderWithProviders(Navigation, { props: { placement: 'bottom' } });

    expect(screen.queryByRole('navigation')).toBeNull();
  });
});
