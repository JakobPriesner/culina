/**
 * Reduce Transparency and Increase Contrast, mirrored onto <html> as `data-transparency` and `data-contrast` the way
 * `data-mode` mirrors the colour scheme, so no component queries the device itself (tokens/accessibility.css reads
 * them). app.html stamps them before the first paint; this follows changes. WebKit cannot report Reduce Transparency,
 * so the iOS app reports both settings from UIAccessibility (`reportNativeDisplay`), and either source turning one on
 * turns it on.
 */
export interface DisplaySettings {
  readonly reduceTransparency: boolean;
  readonly increaseContrast: boolean;
}

const transparencyQuery = '(prefers-reduced-transparency: reduce)';
const contrastQuery = '(prefers-contrast: more)';

let fromDevice: DisplaySettings = { reduceTransparency: false, increaseContrast: false };

let fromBrowser: DisplaySettings = { reduceTransparency: false, increaseContrast: false };

function paint(): void {
  const root = globalThis.document?.documentElement;

  if (!root) {
    return;
  }

  root.dataset['transparency'] =
    fromBrowser.reduceTransparency || fromDevice.reduceTransparency ? 'reduced' : 'normal';
  root.dataset['contrast'] =
    fromBrowser.increaseContrast || fromDevice.increaseContrast ? 'more' : 'normal';
}

/** Follows the browser's media queries; called once by the root layout. */
export function followDisplaySettings(): () => void {
  const transparency = globalThis.matchMedia?.(transparencyQuery);
  const contrast = globalThis.matchMedia?.(contrastQuery);

  const read = () => {
    fromBrowser = {
      reduceTransparency: transparency?.matches === true,
      increaseContrast: contrast?.matches === true
    };
    paint();
  };

  read();
  transparency?.addEventListener('change', read);
  contrast?.addEventListener('change', read);

  return () => {
    transparency?.removeEventListener('change', read);
    contrast?.removeEventListener('change', read);
  };
}

/** What the installed app's operating system says, which the browser may not know. */
export function reportNativeDisplay(settings: DisplaySettings): void {
  fromDevice = settings;
  paint();
}
