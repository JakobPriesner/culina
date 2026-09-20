import { screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import PlanWeekSkeleton from './PlanWeekSkeleton.svelte';
import { renderWithProviders } from '$lib/test/render';

describe('PlanWeekSkeleton', () => {
  it('renders a 7-day week skeleton with aria-busy set', () => {
    const { container } = renderWithProviders(PlanWeekSkeleton);

    const week = screen.getByRole('list');
    expect(week).toHaveAttribute('aria-busy', 'true');
    expect(container.querySelectorAll('li.day')).toHaveLength(7);
  });

  it('hides individual placeholders from assistive technology', () => {
    const { container } = renderWithProviders(PlanWeekSkeleton);

    for (const block of container.querySelectorAll('.skeleton')) {
      expect(block).toHaveAttribute('aria-hidden', 'true');
    }
  });
});
