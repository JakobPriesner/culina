import { http, request, type AppError } from '$api';
import { ResetEpoch } from '$shell/optimistic';
import { LatestRead, registerStore } from '$shell/stores';

/** A word the household writes in both numbers: "1 Maultasche", "2 Maultaschen". */
export interface OwnNoun {
  readonly one: string;
  readonly many: string;
}

const sameWord = (one: string, other: string) => one.toLowerCase() === other.toLowerCase();

/**
 * The household's own ingredient words in both numbers, beyond the ones `nounForms` lists, so a scaled recipe says
 * "2 Maultaschen". Loaded once per household; a change shows at once and a refusal puts the list back.
 */
class NounStore {
  #own = $state<readonly OwnNoun[]>([]);

  /** Not `$state`, as in the units store: `load` reads then writes it, and an effect calling it would re-run. */
  #loadedFor: string | null = null;
  #reads = new LatestRead();
  #epoch = new ResetEpoch();

  get own(): readonly OwnNoun[] {
    return this.#own;
  }

  async load(householdId: string): Promise<void> {
    if (!householdId || this.#loadedFor === householdId) {
      return;
    }

    this.#loadedFor = householdId;

    const isLatest = this.#reads.start();

    const result = await request(() =>
      http.GET('/api/v1/households/{householdId}/ingredient-nouns', {
        params: { path: { householdId } }
      })
    );

    if (!isLatest()) {
      return;
    }

    if (result.ok) {
      this.#own = [...result.value.items];

      return;
    }

    // Silent: names then change number only for the words Culina lists itself.
    this.#loadedFor = null;
  }

  /** Lists a word, or changes the plural of the one listed under the same singular. */
  async set(householdId: string, noun: OwnNoun): Promise<AppError | null> {
    const previous = this.#own.find((listed) => sameWord(listed.one, noun.one));
    const undo = this.#epoch.guard(() => this.#put(noun.one, previous));

    this.#put(noun.one, noun);

    const result = await request(() =>
      http.PUT('/api/v1/households/{householdId}/ingredient-nouns/{one}', {
        params: { path: { householdId, one: noun.one } },
        body: { many: noun.many }
      })
    );

    if (!result.ok) {
      undo();
    }

    return result.ok ? null : result.error;
  }

  async remove(householdId: string, one: string): Promise<AppError | null> {
    const previous = this.#own.find((listed) => sameWord(listed.one, one));
    const undo = this.#epoch.guard(() => this.#put(one, previous));

    this.#put(one, undefined);

    const result = await request(() =>
      http.DELETE('/api/v1/households/{householdId}/ingredient-nouns/{one}', {
        params: { path: { householdId, one } }
      })
    );

    if (!result.ok) {
      undo();
    }

    return result.ok ? null : result.error;
  }

  /** Lists, replaces or (with undefined) drops the one word, leaving every other entry as it is now. */
  #put(one: string, noun: OwnNoun | undefined): void {
    const others = this.#own.filter((listed) => !sameWord(listed.one, one));

    this.#own = (noun ? [...others, noun] : others).sort((a, b) => a.one.localeCompare(b.one));
  }

  reset(): void {
    this.#reads.cancel();
    this.#epoch.bump();
    this.#own = [];
    this.#loadedFor = null;
  }
}

export const nouns = new NounStore();

registerStore(() => nouns.reset());
