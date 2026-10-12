import { http, request, type AppError } from '$api';
import { registerStore } from '$shell/stores';

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

  get own(): readonly OwnNoun[] {
    return this.#own;
  }

  async load(householdId: string): Promise<void> {
    if (!householdId || this.#loadedFor === householdId) {
      return;
    }

    this.#loadedFor = householdId;

    const result = await request(() =>
      http.GET('/api/v1/households/{householdId}/ingredient-nouns', {
        params: { path: { householdId } }
      })
    );

    if (result.ok) {
      this.#own = [...result.value.items];

      return;
    }

    // Silent: names then change number only for the words Culina lists itself.
    this.#loadedFor = null;
  }

  /** Lists a word, or changes the plural of the one listed under the same singular. */
  async set(householdId: string, noun: OwnNoun): Promise<AppError | null> {
    const before = this.#own;

    this.#own = [...before.filter((listed) => !sameWord(listed.one, noun.one)), noun].sort((a, b) =>
      a.one.localeCompare(b.one)
    );

    const result = await request(() =>
      http.PUT('/api/v1/households/{householdId}/ingredient-nouns/{one}', {
        params: { path: { householdId, one: noun.one } },
        body: { many: noun.many }
      })
    );

    if (!result.ok) {
      this.#own = before;
    }

    return result.ok ? null : result.error;
  }

  async remove(householdId: string, one: string): Promise<AppError | null> {
    const before = this.#own;

    this.#own = before.filter((listed) => !sameWord(listed.one, one));

    const result = await request(() =>
      http.DELETE('/api/v1/households/{householdId}/ingredient-nouns/{one}', {
        params: { path: { householdId, one } }
      })
    );

    if (!result.ok) {
      this.#own = before;
    }

    return result.ok ? null : result.error;
  }

  reset(): void {
    this.#own = [];
    this.#loadedFor = null;
  }
}

export const nouns = new NounStore();

registerStore(() => nouns.reset());
