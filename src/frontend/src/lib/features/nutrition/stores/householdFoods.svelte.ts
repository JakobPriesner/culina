import { http, ok, request, type Result } from '$api';
import { ResetEpoch, removeUndoable } from '$shell/optimistic';
import { LatestRead, registerStore } from '$shell/stores';

import type { FoodHit } from '../types';

/** A food a household typed from a package's label, per 100 g; null is what the label does not say. */
export interface HouseholdFood {
  readonly id: string;
  readonly code: string;
  readonly name: string;
  readonly energyKj: number;
  readonly energyKcal: number;
  readonly fat: number | null;
  readonly saturatedFat: number | null;
  readonly carbohydrate: number | null;
  readonly sugars: number | null;
  readonly protein: number | null;
  readonly salt: number | null;
  /** Whether this household typed it; one it inherits from otherwise, and only that one removes it. */
  readonly own: boolean;
}

/** What a label gives per 100 g: the kilocalories, and whatever else it prints. */
export type Label = Omit<HouseholdFood, 'id' | 'code' | 'own' | 'energyKj'> & {
  readonly energyKj: number | null;
};

type Wire = { [K in keyof HouseholdFood]?: HouseholdFood[K] | null } & {
  id: string;
  code: string;
  name: string;
  energyKj: number;
  energyKcal: number;
  own: boolean;
};

const toFood = (wire: Wire): HouseholdFood => ({
  id: wire.id,
  code: wire.code,
  name: wire.name,
  energyKj: wire.energyKj,
  energyKcal: wire.energyKcal,
  fat: wire.fat ?? null,
  saturatedFat: wire.saturatedFat ?? null,
  carbohydrate: wire.carbohydrate ?? null,
  sugars: wire.sugars ?? null,
  protein: wire.protein ?? null,
  salt: wire.salt ?? null,
  own: wire.own
});

/** A household food as the correction sheet offers it beside the table's. */
export const hitOf = (food: HouseholdFood): FoodHit => ({
  code: food.code,
  nameDe: food.name,
  nameEn: food.name,
  energyKcal: food.energyKcal,
  source: 'package'
});

/**
 * The foods a household typed from a package, for what the table does not have ("Kokosmilch light"); those of the
 * households it inherits from too. Loaded once per household; a new one is added as the server answers.
 */
class HouseholdFoodStore {
  #foods = $state<readonly HouseholdFood[]>([]);

  /** Not `$state`, as in the units store: `load` reads then writes it. */
  #loadedFor: string | null = null;
  #reads = new LatestRead();
  #epoch = new ResetEpoch();

  get foods(): readonly HouseholdFood[] {
    return this.#foods;
  }

  async load(householdId: string): Promise<void> {
    if (!householdId || this.#loadedFor === householdId) {
      return;
    }

    this.#loadedFor = householdId;

    const isLatest = this.#reads.start();

    const result = await request(() =>
      http.GET('/api/v1/households/{householdId}/foods', { params: { path: { householdId } } })
    );

    if (!isLatest()) {
      return;
    }

    if (result.ok) {
      this.#foods = result.value.items.map(toFood);

      return;
    }

    // Silent: the table's foods remain to choose from.
    this.#loadedFor = null;
  }

  async add(householdId: string, label: Label): Promise<Result<HouseholdFood>> {
    const result = await request(() =>
      http.POST('/api/v1/households/{householdId}/foods', {
        params: { path: { householdId } },
        body: label
      })
    );

    if (!result.ok) {
      return result;
    }

    const food = toFood(result.value);

    this.#foods = [...this.#foods, food].sort((a, b) => a.name.localeCompare(b.name));

    return ok(food);
  }

  /** Removes one, and with it every choice of it; a refusal puts it back. */
  async remove(householdId: string, id: string): Promise<Result<null>> {
    const gone = removeUndoable(this.#foods, (food) => food.id === id);
    const putBack = this.#epoch.guard(() => (this.#foods = gone.undo(this.#foods)));

    this.#foods = gone.items;

    const result = await request(() =>
      http.DELETE('/api/v1/households/{householdId}/foods/{foodId}', {
        params: { path: { householdId, foodId: id } }
      })
    );

    if (!result.ok) {
      putBack();

      return result;
    }

    return ok(null);
  }

  reset(): void {
    this.#reads.cancel();
    this.#epoch.bump();
    this.#foods = [];
    this.#loadedFor = null;
  }
}

export const householdFoods = new HouseholdFoodStore();

registerStore(() => householdFoods.reset());
