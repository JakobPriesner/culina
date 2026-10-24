import { untrack } from 'svelte';
import { SvelteMap } from 'svelte/reactivity';

import { http, request, type AppError } from '$api';
import { LatestRead, registerStore, type LoadStatus } from '$shell/stores';

import { dayWith, sameThing } from '../dayTotals';
import type { DiaryDay, DiaryEntry, DiaryEntryChange, DiaryEntryRequest } from '../types';

export type DiaryWrite = { ok: true; entry: DiaryEntry } | { ok: false; error: AppError };

/**
 * The days of this person's diary that were read, by date. Writes change the day on screen at once and keep it in
 * step with what the server answers; the totals are added up here as the server adds them (see `dayWith`).
 */
class DiaryStore {
  #days = new SvelteMap<string, DiaryDay>();
  #status = $state<LoadStatus>('idle');
  #error = $state<AppError | null>(null);
  #reads = new LatestRead();

  get status(): LoadStatus {
    return this.#status;
  }

  get error(): AppError | null {
    return this.#error;
  }

  day(date: string): DiaryDay | null {
    return this.#days.get(date) ?? null;
  }

  /** Reads the days from `from` to `to`, inclusive. */
  async load(from: string, to: string = from): Promise<void> {
    const isLatest = this.#reads.start();

    // Untracked: an effect calls this, and must not come to depend on the days this read writes (it asked again on
    // every answer that held the day, as fast as the server could answer, until rate limiting refused it).
    if (!untrack(() => this.#days.has(to))) {
      this.#status = 'loading';
    }

    const result = await request(() =>
      http.GET('/api/v1/users/me/diary', { params: { query: { from, to } } })
    );

    if (!isLatest()) {
      return;
    }

    if (result.ok) {
      for (const day of result.value.days) {
        this.#days.set(day.date, day);
      }

      this.#error = null;
      this.#status = 'ready';
    } else {
      this.#error = result.error;
      this.#status = 'failed';
    }
  }

  async add(entry: DiaryEntryRequest): Promise<DiaryWrite> {
    const result = await request(() =>
      http.POST('/api/v1/users/me/diary/entries', { body: entry })
    );

    if (!result.ok) {
      return { ok: false, error: result.error };
    }

    this.#put(result.value.entry);

    return { ok: true, entry: result.value.entry };
  }

  /** Takes the entry off the day at once; a failure puts it back. The undo logs it again (`requestOf`). */
  async remove(entry: DiaryEntry): Promise<AppError | null> {
    this.#drop(entry.entryId, entry.date);

    const result = await request(() =>
      http.DELETE('/api/v1/users/me/diary/entries/{entryId}', {
        params: { path: { entryId: entry.entryId } }
      })
    );

    if (!result.ok) {
      this.#put(entry);

      return result.error;
    }

    return null;
  }

  /** A new meal or day moves the entry with its figures; a new amount is looked up again. */
  async change(entry: DiaryEntry, change: DiaryEntryChange): Promise<DiaryWrite> {
    const result = await request(() =>
      http.PATCH('/api/v1/users/me/diary/entries/{entryId}', {
        params: { path: { entryId: entry.entryId } },
        body: change
      })
    );

    if (!result.ok) {
      return { ok: false, error: result.error };
    }

    this.#drop(entry.entryId, entry.date);
    this.#put(result.value.entry);

    return { ok: true, entry: result.value.entry };
  }

  /**
   * What this person ate most often in the days read, newest occurrence of each, most frequent first; never ranked
   * by energy. The quick path: most of what anyone eats, they ate before.
   */
  recent(limit = 8): DiaryEntry[] {
    // eslint-disable-next-line svelte/prefer-svelte-reactivity -- Counted and read within one call.
    const seen = new Map<string, { entry: DiaryEntry; count: number }>();
    const newestFirst = [...this.#days.values()]
      .sort((a, b) => b.date.localeCompare(a.date))
      .flatMap((day) => [...day.entries].reverse());

    for (const entry of newestFirst) {
      const key = sameThing(entry);
      const known = seen.get(key);

      seen.set(key, { entry: known?.entry ?? entry, count: (known?.count ?? 0) + 1 });
    }

    return [...seen.values()]
      .sort((a, b) => b.count - a.count)
      .slice(0, limit)
      .map((one) => one.entry);
  }

  /** The entries logged from one "made it", among the days read. */
  loggedFrom(cookLogEntryId: string): DiaryEntry[] {
    return [...this.#days.values()].flatMap((day) =>
      day.entries.filter((entry) => entry.cookLogEntryId === cookLogEntryId)
    );
  }

  #put(entry: DiaryEntry): void {
    const day = this.#days.get(entry.date);
    const others = (day?.entries ?? []).filter((one) => one.entryId !== entry.entryId);

    this.#days.set(entry.date, dayWith(entry.date, [...others, entry]));
  }

  #drop(entryId: string, date: string): void {
    const day = this.#days.get(date);

    if (day) {
      this.#days.set(
        date,
        dayWith(
          date,
          day.entries.filter((one) => one.entryId !== entryId)
        )
      );
    }
  }

  reset(): void {
    this.#days.clear();
    this.#status = 'idle';
    this.#error = null;
  }
}

export const diary = new DiaryStore();

registerStore(() => diary.reset());
