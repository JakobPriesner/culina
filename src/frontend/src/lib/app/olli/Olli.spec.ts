import { render, screen } from '@testing-library/svelte';
import { createRawSnippet } from 'svelte';
import { beforeEach, describe, expect, it } from 'vitest';

import Olli from './Olli.svelte';
import { olliSetting } from './setting.svelte';

const fallback = createRawSnippet(() => ({ render: () => '<p>The plain version</p>' }));

beforeEach(() => {
  localStorage.clear();
  olliSetting.reset();
});

describe('Olli', () => {
  it('is decoration: the page says it in words, so a screen reader hears nothing', () => {
    const { container } = render(Olli, { props: { pose: 'hello' } });

    expect(container.querySelector('svg')).toHaveAttribute('aria-hidden', 'true');
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
  });

  it('gives way to the plain version when this device turned it off', () => {
    olliSetting.show(false);

    const { container } = render(Olli, { props: { pose: 'peeking', fallback } });

    expect(container.querySelector('svg')).not.toBeInTheDocument();
    expect(screen.getByText('The plain version')).toBeInTheDocument();
  });

  it('leaves nothing behind when turned off and there is no plain version', () => {
    olliSetting.show(false);

    const { container } = render(Olli, { props: { pose: 'hello' } });

    expect(container.querySelector('svg')).not.toBeInTheDocument();
  });

  it('remembers being turned off on this device', () => {
    olliSetting.show(false);

    expect(localStorage.getItem('culina.olli')).toBe('hidden');
  });

  it('is fully there at once when it must not move', () => {
    const { container } = render(Olli, { props: { pose: 'celebrating', size: 'sm', still: true } });

    expect(container.querySelector('svg > g')).toHaveAttribute('opacity', '1');
  });
});
