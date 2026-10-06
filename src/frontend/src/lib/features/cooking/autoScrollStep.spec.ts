import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { autoScrollStep } from './autoScrollStep';

describe('reading a long cooking step hands-free', () => {
  let root: HTMLElement;
  let now: number;
  let scroll: number;
  let callbacks: Map<number, FrameRequestCallback>;
  let serial: number;
  let hidden: boolean;
  let reduce: boolean;
  let height: number;
  const onstop = vi.fn();
  const options = () => ({ enabled: true, step: 0, suspended: false, onstop });

  function advance(milliseconds: number) {
    const until = now + milliseconds;
    while (now < until) {
      const next = Math.min(until, now + 50);
      vi.advanceTimersByTime(next - now);
      now = next;
      const pending = [...callbacks.values()];
      callbacks.clear();
      pending.forEach((run) => run(now));
    }
  }

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    now = scroll = serial = 0;
    hidden = reduce = false;
    height = 800;
    callbacks = new Map();
    onstop.mockClear();
    root = document.createElement('div');
    root.innerHTML =
      '<div class="step current"></div><div class="controls"><button>Stop</button></div>';
    document.body.append(root);
    const step = root.querySelector<HTMLElement>('.step')!;
    step.style.scrollMarginTop = '100px';
    step.style.scrollMarginBottom = '100px';
    vi.spyOn(step, 'getBoundingClientRect').mockImplementation(
      () => new DOMRect(0, 300 - scroll, 320, height)
    );
    vi.spyOn(window, 'innerHeight', 'get').mockReturnValue(500);
    vi.spyOn(window, 'scrollY', 'get').mockImplementation(() => scroll);
    vi.spyOn(document.documentElement, 'scrollHeight', 'get').mockReturnValue(2500);
    vi.spyOn(document, 'hidden', 'get').mockImplementation(() => hidden);
    vi.spyOn(window, 'scrollTo').mockImplementation((value) => {
      scroll = (value as ScrollToOptions).top!;
    });
    vi.spyOn(window, 'matchMedia').mockImplementation(
      (query) =>
        ({
          matches: reduce,
          media: query,
          addEventListener: vi.fn(),
          removeEventListener: vi.fn()
        }) as unknown as MediaQueryList
    );
    vi.stubGlobal('requestAnimationFrame', (run: FrameRequestCallback) => {
      callbacks.set(++serial, run);
      return serial;
    });
    vi.stubGlobal('cancelAnimationFrame', (id: number) => callbacks.delete(id));
  });

  afterEach(() => {
    root.remove();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it('starts at the first line, pauses, reads to the last line and returns', () => {
    const reader = autoScrollStep(root, options());
    advance(1000);
    expect(scroll).toBe(200);
    advance(4800);
    expect(scroll).toBe(200);
    advance(25100);
    expect(scroll).toBe(700);
    advance(4800);
    expect(scroll).toBe(700);
    advance(1000);
    expect(scroll).toBeLessThan(700);
    expect(scroll).toBeGreaterThanOrEqual(200);
    reader.destroy();
    expect(callbacks.size).toBe(0);
  });

  it('leaves fitting steps and disabled reading still', () => {
    height = 250;
    const reader = autoScrollStep(root, options());
    advance(20000);
    expect(window.scrollTo).not.toHaveBeenCalled();
    reader.update({ ...options(), enabled: false });
    expect(callbacks.size).toBe(0);
    reader.destroy();
  });

  it('shows overlapping still portions when reduced motion is requested', () => {
    reduce = true;
    const reader = autoScrollStep(root, options());
    advance(1000);
    expect(scroll).toBe(200);
    advance(7900);
    expect(scroll).toBe(410);
    advance(7900);
    expect(scroll).toBe(410);
    advance(100);
    expect(scroll).toBe(620);
    advance(8000);
    expect(scroll).toBe(700);
    advance(8000);
    expect(scroll).toBe(490);
    reader.destroy();
  });

  it('pauses behind sheets and while hidden, then resumes without a time jump', () => {
    const reader = autoScrollStep(root, options());
    advance(7000);
    const before = scroll;
    reader.update({ ...options(), suspended: true });
    advance(60000);
    expect(scroll).toBe(before);
    reader.update(options());
    hidden = true;
    document.dispatchEvent(new Event('visibilitychange'));
    expect(callbacks.size).toBe(0);
    advance(60000);
    expect(scroll).toBe(before);
    hidden = false;
    document.dispatchEvent(new Event('visibilitychange'));
    advance(1000);
    expect(scroll).toBeGreaterThan(before);
    expect(scroll - before).toBeLessThan(10);
    reader.destroy();
  });

  it.each(['wheel', 'touchstart', 'pointerdown', 'keydown'])('stops on manual %s input', (type) => {
    const reader = autoScrollStep(root, options());
    advance(7000);
    root
      .querySelector('.step')!
      .dispatchEvent(
        type === 'keydown'
          ? new KeyboardEvent(type, { key: 'ArrowDown', bubbles: true })
          : new Event(type, { bubbles: true })
      );
    const before = scroll;
    advance(10000);
    expect(scroll).toBe(before);
    expect(onstop).toHaveBeenCalledOnce();
    reader.destroy();
  });

  it('keeps controls usable and restarts at the beginning of a new step', () => {
    const reader = autoScrollStep(root, options());
    advance(7000);
    root.querySelector('button')!.dispatchEvent(new Event('pointerdown', { bubbles: true }));
    root
      .querySelector('button')!
      .dispatchEvent(new KeyboardEvent('keydown', { key: ' ', bubbles: true }));
    expect(onstop).not.toHaveBeenCalled();
    reader.update({ ...options(), step: 1 });
    advance(1000);
    expect(scroll).toBe(200);
    reader.destroy();
  });
});
