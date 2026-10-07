/** Drop handling for a frame that takes files matching `accept`; hands the accepted file to `onpick`. */
interface Options {
  /** The `accept` attribute's syntax: `image/png,image/*`. */
  accept: () => string;
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
  /** A count, not a flag: moving onto a child fires `dragleave` then `dragenter`, which would flicker a flag. */
  let over = $state(0);

  /** Only files; browsers may hide the type until the drop, so an unknown type passes here and is rechecked there. */
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
      // Cancelling the event is what makes the frame a valid drop target.
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

      // Otherwise the browser navigates to the photo and the form is lost.
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
