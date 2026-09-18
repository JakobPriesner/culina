import { registerStore } from '$shell/stores';

/**
 * How a list of recipes is ordered.
 *
 * The app's own words rather than the query string's, so a component reads
 * `quickest` instead of `totalMinutes`; `toWireSort` is the one place the two
 * meet. Every one of these is an order `GET /recipes` accepts — which is the
 * bug this type replaced, where the union said `match` and the API answered
 * 400.
 */
export type RecipeSort =
  | 'relevance'
  | 'suggested'
  | 'recent'
  | 'title'
  | 'quickest'
  | 'mostCooked'
  /** The order a cookbook was built in. Only meaningful inside one. */
  | 'shelf';

/** What the list is being asked for, when nobody has said. */
export interface SortContext {
  /** Whether there are words in the search box. */
  readonly searching: boolean;
  /** Whether the ranking has anything true to say about this kitchen yet. */
  readonly ranks: boolean;
  /** Whether a cookbook is being read. */
  readonly inACookbook: boolean;
}

/**
 * Which order a list is actually in.
 *
 * One function, used by both the request and the label above it. The server
 * has the same rule and would apply it for a client that sent nothing, but a
 * page that says "best match" has to be certain that is what it asked for —
 * a label deduced separately from the request is a label that can be wrong.
 */
export function effectiveSort(chosen: RecipeSort | null, context: SortContext): RecipeSort {
  if (chosen !== null) {
    return chosen;
  }

  if (context.searching) {
    return 'relevance';
  }

  if (context.inACookbook) {
    return 'shelf';
  }

  return context.ranks ? 'suggested' : 'recent';
}

/** Which orders are worth offering, given what is being asked. */
export function sortsFor(context: SortContext): readonly RecipeSort[] {
  return [
    // "Best match" of nothing is not an order anybody means.
    ...(context.searching ? (['relevance'] as const) : []),
    ...(context.ranks ? (['suggested'] as const) : []),
    ...(context.inACookbook ? (['shelf'] as const) : []),
    'recent',
    'title',
    'quickest',
    'mostCooked'
  ];
}

/** The words the query string uses. */
export function toWireSort(sort: RecipeSort): string {
  switch (sort) {
    case 'relevance':
      return 'relevance';
    case 'suggested':
      return 'suggested';
    case 'title':
      return 'title';
    case 'quickest':
      return 'totalMinutes';
    case 'mostCooked':
      return '-cookCount';
    case 'shelf':
      return 'cookbookOrder';
    default:
      return '-updatedAt';
  }
}

/** The same, read back — what a saved search stored. */
export function fromWireSort(wire: string | null | undefined): RecipeSort | null {
  switch (wire) {
    case 'relevance':
      return 'relevance';
    case 'suggested':
      return 'suggested';
    case 'title':
      return 'title';
    case 'totalMinutes':
      return 'quickest';
    case '-cookCount':
      return 'mostCooked';
    case '-updatedAt':
      return 'recent';
    default:
      // Including `cookbookOrder`, which a saved search may not hold: it needs
      // a cookbook to be an order of, and the library has none.
      return null;
  }
}

/**
 * The time ceilings the filter panel offers.
 *
 * Buckets rather than a number field, because "I have about half an hour" is
 * the thought, and asking somebody to type 37 is asking them to invent a
 * precision they do not have.
 */
export const timeCeilings = [15, 30, 45, 60] as const;

/** The ceiling the old "Quick" chip meant, kept as the shortcut it was. */
export const quickCeiling = 30;

/**
 * What the library is being asked for.
 *
 * One object rather than four loose pieces of state, because the page, the
 * cookbook page, the filter panel and a saved search all have to describe the
 * same question — and four separate fields are four chances for one of them to
 * describe a different one.
 */
export class RecipeQuery {
  /** The words in the search box, applied — not what is half-typed into it. */
  query = $state('');

  /** Tag slugs a recipe must all carry. */
  tags = $state<readonly string[]>([]);

  /** The longest a recipe may take, or null for any length. */
  maxMinutes = $state<number | null>(null);

  /**
   * Which order, once somebody has chosen one.
   *
   * Null means nobody has and the page decides — see `effectiveSort`. Stored as
   * a choice rather than as a default, so picking "recently updated" sticks
   * even on a day the ranking would have offered to take over.
   */
  sort = $state<RecipeSort | null>(null);

  /** The old "quick" chip, which was always a 30-minute ceiling. */
  get quick(): boolean {
    return this.maxMinutes === quickCeiling;
  }

  set quick(on: boolean) {
    this.maxMinutes = on ? quickCeiling : null;
  }

  /**
   * How many filters are on, for the number on the panel's trigger.
   *
   * The words are not counted: they are visible in the box they were typed
   * into, and a badge that included them would say "1 filter" over an empty
   * panel.
   */
  get activeCount(): number {
    return this.tags.length + (this.maxMinutes === null ? 0 : 1) + (this.sort === null ? 0 : 1);
  }

  /** Whether anything at all narrows the list. */
  get filtered(): boolean {
    return this.query.trim().length > 0 || this.tags.length > 0 || this.maxMinutes !== null;
  }

  toggleTag(slug: string): void {
    this.tags = this.tags.includes(slug)
      ? this.tags.filter((one) => one !== slug)
      : [...this.tags, slug];
  }

  /** Everything at once, which is what applying a saved search is. */
  assign(next: {
    query?: string;
    tags?: readonly string[];
    maxMinutes?: number | null;
    sort?: RecipeSort | null;
  }): void {
    this.query = next.query ?? '';
    this.tags = next.tags ?? [];
    this.maxMinutes = next.maxMinutes ?? null;
    this.sort = next.sort ?? null;
  }

  /** The same, read back out — what saving a search stores. */
  snapshot(): {
    query: string;
    tags: readonly string[];
    maxMinutes: number | null;
    sort: RecipeSort | null;
  } {
    return {
      query: this.query,
      tags: this.tags,
      maxMinutes: this.maxMinutes,
      sort: this.sort
    };
  }

  clear(): void {
    this.query = '';
    this.tags = [];
    this.maxMinutes = null;
    this.sort = null;
  }
}

/**
 * The library's own question, kept through a recipe round trip.
 *
 * Scoped to the household: another kitchen's tags are not this one's, and a
 * filter naming a slug nobody here uses would quietly match nothing and look
 * broken.
 */
class LibraryView extends RecipeQuery {
  #householdId: string | null = null;

  forHousehold(id: string): void {
    if (this.#householdId !== id) {
      this.reset();
      this.#householdId = id;
    }
  }

  reset(): void {
    this.clear();
    this.#householdId = null;
  }
}

export const libraryView = new LibraryView();
registerStore(() => libraryView.reset());
