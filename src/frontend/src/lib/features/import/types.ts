/**
 * Bringing a whole library over from another app.
 *
 * Kept apart from `$features/recipes` on purpose. Everything here is about a
 * place that is not this app — a connection, its token, their recipes — and the
 * moment one of their recipes becomes one of ours it stops belonging to this
 * feature entirely. That boundary is what stops "imported recipe" from becoming
 * a second kind of recipe.
 */

/** Which app a connection reads. */
export type SourceKind = 'tandoor';

/** A library this household has connected. */
export interface ConnectedSource {
  readonly sourceId: string;
  readonly kind: SourceKind;
  readonly label: string;
  readonly address: string;
  readonly createdAt: string;
  /** Null until recipes have actually been brought over. */
  readonly lastUsedAt: string | null;
}

/** One of their recipes, as the picker draws it. */
export interface SourceRecipe {
  readonly externalId: string;
  readonly title: string;
  readonly description: string | null;
  readonly totalMinutes: number | null;
  /**
   * The recipe this one already is here.
   *
   * What makes coming back next month cheap: you see what is new rather than
   * the whole library again.
   */
  readonly alreadyHere: string | null;
}

/** What happened to one recipe in one batch. */
export interface ImportOutcome {
  readonly externalId: string;
  readonly outcome: 'imported' | 'already_here' | 'failed';
  readonly recipeId: string | null;
  readonly title: string | null;
  readonly reason: string | null;
}

/**
 * An import as it is running, and after it has finished.
 *
 * Counted in recipes rather than in requests, because recipes are what somebody
 * asked for. The `failures` list is kept by name: twelve that could not be read
 * is a number, and twelve titles is something you can go and look at.
 */
export interface ImportRun {
  readonly total: number;
  readonly done: number;
  readonly imported: number;
  readonly skipped: number;
  readonly failures: readonly string[];
  readonly cookbookId: string | null;
  readonly cookbookName: string | null;
  readonly finished: boolean;
}
