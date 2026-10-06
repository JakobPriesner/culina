import { afterEach, describe, expect, it, vi } from 'vitest';
import { lockScroll, unlockScroll } from './scrollLock';

afterEach(() => {
  unlockScroll();
  unlockScroll();
  document.documentElement.style.overflow = '';
  document.body.style.overflow = '';
  document.body.style.paddingInlineEnd = '';
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('modal scroll locking', () => {
  it('keeps the document locked until the last nested dialog closes and restores prior styles', () => {
    vi.stubGlobal('CSS', { supports: () => true });
    vi.spyOn(document.documentElement, 'clientWidth', 'get').mockReturnValue(
      window.innerWidth - 20
    );
    document.body.style.paddingInlineEnd = '12px';
    document.documentElement.style.overflow = 'auto';
    document.body.style.overflow = 'clip';
    lockScroll();
    lockScroll();
    unlockScroll();
    expect(document.body.style.paddingInlineEnd).toBe('12px');
    expect(document.documentElement.style.overflow).toBe('hidden');
    expect(document.body.style.overflow).toBe('hidden');
    unlockScroll();
    expect(document.documentElement.style.overflow).toBe('auto');
    expect(document.body.style.overflow).toBe('clip');
    document.body.style.overflow = 'scroll';
    unlockScroll();
    expect(document.body.style.overflow).toBe('scroll');
  });

  it('preserves padding in the legacy scrollbar compensation path', () => {
    vi.stubGlobal('CSS', { supports: () => false });
    vi.spyOn(document.documentElement, 'clientWidth', 'get').mockReturnValue(
      window.innerWidth - 20
    );
    document.body.style.paddingInlineEnd = '12px';
    lockScroll();
    expect(document.body.style.paddingInlineEnd).toBe('32px');
    lockScroll();
    unlockScroll();
    expect(document.body.style.overflow).toBe('hidden');
    unlockScroll();
    expect(document.body.style.paddingInlineEnd).toBe('12px');
  });
});
