import { SecureStorage } from '@aparajita/capacitor-secure-storage';
import { KeepAwake } from '@capacitor-community/keep-awake';
import { App } from '@capacitor/app';
import { Capacitor, registerPlugin, type PluginListenerHandle } from '@capacitor/core';
import { Haptics, ImpactStyle, NotificationType } from '@capacitor/haptics';

import { goto } from '$app/navigation';

import { useSecretStore } from '$api';
import { drainNativeTimers, startNativeTimers } from '$features/cooking/nativeTimers';
import { openTappedNotifications } from '$features/import/nativePush';
import { useNativeWakeLock } from '$features/cooking/wakeLock.svelte';

import { reportNativeDisplay, type DisplaySettings } from './displaySettings';
import { applyDynamicType } from './dynamicType';
import { useNativeHaptics, type HapticMoment } from './haptics';
import { startNativeImages } from './nativeImages';

let started = false;

/** The iOS app's own plugin (native/ios/App/App/CulinaDisplayPlugin.swift): display settings WebKit does not report. */
interface CulinaDisplayPlugin {
  get(): Promise<DisplaySettings>;
  addListener(
    event: 'change',
    listener: (settings: DisplaySettings) => void
  ): Promise<PluginListenerHandle>;
}

const CulinaDisplay = registerPlugin<CulinaDisplayPlugin>('CulinaDisplay');

/**
 * What the installed app sets up once, before its first route (docs/native-app-design.md, 2.2): where its token is kept,
 * where photos come from, and the platform's own keep-awake, haptics and Back button. Loaded only by the app's bundle
 * (`src/routes/+layout.ts`), so the website carries none of the Capacitor code.
 */
export async function startNativeShell(): Promise<void> {
  if (started) {
    return;
  }

  started = true;

  // The Keychain (iOS) and the Keystore (Android); never synced to iCloud, a session belongs to this device.
  await SecureStorage.setKeyPrefix('culina.').catch(() => {});
  await SecureStorage.setSynchronize(false).catch(() => {});
  useSecretStore({
    read: (key) => SecureStorage.getItem(key),
    write: (key, value) => SecureStorage.setItem(key, value),
    remove: async (key) => {
      await SecureStorage.remove(key);
    }
  });

  startNativeImages();

  useNativeWakeLock({ hold: () => KeepAwake.keepAwake(), release: () => KeepAwake.allowSleep() });

  useNativeHaptics(feel);

  // "Your recipe is ready": a tap opens the import it names, including the tap that launched the app.
  // eslint-disable-next-line svelte/no-navigation-without-resolve -- a path the server built (`/recipes/imports/{id}`), checked same-origin by openTappedNotifications.
  await openTappedNotifications((path) => void goto(path));

  if (Capacitor.getPlatform() === 'ios') {
    await followTheIPhone();
  }

  // Before the first route: presses on the lock screen while the app was closed are applied first.
  await startNativeTimers();
  await App.addListener('resume', () => void drainNativeTimers());

  // Android's Back closes what is open on top before it leaves the page, and leaves the app from its first page.
  await App.addListener('backButton', ({ canGoBack }) => {
    if (closeTopSurface()) {
      return;
    }

    if (canGoBack) {
      history.back();
    } else {
      void App.exitApp();
    }
  });
}

/**
 * The iPhone's text size and its Reduce Transparency and Increase Contrast, which a web page cannot read for itself.
 * Android's web view already follows the system's font scale and reports contrast through the media query.
 */
async function followTheIPhone(): Promise<void> {
  applyDynamicType(document);
  await App.addListener('resume', () => void applyDynamicType(document));

  const settings = await CulinaDisplay.get().catch(() => null);

  if (settings) {
    reportNativeDisplay(settings);
    await CulinaDisplay.addListener('change', reportNativeDisplay).catch(() => {});
  }
}

function feel(moment: HapticMoment): void {
  const play: Record<HapticMoment, () => Promise<void>> = {
    tick: () => Haptics.impact({ style: ImpactStyle.Light }),
    step: () => Haptics.impact({ style: ImpactStyle.Medium }),
    celebrate: () => Haptics.notification({ type: NotificationType.Success }),
    alarm: () => Haptics.notification({ type: NotificationType.Warning })
  };

  void play[moment]().catch(() => {});
}

/** Closes the top-most open popover or dialog the way Escape would, so its own close handling runs. */
function closeTopSurface(): boolean {
  const popover = [...document.querySelectorAll<HTMLElement>(':popover-open')].at(-1);

  if (popover) {
    popover.hidePopover();

    return true;
  }

  const dialog = [...document.querySelectorAll<HTMLDialogElement>('dialog[open]')].at(-1);

  if (dialog) {
    if ('requestClose' in dialog && typeof dialog.requestClose === 'function') {
      dialog.requestClose();
    } else {
      dialog.close();
    }

    return true;
  }

  return false;
}
