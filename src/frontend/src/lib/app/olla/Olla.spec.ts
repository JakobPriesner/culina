import { render, screen } from '@testing-library/svelte';
import { createRawSnippet } from 'svelte';
import { beforeEach, describe, expect, it } from 'vitest';

import Olla from './Olla.svelte';
import { ollaSetting } from './setting.svelte';

const fallback = createRawSnippet(() => ({ render: () => '<p>The plain version</p>' }));

beforeEach(() => {
  localStorage.clear();
  ollaSetting.reset();
});

describe('Olla', () => {
  it('is decoration: the page says it in words, so a screen reader hears nothing', () => {
    const { container } = render(Olla, { props: { pose: 'hello' } });

    expect(container.querySelector('svg')).toHaveAttribute('aria-hidden', 'true');
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
  });

  it('gives way to the plain version when this device turned it off', () => {
    ollaSetting.show(false);

    const { container } = render(Olla, { props: { pose: 'peeking', fallback } });

    expect(container.querySelector('svg')).not.toBeInTheDocument();
    expect(screen.getByText('The plain version')).toBeInTheDocument();
  });

  it('leaves nothing behind when turned off and there is no plain version', () => {
    ollaSetting.show(false);

    const { container } = render(Olla, { props: { pose: 'hello' } });

    expect(container.querySelector('svg')).not.toBeInTheDocument();
  });

  it('remembers being turned off on this device', () => {
    ollaSetting.show(false);

    expect(localStorage.getItem('culina.olla')).toBe('hidden');
  });

  it('is fully there at once when it must not move', () => {
    const { container } = render(Olla, { props: { pose: 'celebrating', size: 'sm', still: true } });

    expect(container.querySelector('svg > g')).toHaveAttribute('opacity', '1');
  });
});
