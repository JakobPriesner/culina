import type { AppError } from '$api';

/**
 * Importing a library from another app; kept apart from `$features/recipes` so an "imported recipe"
 * never becomes a second kind of recipe.
 */

export type SourceKind = 'tandoor';

export interface ConnectedSource {
  readonly sourceId: string;
  readonly kind: SourceKind;
  readonly label: string;
  readonly address: string;
  readonly createdAt: string;
  readonly lastUsedAt: string | null;
}

export interface SourceRecipe {
  readonly externalId: string;
  readonly title: string;
  readonly description: string | null;
  readonly totalMinutes: number | null;
  /** The recipe this one already is here, so a later import shows only what is new. */
  readonly alreadyHere: string | null;
}

export interface ImportOutcome {
  readonly externalId: string;
  readonly outcome: 'imported' | 'already_here' | 'looks_like' | 'failed';
  readonly recipeId: string | null;
  readonly title: string | null;
  readonly reason: string | null;
  readonly looksLike: ImportLookalike | null;
}

export interface ImportLookalike {
  readonly title: string;
  readonly sharedIngredients: number;
  readonly cookCount: number;
}

/** One of their recipes held back because the household has a similar one; nothing was written. */
export interface HeldRecipe {
  readonly externalId: string;
  readonly title: string;
  readonly recipeId: string;
  readonly looksLike: ImportLookalike;
}

/** One line of import progress; counts come with every event, so a missed one doesn't matter. */
export interface ImportEvent {
  readonly recipe: ImportOutcome | null;
  readonly done: number;
  readonly total: number;
  readonly finished: boolean;
}

/** An import while running and after; failures are kept by name so they can be looked up. */
export interface ImportRun {
  readonly total: number;
  readonly done: number;
  readonly imported: number;
  readonly skipped: number;
  readonly failures: readonly string[];
  readonly held: readonly HeldRecipe[];
  readonly cookbookId: string | null;
  readonly cookbookName: string | null;
  readonly finished: boolean;
  /**
   * Why this page stopped following the import, kept as the error (the import itself usually
   * continues server-side).
   */
  readonly lost: AppError | null;
}
