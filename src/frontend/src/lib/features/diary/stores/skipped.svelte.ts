import { readDevice, writeDevice } from '$shell/deviceStorage';

import { addDays } from '../format';

const key = 'culina.diary.skipped';

/** Days are forgotten after a week: a planned meal from then is no longer offered anyway. */
const kept = 7;

type Skipped = Record<string, readonly string[]>;

function read(): Skipped {
  try {
    const parsed: unknown = JSON.parse(readDevice(key) ?? '{}');

    return parsed && typeof parsed === 'object' ? (parsed as Skipped) : {};
  } catch {
    return {};
  }
}

/**
 * Planned meals answered "Übersprungen" (docs/food-log-redesign.md §4.1, after Apple's Medications): a neutral answer,
 * not a lapse. It only stops the plan offering that meal for that day, on this device; nothing is counted or kept
 * on the server.
 */
class SkippedMeals {
  #days = $state<Skipped>(read());

  has(date: string, entryId: string): boolean {
    return this.#days[date]?.includes(entryId) ?? false;
  }

  skip(date: string, entryId: string, today: string): void {
    this.#write({ ...this.#days, [date]: [...(this.#days[date] ?? []), entryId] }, today);
  }

  unskip(date: string, entryId: string, today: string): void {
    this.#write(
      { ...this.#days, [date]: (this.#days[date] ?? []).filter((one) => one !== entryId) },
      today
    );
  }

  #write(days: Skipped, today: string): void {
    const oldest = addDays(today, -kept);

    this.#days = Object.fromEntries(
      Object.entries(days).filter(([date, ids]) => date >= oldest && ids.length > 0)
    );
    writeDevice(key, JSON.stringify(this.#days));
  }
}

export const skipped = new SkippedMeals();
