import { http, request } from '$api';

import { toCompletion } from '../../mappers';
import type { Completion } from '../../types';

/**
 * What a half-typed search could become, for one search field.
 *
 * Created per field rather than shared, like the picker's recipe store: two
 * fields completing into one list would each show the other's answer.
 *
 * Failing quietly is the right failure here. A completion that could not be
 * fetched is a list that does not appear, while the results — asked for
 * separately — still do; nothing is announced for the loss of a convenience.
 */
export function createCompletionStore() {
  let items = $state<Completion[]>([]);

  // A shorter prefix matches more and answers later, so without this the list
  // would settle on whatever the slowest request said. Plain fields, not
  // $state: they are bookkeeping, and reading them must not wake an effect.
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
