import { render, screen } from '@testing-library/svelte';
import { beforeEach, describe, expect, it } from 'vitest';
import Olli from './Olli.svelte';
import { olliSetting, readOlliMotion } from './setting.svelte';

beforeEach(() => {
  localStorage.clear();
  olliSetting.reset();
});

describe('Olli', () => {
  it('is decoration; the page says it in words', () => {
    const { container } = render(Olli, { props: { pose: 'hello' } });
    expect(container.querySelector('svg')).toHaveAttribute('aria-hidden', 'true');
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
  });
  it('stays fully visible when motion is disabled', () => {
    olliSetting.animate(false);
    const { container } = render(Olli, { props: { pose: 'drawing', working: true } });
    expect(container.querySelector('svg > g')).toHaveAttribute('opacity', '1');
    expect(container.querySelector('.brush')).toBeInTheDocument();
    expect(container.querySelector('svg')).toHaveAttribute('data-phase', 'still');
    expect(localStorage.getItem('culina.olli.motion')).toBe('off');
  });
  it('migrates both legacy hidden choices to stillness and prefers the new setting', () => {
    localStorage.setItem('culina.olla', 'hidden');
    expect(readOlliMotion()).toBe(false);
    localStorage.setItem('culina.olli', 'shown');
    expect(readOlliMotion()).toBe(true);
    localStorage.setItem('culina.olli', 'hidden');
    expect(readOlliMotion()).toBe(false);
    localStorage.setItem('culina.olli.motion', 'on');
    expect(readOlliMotion()).toBe(true);
  });
  it('watches with a phone and one pair of handles', () => {
    const { container } = render(Olli, { props: { pose: 'watching', still: true } });
    expect(container.querySelector('.earbuds')).not.toBeInTheDocument();
    expect(container.querySelector('.phone')).toBeInTheDocument();
    expect(container.querySelectorAll('.handle')).toHaveLength(2);
  });
});
