import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createStepGestures } from './stepGestures';

function touch(clientX: number, clientY: number, target: EventTarget = document.body) {
  const event = new Event('touch', { bubbles: true }) as unknown as TouchEvent;
  Object.defineProperty(event, 'changedTouches', { value: [{ clientX, clientY }] });
  Object.defineProperty(event, 'target', { value: target });
  return event;
}

describe('moving between steps by key and swipe', () => {
  const move = vi.fn();
  const advance = vi.fn();
  let ready: boolean;
  let gestures: ReturnType<typeof createStepGestures>;

  beforeEach(() => {
    vi.useFakeTimers();
    move.mockReset();
    advance.mockReset();
    ready = true;
    gestures = createStepGestures({ ready: () => ready, currentStep: () => 2, move, advance });
  });

  afterEach(() => vi.useRealTimers());

  it('steps with the arrow and page keys', () => {
    gestures.keydown(new KeyboardEvent('keydown', { key: 'ArrowRight', cancelable: true }));
    gestures.keydown(new KeyboardEvent('keydown', { key: 'PageUp', cancelable: true }));

    expect(move).toHaveBeenNthCalledWith(1, 3);
    expect(move).toHaveBeenNthCalledWith(2, 1);
  });

  it('leaves keys alone before a session exists or when a control wants them', () => {
    ready = false;
    gestures.keydown(new KeyboardEvent('keydown', { key: 'ArrowRight' }));

    ready = true;
    const field = document.createElement('input');
    document.body.append(field);
    gestures.keydown(
      Object.defineProperty(new KeyboardEvent('keydown', { key: 'ArrowRight' }), 'target', {
        value: field
      })
    );
    field.remove();

    expect(move).not.toHaveBeenCalled();
  });

  it('treats a quick horizontal swipe as a step move', () => {
    gestures.touchstart(touch(200, 100));
    gestures.touchend(touch(100, 110));
    expect(advance).toHaveBeenCalledOnce();

    gestures.touchstart(touch(100, 100));
    gestures.touchend(touch(200, 100));
    expect(move).toHaveBeenCalledWith(1);
  });

  it('ignores slow, short or mostly vertical drags', () => {
    gestures.touchstart(touch(200, 100));
    vi.advanceTimersByTime(700);
    gestures.touchend(touch(100, 100));

    gestures.touchstart(touch(200, 100));
    gestures.touchend(touch(170, 100));

    gestures.touchstart(touch(200, 100));
    gestures.touchend(touch(120, 200));

    expect(advance).not.toHaveBeenCalled();
    expect(move).not.toHaveBeenCalled();
  });
});
