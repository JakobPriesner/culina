import {
  appIconLinks,
  appIconStorageKey,
  defaultAppIcon,
  parseAppIcon,
  type AppIcon
} from './appIcons';
import { readDevice, writeDevice } from './deviceStorage';

/**
 * The icon this device shows (tab and installed home screen); per device, not per account.
 * Browsers read the manifest and touch icon at install time, so an already-installed app keeps its icon until re-added.
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
