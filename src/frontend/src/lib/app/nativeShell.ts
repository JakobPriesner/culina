import { SecureStorage } from '@aparajita/capacitor-secure-storage';
import { KeepAwake } from '@capacitor-community/keep-awake';
import { App } from '@capacitor/app';
import { Haptics, ImpactStyle, NotificationType } from '@capacitor/haptics';

import { useSecretStore } from '$api';
import { drainNativeTimers, startNativeTimers } from '$features/cooking/nativeTimers';
import { useNativeWakeLock } from '$features/cooking/wakeLock.svelte';

import { useNativeHaptics, type HapticMoment } from './haptics';
import { startNativeImages } from './nativeImages';

let started = false;

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
