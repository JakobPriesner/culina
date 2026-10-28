import { http, ok, request, type Result } from '$api';
import { toFood, type HouseholdFood } from '$features/nutrition/stores/householdFoods.svelte';

/** What a household says a barcode is: a mapping from a pack to an ingredient, never what is at home. */
export interface Product {
  /** The normalised digits, as the server keeps them. */
  readonly gtin: string;
  readonly name: string;
  /** What a recipe line or list item is matched by; null when the household gave none. */
  readonly ingredient: string | null;
  readonly quantity: number | null;
  readonly unit: string | null;
  /** Minutes the household found it needs. A barcode never supplies one. */
  readonly cookMinutes: number | null;
  readonly note: string | null;
  /** The household whose memory it is: the one asked, or one it inherits from. */
  readonly householdId: string;
  readonly version: number;
  /** The food the household kept from the pack's label, with its values; null when nobody typed or took them over. */
  readonly food: HouseholdFood | null;
}

interface Wire {
  gtin: string;
  name: string;
  ingredient?: string | null;
  quantity?: number | null;
  unit?: string | null;
  cookMinutes?: number | null;
  note?: string | null;
  householdId: string;
  version: number;
  food?: Parameters<typeof toFood>[0] | null;
}

const toProduct = (wire: Wire): Product => ({
  gtin: wire.gtin,
  name: wire.name,
  ingredient: wire.ingredient ?? null,
  quantity: wire.quantity ?? null,
  unit: wire.unit ?? null,
  cookMinutes: wire.cookMinutes ?? null,
  note: wire.note ?? null,
  householdId: wire.householdId,
  version: wire.version,
  food: wire.food ? toFood(wire.food) : null
});

const unknown = 'products.unknown';

/**
 * Looks a barcode up in the household's memory: the product, or null when nobody named it yet (which is an answer,
 * not a failure; the sheet then asks for a name).
 */
export async function lookupProduct(
  householdId: string,
  gtin: string
): Promise<Result<Product | null>> {
  const result = await request(() =>
    http.GET('/api/v1/households/{householdId}/products/{gtin}', {
      params: { path: { householdId, gtin } }
    })
  );

  if (result.ok) {
    return ok(toProduct(result.value));
  }

  return result.error.code === unknown ? ok(null) : result;
}

/**
 * Names a barcode nobody in the household has named: one text field, kept as both the name and the ingredient it
 * counts as, so it matches recipe lines and list items by the words the household uses. A barcode named meanwhile by
 * another phone answers `products.already_named`; the sheet then reads it again.
 */
export async function nameProduct(
  householdId: string,
  gtin: string,
  name: string
): Promise<Result<Product>> {
  const text = name.trim();
  const result = await request(() =>
    http.PUT('/api/v1/households/{householdId}/products/{gtin}', {
      params: { path: { householdId, gtin } },
      body: { name: text, ingredient: text }
    })
  );

  return result.ok ? ok(toProduct(result.value)) : result;
}
