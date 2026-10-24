import { asDate } from '$features/planning/mealPlan.svelte';
import { explain } from '$shell/explain';
import { m } from '$shell/i18n';
import { toaster } from '$shell/toaster.svelte';

import { requestOf } from './dayTotals';
import { diary } from './stores/diary.svelte';
import { diarySettings } from './stores/diarySettings.svelte';
import type { AppError } from '$api';

import type { DiaryEntry, DiaryEntryRequest } from './types';

const failed = (error: AppError): void => {
  toaster.show({ message: () => explain(error), tone: 'danger' });
};

/**
 * A whole meal again ("Wie gestern"): every entry written, one toast for all of them, and one Rückgängig that takes
 * them all back. What did not reach the server is said, and not claimed.
 */
export async function logAllAndConfirm(entries: readonly DiaryEntryRequest[]): Promise<void> {
  const written: DiaryEntry[] = [];

  for (const entry of entries) {
    const one = await diary.add(entry);

    if (!one.ok) {
      failed(one.error);
      break;
    }

    written.push(one.entry);
  }

  if (written.length === 0) return;

  toaster.show({
    message: () => m['diary.repeated']({ count: written.length }),
    tone: 'success',
    action: {
      label: () => m['diary.undo'](),
      run: () =>
        void Promise.all(written.map((entry) => diary.remove(entry))).then((failures) => {
          const failure = failures.find(Boolean);
          if (failure) failed(failure);
        })
    }
  });
}

/**
 * Logs something eaten and says so with an undo, from wherever it is said: the diary's sheets, a plan suggestion, or
 * "Als Portion eintragen" after "made it". A failure says why; nothing is claimed that did not reach the server.
 */
export async function logAndConfirm(entry: DiaryEntryRequest): Promise<DiaryEntry | null> {
  const written = await diary.add(entry);

  if (!written.ok) {
    failed(written.error);

    return null;
  }

  toaster.show({
    message: () => m['diary.logged']({ label: written.entry.label }),
    tone: 'success',
    action: {
      label: () => m['diary.undo'](),
      run: () =>
        void diary.remove(written.entry).then((failure) => {
          if (failure) failed(failure);
        })
    }
  });

  return written.entry;
}

/** Takes an entry off at once, with an undo that logs the same thing again (looked up afresh). */
export async function removeAndConfirm(entry: DiaryEntry): Promise<void> {
  const failure = await diary.remove(entry);

  if (failure) {
    failed(failure);

    return;
  }

  toaster.show({
    message: () => m['diary.removed']({ label: entry.label }),
    action: {
      label: () => m['diary.undo'](),
      run: () =>
        void diary.add(requestOf(entry, entry.date, entry.meal)).then((written) => {
          if (!written.ok) failed(written.error);
        })
    }
  });
}

/** The meal a portion eaten now most likely belongs to, by the clock: breakfast before 11, lunch before 15, then dinner. */
export function mealByClock(now = new Date()): 'breakfast' | 'lunch' | 'dinner' {
  const hour = now.getHours();

  return hour < 11 ? 'breakfast' : hour < 15 ? 'lunch' : 'dinner';
}

/** The portion offers still on screen, by the "made it" they offer it for. */
const offers = new Map<string, string>();

/**
 * After "made it", for someone who keeps a diary: one more toast offering to log their portion of what they cooked.
 * Its own toast, so the "made it" undo stays where it was; nothing changes for anyone else.
 */
export function offerPortion(
  recipeId: string,
  householdId: string | null,
  cookLogEntryId: string
): void {
  if (!diarySettings.enabled) {
    return;
  }

  const date = new Date();

  offers.set(
    cookLogEntryId,
    toaster.show({
      message: () => m['diary.offer.portion'](),
      action: {
        label: () => m['diary.offer.log'](),
        run: () =>
          void logAndConfirm({
            date: asDate(date),
            meal: mealByClock(date),
            recipeId,
            portions: 1,
            householdId,
            cookLogEntryId
          })
      },
      durationMs: 8000
    })
  );
}

/**
 * "Made it" was taken back: its portion goes too, each with its own undo, and an offer still on screen is withdrawn,
 * so nothing eaten stays logged for a meal that was never cooked.
 */
export async function takeBackPortion(cookLogEntryId: string): Promise<void> {
  const offer = offers.get(cookLogEntryId);

  if (offer) {
    toaster.dismiss(offer);
    offers.delete(cookLogEntryId);
  }

  for (const entry of diary.loggedFrom(cookLogEntryId)) {
    await removeAndConfirm(entry);
  }
}
