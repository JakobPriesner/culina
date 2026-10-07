import type { ConnectedSource, ImportEvent, ImportOutcome, SourceRecipe } from '../types';

export const toSource = (wire: {
  sourceId: string;
  kind: string;
  label: string;
  address: string;
  createdAt: string;
  lastUsedAt?: string | null;
}): ConnectedSource => ({
  sourceId: wire.sourceId,
  kind: wire.kind as ConnectedSource['kind'],
  label: wire.label,
  address: wire.address,
  createdAt: wire.createdAt,
  lastUsedAt: wire.lastUsedAt ?? null
});

export const toRecipe = (wire: {
  externalId: string;
  title: string;
  description?: string | null;
  totalMinutes?: number | null;
  alreadyHere?: string | null;
}): SourceRecipe => ({
  externalId: wire.externalId,
  title: wire.title,
  description: wire.description ?? null,
  totalMinutes: wire.totalMinutes ?? null,
  alreadyHere: wire.alreadyHere ?? null
});

interface ImportedRecipeWire {
  externalId: string;
  outcome: string;
  recipeId?: string | null;
  title?: string | null;
  reason?: string | null;
  looksLike?: { title: string; sharedIngredients: number; cookCount: number } | null;
}

/** One event as the stream sends it. */
export interface ImportEventWire {
  recipe?: ImportedRecipeWire | null;
  done: number;
  total: number;
  finished: boolean;
}

const toOutcome = (wire: ImportedRecipeWire): ImportOutcome => ({
  externalId: wire.externalId,
  outcome: wire.outcome as ImportOutcome['outcome'],
  recipeId: wire.recipeId ?? null,
  title: wire.title ?? null,
  reason: wire.reason ?? null,
  looksLike: wire.looksLike
    ? {
        title: wire.looksLike.title,
        sharedIngredients: wire.looksLike.sharedIngredients,
        cookCount: wire.looksLike.cookCount
      }
    : null
});

export const toEvent = (wire: ImportEventWire): ImportEvent => ({
  recipe: wire.recipe ? toOutcome(wire.recipe) : null,
  done: wire.done,
  total: wire.total,
  finished: wire.finished
});
