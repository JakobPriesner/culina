import { readDevice, writeDevice } from '../deviceStorage';

const storageKey = 'culina.olla';

/**
 * Whether this device shows Olla at all.
 *
 * A mascot is charming until it isn't, and the people who tire of one should
 * not have to look at it again: Mailchimp added a way to turn Freddie off for
 * exactly that reason. Kept on the device, like the app icon — a calm laptop
 * and a cheerful phone in the same kitchen are both reasonable.
 */
class OllaSetting {
  #shown = $state(readDevice(storageKey) !== 'hidden');

  get shown(): boolean {
    return this.#shown;
  }

  show(shown: boolean): void {
    this.#shown = shown;
    writeDevice(storageKey, shown ? 'shown' : 'hidden');
  }

  /** For tests: forget the choice. */
  reset(): void {
    this.#shown = true;
  }
}

export const ollaSetting = new OllaSetting();
