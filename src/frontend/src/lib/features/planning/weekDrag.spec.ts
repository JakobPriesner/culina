import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { WeekDrag, type OnDrop } from './weekDrag.svelte';

/* jsdom has neither PointerEvent nor hit-testing, so the pointer is a mouse event and elementFromPoint answers with the day under test. */
const pointer = (type: string, x = 0, y = 0, init: MouseEventInit = {}) =>
  Object.assign(new MouseEvent(type, { bubbles: true, clientX: x, clientY: y, ...init }), {
    pointerType: 'mouse'
  });

const held = { entryId: 'e1', title: 'Soup', from: '2026-10-12' };

let drag: WeekDrag;
let onDrop: ReturnType<typeof vi.fn<OnDrop>>;
let card: HTMLElement;
let remove: HTMLElement;

beforeEach(() => {
  document.body.innerHTML = `
    <div data-plan-day="2026-10-12">
      <div data-plan-meal id="card"><span data-no-drag><button id="remove">x</button></span></div>
    </div>
    <div data-plan-day="2026-10-13" id="tomorrow"></div>`;

  card = document.getElementById('card')!;
  remove = document.getElementById('remove')!;
  document.elementFromPoint = () => document.getElementById('tomorrow');

  drag = new WeekDrag();
  onDrop = vi.fn<OnDrop>();

  // Wired the way PlannedCard does it.
  card.addEventListener('pointerdown', (event) => drag.press(event as PointerEvent, held, onDrop));
});

afterEach(() => {
  drag.stop();
  document.body.innerHTML = '';
});

/** Pressed on the card, then moved far enough for a mouse to have picked it up. */
const lift = (target: HTMLElement = card) => {
  target.dispatchEvent(pointer('pointerdown', 10, 10));
  window.dispatchEvent(pointer('pointermove', 60, 60));
};

describe('dragging a planned meal', () => {
  it('drops it on the day it was released over', () => {
    lift();

    expect(drag.held).not.toBeNull();

    window.dispatchEvent(pointer('pointerup', 60, 60));

    expect(onDrop).toHaveBeenCalledWith(expect.objectContaining({ entryId: 'e1' }), {
      date: '2026-10-13',
      position: 0
    });
  });

  it('leaves the week alone when the browser takes the gesture over', () => {
    lift();

    window.dispatchEvent(pointer('pointercancel', 60, 60));

    // A scroll takeover or an incoming call is not a decision to move the meal.
    expect(onDrop).not.toHaveBeenCalled();
    expect(drag.held).toBeNull();
  });

  it('does not lift the card from its remove button', () => {
    lift(remove);

    expect(drag.held).toBeNull();

    window.dispatchEvent(pointer('pointerup', 60, 60));

    expect(onDrop).not.toHaveBeenCalled();
  });
});
