import { describe, expect, it, vi } from 'vitest';

import { createImageDrop, matchesAccept } from './imageDrop.svelte';

const photo = new File(['bytes'], 'dinner.jpg', { type: 'image/jpeg' });
const document = new File(['bytes'], 'notes.pdf', { type: 'application/pdf' });

/** What a browser hands over while files are dragged, and on the drop the files. */
function carrying(...files: File[]) {
  return {
    preventDefault: vi.fn(),
    dataTransfer: {
      types: ['Files'],
      items: files.map((file) => ({ kind: 'file', type: file.type })),
      files,
      dropEffect: 'none'
    }
  } as unknown as DragEvent;
}

function frame(canDrop = true, accept = 'image/jpeg,image/png') {
  const onpick = vi.fn();

  return {
    onpick,
    drop: createImageDrop({ accept: () => accept, canDrop: () => canDrop, onpick })
  };
}

describe('matchesAccept', () => {
  it('matches exact types and wildcard families', () => {
    expect(matchesAccept('image/png, image/jpeg', 'image/jpeg')).toBe(true);
    expect(matchesAccept('image/*', 'image/webp')).toBe(true);
    expect(matchesAccept('image/png', 'application/pdf')).toBe(false);
  });
});

describe('a frame that takes dropped files', () => {
  it('is being dropped on from the first drag in until the last one out', () => {
    const { drop } = frame();

    drop.dragenter(carrying(photo));
    drop.dragenter(carrying(photo));
    drop.dragleave();

    expect(drop.dropping).toBe(true);

    drop.dragleave();

    expect(drop.dropping).toBe(false);
  });

  it('reports the accepted file and stops being dropped on', () => {
    const { drop, onpick } = frame();

    drop.dragenter(carrying(document, photo));
    drop.drop(carrying(document, photo));

    expect(onpick).toHaveBeenCalledWith(photo);
    expect(drop.dropping).toBe(false);
  });

  it('ignores a drag of something that is not an accepted file', () => {
    const { drop, onpick } = frame();

    drop.dragenter(carrying(document));
    drop.drop(carrying(document));

    expect(drop.dropping).toBe(false);
    expect(onpick).not.toHaveBeenCalled();
  });

  it('takes nothing while busy', () => {
    const { drop, onpick } = frame(false);

    drop.dragenter(carrying(photo));
    drop.drop(carrying(photo));

    expect(drop.dropping).toBe(false);
    expect(onpick).not.toHaveBeenCalled();
  });
});
