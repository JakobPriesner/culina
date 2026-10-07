import { http, request, type AppError } from '$api';
import { registerStore, type LoadStatus } from '$shell/stores';

import type { Unit } from '$features/recipes/units';

import { sectionOrder, type Section } from '../sections';

import type { components } from '$api/generated/schema';

/** One household's list. Every change returns the whole list and the store takes the server's answer: two people can add at once. */
export type ShoppingItem = components['schemas']['ShoppingItemContract'];
type ShoppingList = components['schemas']['ShoppingResponse'];

export interface SectionGroup {
  readonly section: Section;
  readonly items: readonly ShoppingItem[];
}

class ShoppingStore {
  #list = $state<ShoppingList | null>(null);
  #status = $state<LoadStatus>('idle');

  /** Plain, not $state: read before the first await of an effect-called method, where a tracked read would retrigger on its own writes. */
  #householdId: string | null = null;
  #error = $state<AppError | null>(null);

  get householdId() {
    return this.#list ? this.#householdId : null;
  }

  get status() {
    return this.#status;
  }

  get error(): AppError | null {
    return this.#error;
  }

  get items(): readonly ShoppingItem[] {
    return this.#list?.items ?? [];
  }

  /** Still to buy, grouped in shop order; empty sections omitted. */
  get toBuy(): readonly SectionGroup[] {
    const remaining = this.items.filter((item) => !item.isChecked);

    return sectionOrder
      .map((section) => ({
        section,
        items: remaining.filter((item) => item.section === section)
      }))
      .filter((group) => group.items.length > 0);
  }

  /** Already in the trolley; kept visible because putting one back is common. */
  get bought(): readonly ShoppingItem[] {
    return this.items.filter((item) => item.isChecked);
  }

  async load(householdId: string): Promise<void> {
    // Don't keep showing another household's list: a tick in that moment would hit the wrong list.
    if (this.#householdId !== householdId) {
      this.#householdId = householdId;
      this.#list = null;
    }

    this.#status = 'loading';
    this.#error = null;

    const result = await request(() =>
      http.GET('/api/v1/households/{householdId}/shopping-list', {
        params: { path: { householdId } }
      })
    );

    if (result.ok) {
      this.#list = result.value;
      this.#status = 'ready';
    } else {
      this.#error = result.error;
      this.#status = 'failed';
    }
  }

  async add(
    householdId: string,
    name: string,
    quantity?: number,
    unit?: Unit | null
  ): Promise<void> {
    const result = await request(() =>
      http.POST('/api/v1/households/{householdId}/shopping-list/items', {
        params: { path: { householdId } },
        body: { name, quantity, unit: unit ?? undefined }
      })
    );

    this.#take(result.ok ? result.value : null, result.ok ? null : result.error);
  }

  /** Optimistic: in a shop the round trip is slowest and least forgiven; the server's answer replaces the tick. */
  async check(householdId: string, itemId: string, isChecked: boolean): Promise<void> {
    const failure = await this.#change(householdId, itemId, { isChecked });

    if (failure) {
      this.#error = failure;
    }
  }

  /** Moves a line to another shop section and remembers it for that name; optimistic like a tick. */
  async moveToSection(
    householdId: string,
    itemId: string,
    section: Section
  ): Promise<AppError | null> {
    return this.#change(householdId, itemId, { section });
  }

  async remove(householdId: string, itemId: string): Promise<void> {
    const result = await request(() =>
      http.DELETE('/api/v1/households/{householdId}/shopping-list/items', {
        params: { path: { householdId }, query: { itemId } }
      })
    );

    this.#take(result.ok ? result.value : null, result.ok ? null : result.error);
  }

  async clearBought(householdId: string): Promise<void> {
    const result = await request(() =>
      http.DELETE('/api/v1/households/{householdId}/shopping-list/items', {
        params: { path: { householdId } }
      })
    );

    this.#take(result.ok ? result.value : null, result.ok ? null : result.error);
  }

  /** Puts a recipe's ingredients on, at the scaling being cooked. */
  async addRecipe(
    householdId: string,
    recipeId: string,
    servings: number
  ): Promise<AppError | null> {
    const result = await request(() =>
      http.POST('/api/v1/households/{householdId}/shopping-list/recipes', {
        params: { path: { householdId } },
        body: { recipeId, servings }
      })
    );

    this.#take(result.ok ? result.value : null, result.ok ? null : result.error);

    return result.ok ? null : result.error;
  }

  /** One request for the whole week: only the server knows which meals are already here, so nothing is bought twice. */
  async addPlannedWeek(householdId: string, from: string): Promise<AppError | null> {
    const result = await request(() =>
      http.POST('/api/v1/households/{householdId}/shopping-list/meals', {
        params: { path: { householdId } },
        body: { from }
      })
    );

    this.#take(result.ok ? result.value : null, result.ok ? null : result.error);

    return result.ok ? null : result.error;
  }

  /** Takes exactly what one planned meal put on the list back off it. */
  async withdrawMeal(householdId: string, entryId: string): Promise<AppError | null> {
    const result = await request(() =>
      http.DELETE('/api/v1/households/{householdId}/shopping-list/meals/{entryId}', {
        params: { path: { householdId, entryId } }
      })
    );

    this.#take(result.ok ? result.value : null, result.ok ? null : result.error);

    return result.ok ? null : result.error;
  }

  clearError(): void {
    this.#error = null;
  }

  reset(): void {
    this.#householdId = null;
    this.#list = null;
    this.#status = 'idle';
    this.#error = null;
  }

  /** One line changed here first, then on the server, and put back exactly as it was if that fails. */
  async #change(
    householdId: string,
    itemId: string,
    change: Partial<Pick<ShoppingItem, 'isChecked' | 'section'>>
  ): Promise<AppError | null> {
    const before = this.#list;

    if (this.#list) {
      this.#list = {
        ...this.#list,
        items: this.#list.items.map((item) =>
          item.itemId === itemId ? { ...item, ...change } : item
        )
      };
    }

    const result = await request(() =>
      http.PATCH('/api/v1/households/{householdId}/shopping-list/items/{itemId}', {
        params: { path: { householdId, itemId } },
        body: change
      })
    );

    if (result.ok) {
      this.#list = result.value;

      return null;
    }

    // Exact restore, not an inverse: inverses drift if something else changed meanwhile.
    this.#list = before;

    return result.error;
  }

  #take(list: ShoppingList | null, error: AppError | null): void {
    if (list) {
      this.#list = list;
      this.#status = 'ready';
    }

    this.#error = error;
  }
}

export const shopping = new ShoppingStore();

registerStore(() => shopping.reset());
