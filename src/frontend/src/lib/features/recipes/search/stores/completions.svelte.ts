import { http, request } from '$api';

import { toCompletion } from '../../mappers';
import type { Completion } from '../../types';

/** Completions for one search field, created per field (two fields into one list would show each other's answers); a failed fetch just hides the list. */
export function createCompletionStore() {
  let items = $state<Completion[]>([]);

  // A shorter prefix answers later, so without a token the list would settle on the slowest request.
  // Plain fields, not $state: reading them must not wake an effect.
  let token = 0;
  let reading: AbortController | null = null;

  return {
    get items(): readonly Completion[] {
      return items;
    },

    async complete(householdId: string, query: string): Promise<void> {
      const mine = ++token;

      reading?.abort();
      reading = new AbortController();

      if (query.trim().length < 2) {
        items = [];

        return;
      }

      const result = await request(() =>
        http.GET('/api/v1/households/{householdId}/completions', {
          signal: reading?.signal,
          params: { path: { householdId }, query: { query } }
        })
      );

      if (mine !== token) {
        return;
      }

      items = result.ok
        ? result.value.items.map(toCompletion).filter((one): one is Completion => one !== null)
        : [];
    },

    clear(): void {
      token += 1;
      reading?.abort();
      items = [];
    }
  };
}
