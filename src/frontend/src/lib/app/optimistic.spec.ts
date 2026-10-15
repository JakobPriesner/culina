import { describe, expect, it } from 'vitest';

import { removeUndoable, ResetEpoch } from './optimistic';

const same = (id: string) => (item: { id: string }) => item.id === id;

describe('removeUndoable', () => {
  const list = [{ id: 'a' }, { id: 'b' }, { id: 'c' }];

  it('puts only the removed element back at its place, keeping what changed meanwhile', () => {
    const gone = removeUndoable(list, same('b'));

    expect(gone.items.map((one) => one.id)).toEqual(['a', 'c']);

    const meanwhile = [...gone.items, { id: 'd' }];

    expect(gone.undo(meanwhile).map((one) => one.id)).toEqual(['a', 'b', 'c', 'd']);
  });

  it('does not insert it twice when it is already back', () => {
    const gone = removeUndoable(list, same('b'));

    expect(gone.undo(list)).toHaveLength(3);
  });

  it('removes and restores nothing for an element that is not there', () => {
    const gone = removeUndoable(list, same('x'));

    expect(gone.items).toHaveLength(3);
    expect(gone.undo(gone.items)).toHaveLength(3);
  });
});

describe('ResetEpoch', () => {
  it('runs an undo until the store is reset, then skips it', () => {
    const epoch = new ResetEpoch();
    let undone = 0;

    epoch.guard(() => undone++)();
    expect(undone).toBe(1);

    const late = epoch.guard(() => undone++);

    epoch.bump();
    late();
    expect(undone).toBe(1);
  });
});
