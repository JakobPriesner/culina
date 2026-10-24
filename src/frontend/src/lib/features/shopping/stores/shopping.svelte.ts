import { http, request, type AppError } from '$api';
import { LatestRead, registerStore, type LoadStatus } from '$shell/stores';

import { foldText } from '$features/recipes/foldText';
import type { Unit } from '$features/recipes/units';

import { groupsToBuy } from '../lineRuns';
import type { Section } from '../sections';

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

  /** Plain counter of tick writes still in flight; a whole-list answer is only taken once none are left. */
  #inFlight = 0;

  /** Per line with a write out: what the server last confirmed, and how many writes are out. A failed write goes back to this, never to the optimistic value a neighbouring tick left. */
  #confirmed = new Map<string, { item: ShoppingItem; writes: number }>();

  /** Plain, not $state: read before the first await of an effect-called method, where a tracked read would retrigger on its own writes. */
  #householdId: string | null = null;
  #reads = new LatestRead();
  #error = $state<AppError | null>(null);
  #arrived = $state<readonly string[]>([]);
  #arrivedTimer: ReturnType<typeof setTimeout> | undefined;

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

  /**
   * Still to buy, grouped in shop order; empty sections omitted. Within a section, lines of the same
   * thing (kept apart because their units cannot be added) sit together, where the first of them is.
   */
  get toBuy(): readonly SectionGroup[] {
    return groupsToBuy(this.items);
  }

  /** Already in the trolley; kept visible because putting one back is common. */
  get bought(): readonly ShoppingItem[] {
    return this.items.filter((item) => item.isChecked);
  }

  /** Lines a plan or a recipe has just put here, or whose amount it has just changed; empty again after a moment. */
  get arrived(): readonly string[] {
    return this.#arrived;
  }

  /** Lines still to find, as the list's heading and Woche's switch say it. */
  get remaining(): number {
    return this.items.length - this.bought.length;
  }

  /** The line of this name still to buy, whichever recipe (or nobody) put it there. */
  itemNamed(name: string): ShoppingItem | undefined {
    const folded = foldText(name.trim());

    return this.items.find((item) => !item.isChecked && foldText(item.name.trim()) === folded);
  }

  /** Whether something of this name is still to buy. */
  onTheList(name: string): boolean {
    return this.itemNamed(name) !== undefined;
  }

  /** Whether a recipe still has lines to buy. `sources` can be missing from a list cached before it existed. */
  holdsRecipe(recipeId: string): boolean {
    return this.items.some(
      (item) =>
        !item.isChecked && (item.sources ?? []).some((source) => source.recipeId === recipeId)
    );
  }

  async load(householdId: string): Promise<void> {
    // Don't keep showing another household's list: a tick in that moment would hit the wrong list.
    if (this.#householdId !== householdId) {
      this.#householdId = householdId;
      this.#list = null;
    }

    const isLatest = this.#reads.start();

    this.#status = 'loading';
    this.#error = null;

    const result = await request(() =>
      http.GET('/api/v1/households/{householdId}/shopping-list', {
        params: { path: { householdId } }
      })
    );

    // Another household was asked for since; its answer is the one to show.
    if (!isLatest()) {
      return;
    }

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

    this.#take(householdId, result.ok ? result.value : null, result.ok ? null : result.error);
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

    this.#take(householdId, result.ok ? result.value : null, result.ok ? null : result.error);
  }

  async clearBought(householdId: string): Promise<void> {
    const result = await request(() =>
      http.DELETE('/api/v1/households/{householdId}/shopping-list/items', {
        params: { path: { householdId } }
      })
    );

    this.#take(householdId, result.ok ? result.value : null, result.ok ? null : result.error);
  }

  /** Puts a recipe's ingredients on, at the scaling being cooked, but for the ones already in the cupboard (`except`). */
  async addRecipe(
    householdId: string,
    recipeId: string,
    servings: number,
    except: readonly string[] = []
  ): Promise<AppError | null> {
    const result = await request(() =>
      http.POST('/api/v1/households/{householdId}/shopping-list/recipes', {
        params: { path: { householdId } },
        body: { recipeId, servings, exceptIngredientIds: except.length ? [...except] : undefined }
      })
    );

    this.#takeArrivals(
      householdId,
      result.ok ? result.value : null,
      result.ok ? null : result.error
    );

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

    this.#takeArrivals(
      householdId,
      result.ok ? result.value : null,
      result.ok ? null : result.error
    );

    return result.ok ? null : result.error;
  }

  /** One planned meal of the week starting `from`, as when it is planned after the week was shopped; `except` as for a recipe. */
  async addPlannedMeal(
    householdId: string,
    from: string,
    entryId: string,
    except: readonly string[] = []
  ): Promise<AppError | null> {
    const result = await request(() =>
      http.POST('/api/v1/households/{householdId}/shopping-list/meals', {
        params: { path: { householdId } },
        body: { from, entryId, exceptIngredientIds: except.length ? [...except] : undefined }
      })
    );

    this.#takeArrivals(
      householdId,
      result.ok ? result.value : null,
      result.ok ? null : result.error
    );

    return result.ok ? null : result.error;
  }

  /** Writes one planned meal's share again at what the plan now says (its servings and day); a meal not on the list stays off it. */
  async refreshMeal(householdId: string, entryId: string): Promise<AppError | null> {
    const result = await request(() =>
      http.PUT('/api/v1/households/{householdId}/shopping-list/meals/{entryId}', {
        params: { path: { householdId, entryId } }
      })
    );

    this.#takeArrivals(
      householdId,
      result.ok ? result.value : null,
      result.ok ? null : result.error
    );

    return result.ok ? null : result.error;
  }

  /** Takes exactly what one planned meal put on the list back off it. */
  async withdrawMeal(householdId: string, entryId: string): Promise<AppError | null> {
    const result = await request(() =>
      http.DELETE('/api/v1/households/{householdId}/shopping-list/meals/{entryId}', {
        params: { path: { householdId, entryId } }
      })
    );

    this.#take(householdId, result.ok ? result.value : null, result.ok ? null : result.error);

    return result.ok ? null : result.error;
  }

  /** Takes back exactly what a recipe added by itself (not for a planned meal) put on the list. */
  async withdrawRecipe(householdId: string, recipeId: string): Promise<AppError | null> {
    const result = await request(() =>
      http.DELETE('/api/v1/households/{householdId}/shopping-list/recipes/{recipeId}', {
        params: { path: { householdId, recipeId } }
      })
    );

    this.#take(householdId, result.ok ? result.value : null, result.ok ? null : result.error);

    return result.ok ? null : result.error;
  }

  /** Sets how much of a line to buy, in its own unit; the server's answer replaces the list. */
  async setQuantity(
    householdId: string,
    itemId: string,
    quantity: number
  ): Promise<AppError | null> {
    const result = await request(() =>
      http.PATCH('/api/v1/households/{householdId}/shopping-list/items/{itemId}', {
        params: { path: { householdId, itemId } },
        body: { quantity }
      })
    );

    this.#take(householdId, result.ok ? result.value : null, result.ok ? null : result.error);

    return result.ok ? null : result.error;
  }

  clearError(): void {
    this.#error = null;
  }

  reset(): void {
    this.#reads.cancel();
    this.#confirmed.clear();
    this.#householdId = null;
    this.#list = null;
    this.#status = 'idle';
    this.#error = null;
    clearTimeout(this.#arrivedTimer);
    this.#arrived = [];
  }

  /** One line changed here first, then on the server; if that fails only this line is put back, so other ticks meanwhile stay. */
  async #change(
    householdId: string,
    itemId: string,
    change: Partial<Pick<ShoppingItem, 'isChecked' | 'section'>>
  ): Promise<AppError | null> {
    const current = this.#list?.items.find((item) => item.itemId === itemId);
    const held = this.#confirmed.get(itemId) ?? (current ? { item: current, writes: 0 } : null);

    if (held) {
      held.writes++;
      this.#confirmed.set(itemId, held);
    }

    this.#inFlight++;

    this.#replace(itemId, change);

    const result = await request(() =>
      http.PATCH('/api/v1/households/{householdId}/shopping-list/items/{itemId}', {
        params: { path: { householdId, itemId } },
        body: change
      })
    );

    this.#inFlight--;

    if (held && --held.writes === 0) {
      this.#confirmed.delete(itemId);
    }

    if (result.ok) {
      const answered = result.value.items.find((item) => item.itemId === itemId);

      if (held && answered) {
        held.item = answered;
      }

      // Another tick still out would be undone by this list; the last answer to land carries all of them.
      if (this.#inFlight === 0 && householdId === this.#householdId) {
        this.#list = result.value;
      }

      return null;
    }

    // Back to what the server last confirmed, not an inverse and not the value before this tick: a tick then untick that both fail must end where they began.
    // While another write to this line is out, its answer or failure settles the line.
    if (held && held.writes === 0) {
      this.#replace(
        itemId,
        Object.fromEntries(
          Object.keys(change).map((key) => [key, held.item[key as keyof typeof change]])
        )
      );
    }

    return result.error;
  }

  #replace(itemId: string, change: Partial<ShoppingItem>): void {
    if (this.#list) {
      this.#list = {
        ...this.#list,
        items: this.#list.items.map((item) =>
          item.itemId === itemId ? { ...item, ...change } : item
        )
      };
    }
  }

  /**
   * As `#take`, for a write that puts a plan's or a recipe's lines here: what arrived (or changed amount) is kept for
   * a moment, so a list on screen can show it instead of a toast saying it.
   */
  #takeArrivals(householdId: string, list: ShoppingList | null, error: AppError | null): void {
    const before = new Map(this.items.map((item) => [item.itemId, item.quantity]));

    this.#take(householdId, list, error);

    const arrived = (list?.items ?? [])
      .filter(
        (item) =>
          !item.isChecked && (!before.has(item.itemId) || before.get(item.itemId) !== item.quantity)
      )
      .map((item) => item.itemId);

    if (arrived.length === 0) return;

    clearTimeout(this.#arrivedTimer);
    this.#arrived = arrived;
    this.#arrivedTimer = setTimeout(() => (this.#arrived = []), 2400);
  }

  #take(householdId: string, list: ShoppingList | null, error: AppError | null): void {
    // The household was switched meanwhile: this answer is for a list no longer shown.
    if (householdId !== this.#householdId) {
      return;
    }

    if (list) {
      this.#list = list;
      this.#status = 'ready';
    }

    this.#error = error;
  }
}

export const shopping = new ShoppingStore();

registerStore(() => shopping.reset());
