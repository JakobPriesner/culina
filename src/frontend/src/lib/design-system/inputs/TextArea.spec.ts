import { screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

import TextAreaHarness from '../__fixtures__/TextAreaHarness.svelte';
import { renderWithProviders } from '$lib/test/render';

// jsdom lays nothing out, so every element is 0px tall; a line is 20px here.
function measureByLines() {
  vi.spyOn(HTMLTextAreaElement.prototype, 'scrollHeight', 'get').mockImplementation(function (
    this: HTMLTextAreaElement
  ) {
    return this.value.split('\n').length * 20;
  });
}

describe('TextArea', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('grows to fit a value set from outside after it is shown', async () => {
    measureByLines();
    const { rerender } = renderWithProviders(TextAreaHarness, { props: { value: '' } });

    const tenLines = Array.from({ length: 10 }, (_, line) => `Line ${line + 1}`).join('\n');
    await rerender({ value: tenLines });

    const area = screen.getByLabelText('Note');
    expect(area.style.height).toBe(`${area.scrollHeight}px`);
    expect(area.style.height).toBe('200px');
  });
});
