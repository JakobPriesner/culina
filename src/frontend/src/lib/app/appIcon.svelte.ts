import {
  appIconLinks,
  appIconStorageKey,
  defaultAppIcon,
  parseAppIcon,
  type AppIcon
} from './appIcons';
import { readDevice, writeDevice } from './deviceStorage';

/**
 * Which icon this device shows for Culina: in the tab, and on the home screen
 * once it is installed.
 *
 * Kept on the device and not with the account. Every install has its own icon,
 * and the phone and the laptop in one kitchen can reasonably want two.
 *
 * Choosing one points the document's links at that icon's files. A browser
 * reads the manifest and the touch icon when Culina is installed, not before,
 * so a choice made before installing is the one the install gets; an icon
 * already on a home screen follows on whatever terms that platform sets.
 */
class AppIconStore {
  #current = $state<AppIcon>(defaultAppIcon);

  get current(): AppIcon {
    return this.#current;
  }

  /** Points the links at what this device chose last time. Called once by the root layout. */
  start(): void {
    this.#current = parseAppIcon(readDevice(appIconStorageKey));
    this.#link();
  }

  choose(icon: AppIcon): void {
    this.#current = icon;
    writeDevice(appIconStorageKey, icon);
    this.#link();
  }

  #link(): void {
    const links = appIconLinks(this.#current);

    pointAt('link[rel="manifest"]', links.manifest);
    pointAt('link[rel="icon"][type="image/svg+xml"]', links.svg);
    pointAt('link[rel="icon"][sizes="any"]', links.ico);
    pointAt('link[rel="apple-touch-icon"]', links.appleTouch);
  }
}

function pointAt(selector: string, href: string): void {
  globalThis.document?.querySelector<HTMLLinkElement>(selector)?.setAttribute('href', href);
}

export const appIcon = new AppIconStore();
