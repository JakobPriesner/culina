import { readDevice, writeDevice } from '../deviceStorage';

const storageKey = 'culina.olli.motion';

/** Olli stays visible; only motion is a device preference. */
export function readOlliMotion(): boolean {
  const choice = readDevice(storageKey);
  if (choice !== null) return choice !== 'off';
  // Respect an older request for a quiet interface without hiding Olli.
  return (readDevice('culina.olli') ?? readDevice('culina.olla')) !== 'hidden';
}

class OlliSetting {
  #animated = $state(readOlliMotion());
  get animated(): boolean {
    return this.#animated;
  }
  animate(animated: boolean): void {
    this.#animated = animated;
    writeDevice(storageKey, animated ? 'on' : 'off');
  }
  /** For tests: forget the choice. */
  reset(): void {
    this.#animated = true;
  }
}

export const olliSetting = new OlliSetting();
