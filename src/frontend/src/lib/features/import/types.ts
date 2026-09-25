import type { AppError } from '$api';

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

/** What happened to one recipe in one import. */
export interface ImportOutcome {
  readonly externalId: string;
  readonly outcome: 'imported' | 'already_here' | 'looks_like' | 'failed';
  readonly recipeId: string | null;
  readonly title: string | null;
  readonly reason: string | null;
  /** What it looks like, for `looks_like`. */
  readonly looksLike: ImportLookalike | null;
}

/** A recipe already here that an imported one looks like. */
export interface ImportLookalike {
  readonly title: string;
  readonly sharedIngredients: number;
  /** How often the person importing has made it — why doubling it matters. */
  readonly cookCount: number;
}

/**
 * One of their recipes, held back because the household has one like it.
 *
 * Nothing about it was written, so bringing it over after all is simply
 * asking for it again — and until somebody does, it is not here.
 */
export interface HeldRecipe {
  readonly externalId: string;
  readonly title: string;
  /** The recipe here that it looks like. */
  readonly recipeId: string;
  readonly looksLike: ImportLookalike;
}

/**
 * One line of an import's progress, as the stream sends it.
 *
 * A recipe finished, or — when there is no recipe on it — either the run being
 * over or a tick saying it is still going. The counts come from the server on
 * every event, so a reader that missed one is still right.
 */
export interface ImportEvent {
  readonly recipe: ImportOutcome | null;
  readonly done: number;
  readonly total: number;
  readonly finished: boolean;
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
  /** Held back because each looks like a recipe already here, for somebody to decide. */
  readonly held: readonly HeldRecipe[];
  readonly cookbookId: string | null;
  readonly cookbookName: string | null;
  readonly finished: boolean;
  /**
   * Why this page stopped following the import, if it did.
   *
   * Usually not a failure of the import itself: the server brings the recipes
   * over whatever this tab can see. Which it is comes from the error — a run
   * the server no longer has is a different sentence from a network that went
   * away — so it is kept here rather than reduced to a flag.
   */
  readonly lost: AppError | null;
}
