/**
 * A frame that files can be dropped onto.
 *
 * Only files, only of a kind the field accepts, and only while the field is
 * not already busy being filled. Knows nothing about what the file is for: it
 * hands the accepted one to `onpick` and says whether one is being held over.
 */
interface Options {
  /** The `accept` attribute's own syntax: `image/png,image/*`. */
  accept: () => string;
  /** Nothing can land while the frame is already busy being filled. */
  canDrop: () => boolean;
  onpick: (file: File) => void;
}

export function matchesAccept(accept: string, type: string) {
  return accept
    .split(',')
    .map((entry) => entry.trim())
    .some(
      (entry) => entry === type || (entry.endsWith('/*') && type.startsWith(entry.slice(0, -1)))
    );
}

export function createImageDrop({ accept, canDrop, onpick }: Options) {
  /**
   * How many of the frame's elements the dragged file is currently over.
   *
   * Counted rather than a flag, because moving from the frame onto the picture
   * inside it is a `dragleave` from one and a `dragenter` into the other, and a
   * flag cleared on every leave makes the drop target flicker.
   */
  let over = $state(0);

  /**
   * Whether this drag is something the frame would take.
   *
   * Only files: dragging a word or a link across the form is not an upload. A
   * browser may not say what kind of file it is until the drop, so an unknown
   * type is given the benefit of the doubt here and checked again there.
   */
  function wanted(event: DragEvent) {
    const transfer = event.dataTransfer;

    if (!canDrop() || !transfer?.types.includes('Files')) {
      return false;
    }

    return [...transfer.items].some(
      (item) => item.kind === 'file' && (!item.type || matchesAccept(accept(), item.type))
    );
  }

  return {
    get dropping() {
      return over > 0;
    },

    dragenter(event: DragEvent) {
      if (wanted(event)) {
        event.preventDefault();
        over += 1;
      }
    },

    dragover(event: DragEvent) {
      // Taking the event is what makes the frame a place a file can be dropped;
      // not taking it leaves the browser's own "no" cursor, which is the answer.
      if (wanted(event)) {
        event.preventDefault();
        event.dataTransfer!.dropEffect = 'copy';
      }
    },

    dragleave() {
      over = Math.max(0, over - 1);
    },

    drop(event: DragEvent) {
      over = 0;

      if (!wanted(event)) {
        return;
      }

      // Otherwise the browser opens the photo in this tab, and the form with it
      // is gone.
      event.preventDefault();

      const file = [...(event.dataTransfer?.files ?? [])].find((candidate) =>
        matchesAccept(accept(), candidate.type)
      );

      if (file) {
        onpick(file);
      }
    }
  };
}
