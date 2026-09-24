import { http, request, type AppError } from '$api';
import { registerStore, type LoadStatus } from '$shell/stores';

import type { Unit } from '$features/recipes/units';

import { sectionOrder, type Section } from '../sections';

import type { components } from '$api/generated/schema';

/**
 * One household's shopping list.
 *
 * Household-owned, so two people can add to it at once — which is why every
 * change sends the whole list back and the store takes the server's answer
 * rather than patching its own copy.
 */
export type ShoppingItem = components['schemas']['ShoppingItemContract'];
type ShoppingList = components['schemas']['ShoppingResponse'];

export interface SectionGroup {
  readonly section: Section;
  readonly items: readonly ShoppingItem[];
}

class ShoppingStore {
  #list = $state<ShoppingList | null>(null);
  #status = $state<LoadStatus>('idle');
  #error = $state<AppError | null>(null);

  get status() {
    return this.#status;
  }

  get error(): AppError | null {
    return this.#error;
  }

  get items(): readonly ShoppingItem[] {
    return this.#list?.items ?? [];
  }

  /** Still to buy, grouped in shop order. Empty sections are not shown. */
  get toBuy(): readonly SectionGroup[] {
    const remaining = this.items.filter((item) => !item.isChecked);

    return sectionOrder
      .map((section) => ({
        section,
        items: remaining.filter((item) => item.section === section)
      }))
      .filter((group) => group.items.length > 0);
  }

  /** Already in the trolley. Kept visible, because putting one back is common. */
  get bought(): readonly ShoppingItem[] {
    return this.items.filter((item) => item.isChecked);
  }

  async load(householdId: string): Promise<void> {
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

  /**
   * Ticks a line off, here first and on the server after.
   *
   * Standing in a shop is exactly where a round trip is most likely to be slow
   * and least likely to be forgiven, so the tick lands immediately and the
   * server's answer replaces it when it arrives.
   */
  async check(householdId: string, itemId: string, isChecked: boolean): Promise<void> {
    const before = this.#list;

    if (this.#list) {
      this.#list = {
        ...this.#list,
        items: this.#list.items.map((item) =>
          item.itemId === itemId ? { ...item, isChecked } : item
        )
      };
    }

    const result = await request(() =>
      http.PATCH('/api/v1/households/{householdId}/shopping-list/items/{itemId}', {
        params: { path: { householdId, itemId } },
        body: { isChecked }
      })
    );

    if (result.ok) {
      this.#list = result.value;
    } else {
      // Exactly what was there, not an inverse: inverses drift when something
      // else changed in between.
      this.#list = before;
      this.#error = result.error;
    }
  }

  async moveToSection(householdId: string, itemId: string, section: Section): Promise<void> {
    const result = await request(() =>
      http.PATCH('/api/v1/households/{householdId}/shopping-list/items/{itemId}', {
        params: { path: { householdId, itemId } },
        body: { section }
      })
    );

    this.#take(result.ok ? result.value : null, result.ok ? null : result.error);
  }

  async remove(householdId: string, itemId: string): Promise<void> {
    const result = await request(() =>
      http.DELETE('/api/v1/households/{householdId}/shopping-list/items', {
        params: { path: { householdId }, query: { itemId } }
      })
    );

    this.#take(result.ok ? result.value : null, result.ok ? null : result.error);
  }

  /** After a shop: the one bulk action worth having. */
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

  /**
   * Puts a planned week's meals on, each once.
   *
   * One request for the whole week rather than one per meal, because only the
   * server can see which meals are already here — and a week that is added
   * twice, or after one of its recipes was added from its own page, must not
   * be bought twice.
   */
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
    this.#list = null;
    this.#status = 'idle';
    this.#error = null;
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
