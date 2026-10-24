import type { Snippet } from 'svelte';

/**
 * A page's own controls carried in the floating header, under its pills: Apple's navigation bar with a segmented
 * control in it. The header's scrim then covers them and fades out below them, where a sticky bar of the page's own
 * would sit under the fade (the scrim is above every page) and read through it.
 */
class HeaderAccessory {
  #current = $state<Snippet | null>(null);

  get current(): Snippet | null {
    return this.#current;
  }

  /** Shows `content` while the caller lives; the returned function takes it away again (an effect's teardown). */
  show(content: Snippet): () => void {
    this.#current = content;

    return () => {
      if (this.#current === content) this.#current = null;
    };
  }
}

export const headerAccessory = new HeaderAccessory();
