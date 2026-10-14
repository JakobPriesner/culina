import {
  ask as askServer,
  clientError,
  ErrorCodes,
  http,
  request as send,
  watch,
  type AppError,
  type Stream
} from '$api';
import type { components } from '$api/generated/schema';
import { m } from '$shell/i18n';
import { registerStore } from '$shell/stores';

import type { Draft } from '../draftToRecipe';

/** The kinds that post JSON; a photograph posts multipart. */
export type DraftKind = 'idea' | 'text' | 'revision' | 'social';

/** The kinds the server writes as a job, which leaving the app does not stop (docs/native-app-design.md, part 8). */
type JobKind = 'idea' | 'revision';

interface Ask {
  kind: DraftKind;
  householdId: string;
  material?: string;
  recipeId?: string;
  language: string;
  transcript?: string;
}

interface Read {
  householdId: string;
  language: string;
  file: File;
}

interface DraftEventWire {
  draft: Draft;
  finished: boolean;
  problem?: { code: string; detail: string } | null;
}

type WireJob = components['schemas']['RecipesIntakeIntakeJob'];
type WireEvent = components['schemas']['RecipesIntakeIntakeEvent'];

/** The draft job this install is waiting for or showing, so a page finds it again after the app was put away or closed. */
interface Kept {
  id: string;
  kind: JobKind;
  recipeId?: string;
}

const keptKey = 'culina.drafts.job';

const isJobKind = (kind: DraftKind): kind is JobKind => kind === 'idea' || kind === 'revision';

/**
 * Asks the assistant for a recipe and shows it arriving: `draft` is the answer so far, `asking` says whether more is coming.
 * An idea or a revision is a server job followed on the intake stream, so a phone putting the app away does not cut it;
 * the page re-attaches with `resume`. One at a time, no cache: a second ask is a double press, and a kept draft could be
 * accepted into the wrong recipe, which `concerns` guards.
 */
class DraftStore {
  #draft = $state<Draft | null>(null);
  #asking = $state(false);
  #error = $state<AppError | null>(null);
  #material = $state<string | null>(null);
  #job = $state<Kept | null>(null);

  /** The stream being read; not `$state`, since only the draft it fills is rendered. */
  #stream: Stream | null = null;

  /** Ends the current ask, once. */
  #settle: ((error: AppError | null) => void) | null = null;
  #pending: Promise<AppError | null> | null = null;

  get draft(): Draft | null {
    return this.#draft;
  }

  get asking(): boolean {
    return this.#asking;
  }

  get error(): AppError | null {
    return this.#error;
  }

  /** What an idea said, for a page that re-attached to it and has an empty box. */
  get material(): string | null {
    return this.#material;
  }

  /** Whether there is a draft of this kind to show or find again, so a page can open the form that shows it. */
  remembers(kind: JobKind, recipeId?: string): boolean {
    const kept = readKept();

    return (
      this.concerns(kind, recipeId) ||
      (kept !== null && kept.kind === kind && (kind !== 'revision' || kept.recipeId === recipeId))
    );
  }

  /** Whether the store's draft is this page's: an idea, or a rewrite of this recipe. */
  concerns(kind: JobKind, recipeId?: string): boolean {
    const job = this.#job;

    return job !== null && job.kind === kind && (kind !== 'revision' || job.recipeId === recipeId);
  }

  ask(request: Ask): Promise<AppError | null> {
    if (isJobKind(request.kind)) {
      return this.#start({ ...request, kind: request.kind });
    }

    return this.#follow(
      '/api/v1/recipe-drafts',
      JSON.stringify({
        kind: request.kind,
        householdId: request.householdId,
        material: request.material,
        transcript: request.transcript,
        recipeId: request.recipeId,
        language: request.language
      })
    );
  }

  /**
   * Finds this page's draft again: the one still being written, or the one this install was waiting for when the app was
   * put away or closed. Null when there is none, else what `ask` would have answered.
   */
  resume(kind: JobKind, recipeId?: string): Promise<AppError | null> | null {
    if (this.#job) {
      if (!this.concerns(kind, recipeId)) {
        return null;
      }

      if (this.#asking) {
        return this.#pending;
      }

      // The stream gave up while the job went on: follow it again.
      return this.#error?.code === ErrorCodes.offline && readKept()?.id === this.#job.id
        ? this.#attend(this.#job)
        : null;
    }

    const kept = readKept();

    if (!kept || kept.kind !== kind || (kind === 'revision' && kept.recipeId !== recipeId)) {
      return null;
    }

    this.#job = kept;

    return this.#attend(kept);
  }

  read(request: Read): Promise<AppError | null> {
    const body = new FormData();

    body.append('file', request.file);

    const query = new URLSearchParams({
      householdId: request.householdId,
      language: request.language
    });

    return this.#follow(`/api/v1/recipe-drafts/photographs?${query}`, body);
  }

  /** All screenshots and their caption form one read job and one budget reservation. */
  readMedia(request: {
    householdId: string;
    language: string;
    photos: readonly File[];
    material: string;
    transcript: string;
  }): Promise<AppError | null> {
    const body = new FormData();
    for (const file of request.photos) body.append('photos', file);
    body.append('material', request.material);
    body.append('transcript', request.transcript);
    const query = new URLSearchParams({
      householdId: request.householdId,
      language: request.language
    });
    return this.#follow(`/api/v1/recipe-drafts/media?${query}`, body);
  }

  /** Done with the draft, accepted or not; a job still being written stops at its next step. */
  dismiss(): void {
    const job = this.#job;

    this.#forget();

    if (job) {
      void send(() =>
        http.POST('/api/v1/recipe-intakes/{id}/reviewed', { params: { path: { id: job.id } } })
      );
    }
  }

  /** On signing out: the job is the server's to keep or drop, this install only forgets it. */
  reset(): void {
    this.#forget();
  }

  #forget(): void {
    this.#end(null);
    this.#job = null;
    this.#draft = null;
    this.#error = null;
    this.#material = null;
    removeKept();
  }

  #start(request: Ask & { kind: JobKind }): Promise<AppError | null> {
    if (this.#asking) {
      return Promise.resolve(null);
    }

    const job: Kept = {
      id: crypto.randomUUID(),
      kind: request.kind,
      ...(request.recipeId ? { recipeId: request.recipeId } : {})
    };

    this.#forget();
    this.#job = job;
    this.#asking = true;

    // Kept before it is asked for: an app closed in between finds it again, or learns there is none.
    writeKept(job);

    return (this.#pending = (async () => {
      const started = await send(() =>
        http.PUT('/api/v1/recipe-drafts/{id}', {
          params: { path: { id: job.id } },
          body: {
            kind: request.kind,
            householdId: request.householdId,
            material: request.material,
            recipeId: request.recipeId,
            language: request.language
          }
        })
      );

      if (this.#job?.id !== job.id) {
        return null;
      }

      // Refused before anything was queued; the job stays this page's, so the reason shows beside its button.
      if (!started.ok) {
        removeKept();
        this.#asking = false;
        this.#error = started.error;

        return started.error;
      }

      this.#apply(started.value);

      return this.#asking ? this.#attend(job) : this.#error;
    })());
  }

  /** Follows the job on the intake stream until it is ready or failed; closed while the page is hidden. */
  #attend(job: Kept): Promise<AppError | null> {
    this.#asking = true;

    const settled = new Promise<AppError | null>((resolve) => {
      // While the page is hidden nobody is looking: the stream closes, so the server announces "ready" instead.
      const hiddenOrShown = () => {
        if (document.visibilityState === 'hidden') {
          this.#stream?.close();
          this.#stream = null;
        } else if (!this.#stream && this.#job?.id === job.id) {
          this.#open(job);
        }
      };

      document.addEventListener('visibilitychange', hiddenOrShown);

      this.#settle = (error) => {
        document.removeEventListener('visibilitychange', hiddenOrShown);
        resolve(error);
      };
    });

    if (document.visibilityState !== 'hidden') {
      this.#open(job);
    }

    return (this.#pending = settled);
  }

  #open(job: Kept): void {
    this.#stream = watch<WireEvent>(`/api/v1/recipe-intakes/events?attending=${job.id}`, {
      message: (event) => {
        if (this.#job?.id !== job.id) {
          return;
        }

        const found = event.jobs.find((one) => one.id === job.id);

        if (found) {
          this.#apply(found);
        } else if (event.snapshot) {
          // Dismissed elsewhere, or long gone: nothing left to show.
          removeKept();
          this.#job = null;
          this.#draft = null;
          this.#end(null);
        }
      },
      failed: (error) => {
        // The stream gave up, not the job: it is still kept, and the page can find it again.
        this.#stream = null;
        this.#end(error);
      }
    });
  }

  #apply(job: WireJob): void {
    // Kept even alongside a failure: a stopped provider's partial draft was paid for.
    if (job.draft) {
      this.#draft = job.draft;
    }

    if (this.#material === null && job.material) {
      this.#material = job.material;
    }

    switch (job.stage) {
      case 'ready':
        this.#end(null);
        break;
      case 'failed':
        this.#end(
          clientError(job.errorCode ?? ErrorCodes.unexpected, m['error.unexpected.body']())
        );
        break;
      case 'reviewed':
        removeKept();
        this.#job = null;
        this.#draft = null;
        this.#end(null);
        break;
    }
  }

  /** Stops following and answers the ask. */
  #end(error: AppError | null): void {
    this.#stream?.close();
    this.#stream = null;
    this.#asking = false;
    this.#error = error;

    const settle = this.#settle;

    this.#settle = null;
    settle?.(error);
  }

  #follow(path: string, body: BodyInit): Promise<AppError | null> {
    if (this.#asking) {
      return Promise.resolve(null);
    }

    this.#forget();
    this.#asking = true;

    return new Promise((resolve) => {
      // A stream ends once; a finished event and a dead connection can both arrive.
      let settled = false;
      // A phone suspends a page in the background, which cuts the stream; "offline" would be the wrong thing to say.
      let leftTheApp = false;
      const noticeLeaving = () => {
        if (document.visibilityState === 'hidden') {
          leftTheApp = true;
        }
      };

      document.addEventListener('visibilitychange', noticeLeaving);

      const settle = (error: AppError | null) => {
        if (settled) {
          return;
        }

        settled = true;
        document.removeEventListener('visibilitychange', noticeLeaving);
        this.#end(
          error?.code === ErrorCodes.offline && leftTheApp
            ? clientError(ErrorCodes.backgrounded, error.detail)
            : error
        );
        resolve(this.#error);
      };

      this.#stream = askServer<DraftEventWire>(path, body, {
        message: (event) => {
          // Kept even alongside a failure: a stopped provider's partial draft was paid for.
          this.#draft = event.draft;

          if (event.finished) {
            settle(event.problem ? clientError(event.problem.code, event.problem.detail) : null);
          }
        },
        failed: settle
      });
    });
  }
}

function readKept(): Kept | null {
  try {
    const kept = JSON.parse(localStorage.getItem(keptKey) ?? 'null') as Partial<Kept> | null;

    return kept && typeof kept.id === 'string' && (kept.kind === 'idea' || kept.kind === 'revision')
      ? {
          id: kept.id,
          kind: kept.kind,
          ...(typeof kept.recipeId === 'string' ? { recipeId: kept.recipeId } : {})
        }
      : null;
  } catch {
    return null;
  }
}

function writeKept(job: Kept): void {
  try {
    localStorage.setItem(keptKey, JSON.stringify(job));
  } catch {
    // Not kept: a closed app does not find it again, and the notification still says it is ready.
  }
}

function removeKept(): void {
  try {
    localStorage.removeItem(keptKey);
  } catch {
    // Nothing kept to remove.
  }
}

export const createDraftStore = (): DraftStore => new DraftStore();
export const drafts = createDraftStore();

registerStore(() => drafts.reset());
