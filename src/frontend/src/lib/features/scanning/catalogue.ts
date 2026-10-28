import { http, ok, request, type Result } from '$api';

import type { Label } from '$features/nutrition/stores/householdFoods.svelte';

/**
 * A packaged product as Open Food Facts describes it, per 100 g: crowd-sourced, so always shown with its source and
 * its doubts, and kept by the household only when a person takes it over (`docs/app-structure-design.md` §6.3).
 * The server asks Open Food Facts, never the browser, and only when the administrator switched it on.
 */
export interface CatalogueProduct {
  readonly gtin: string;
  readonly name: string;
  readonly brand: string | null;
  /** What the pack says it holds, as printed: "450 g". */
  readonly quantity: string | null;
  readonly packGrams: number | null;
  readonly servingGrams: number | null;
  /** The label's values; null is what nobody entered there. */
  readonly label: Omit<Label, 'name'>;
  /** What to check before taking it over (`noEnergy`, `macrosMissing`, `energyMismatch`, `unitMismatch`, `impossible`). */
  readonly doubts: readonly string[];
  /** The product's page at Open Food Facts, for the attribution. */
  readonly url: string;
}

interface Wire {
  gtin: string;
  name: string;
  brand?: string | null;
  quantity?: string | null;
  packGrams?: number | null;
  servingGrams?: number | null;
  energyKj?: number | null;
  energyKcal?: number | null;
  fat?: number | null;
  saturatedFat?: number | null;
  carbohydrate?: number | null;
  sugars?: number | null;
  protein?: number | null;
  salt?: number | null;
  doubts: string[];
  url: string;
}

const toProduct = (wire: Wire): CatalogueProduct => ({
  gtin: wire.gtin,
  name: wire.name,
  brand: wire.brand ?? null,
  quantity: wire.quantity ?? null,
  packGrams: wire.packGrams ?? null,
  servingGrams: wire.servingGrams ?? null,
  label: {
    energyKj: wire.energyKj ?? null,
    // A product without kilocalories carries the doubt `noEnergy` and is not taken over as it is.
    energyKcal: wire.energyKcal ?? 0,
    fat: wire.fat ?? null,
    saturatedFat: wire.saturatedFat ?? null,
    carbohydrate: wire.carbohydrate ?? null,
    sugars: wire.sugars ?? null,
    protein: wire.protein ?? null,
    salt: wire.salt ?? null
  },
  doubts: wire.doubts,
  url: wire.url
});

/** The answers that mean "nothing to show from Open Food Facts", which the sheet reads as "type the label". */
const notThere = new Set(['products.not_in_catalogue', 'products.catalogue_off']);

/** One barcode at Open Food Facts: the product, null when it is not there to show, or why it could not be asked. */
export async function lookupCatalogue(gtin: string): Promise<Result<CatalogueProduct | null>> {
  const result = await request(() =>
    http.GET('/api/v1/products/{gtin}', { params: { path: { gtin } } })
  );

  if (result.ok) {
    return ok(toProduct(result.value));
  }

  return notThere.has(result.error.code) ? ok(null) : result;
}

/** One name search at Open Food Facts, sent only when a person asks for it: its limit rules out searching as one types. */
export async function searchCatalogue(query: string): Promise<Result<CatalogueProduct[]>> {
  const result = await request(() =>
    http.GET('/api/v1/products', { params: { query: { q: query.trim() } } })
  );

  return result.ok ? ok(result.value.items.map(toProduct)) : result;
}

/** A product's name as people say it in a list: the brand before it, unless the name already says it. */
export function productTitle(product: Pick<CatalogueProduct, 'name' | 'brand'>): string {
  const { name, brand } = product;

  return brand && !name.toLowerCase().includes(brand.toLowerCase()) ? `${brand} ${name}` : name;
}
