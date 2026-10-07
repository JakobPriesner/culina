import { fireEvent, screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import SectionWatchHarness from './__fixtures__/SectionWatchHarness.svelte';
import { renderWithProviders } from '$lib/test/render';

let report: (entries: Partial<IntersectionObserverEntry>[]) => void;

beforeEach(() => {
  vi.stubGlobal(
    'IntersectionObserver',
    class {
      constructor(callback: (entries: Partial<IntersectionObserverEntry>[]) => void) {
        report = callback;
      }
      observe() {}
      disconnect() {}
    }
  );
});

afterEach(() => {
  vi.unstubAllGlobals();
});

const current = () => screen.getByTestId('current');
const heading = (id: string) => document.getElementById(id)!;

describe('the section being read', () => {
  it('is the first until the page says otherwise', () => {
    renderWithProviders(SectionWatchHarness, {});

    expect(current()).toHaveTextContent('basics');
    expect(screen.getByTestId('away')).toHaveTextContent('false');
  });

  it('is the first in the recipe order of those in view, not the first reported', async () => {
    renderWithProviders(SectionWatchHarness, {});

    report([
      { target: heading('steps'), isIntersecting: true },
      { target: heading('ingredients'), isIntersecting: true }
    ]);

    await vi.waitFor(() => expect(current()).toHaveTextContent('ingredients'));
    expect(screen.getByTestId('away')).toHaveTextContent('true');
  });
});

describe('jumping to a section', () => {
  it('marks the section and moves focus to it', async () => {
    renderWithProviders(SectionWatchHarness, {});

    await userEvent.click(screen.getByRole('button', { name: 'Jump steps' }));

    expect(current()).toHaveTextContent('steps');
    expect(heading('steps')).toHaveFocus();
  });

  it('leaves a click that opens it somewhere else to the browser', async () => {
    renderWithProviders(SectionWatchHarness, {});

    await fireEvent.click(screen.getByRole('button', { name: 'Jump steps' }), { metaKey: true });

    expect(current()).toHaveTextContent('basics');
  });
});
