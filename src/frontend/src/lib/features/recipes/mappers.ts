import type { components } from '$api/generated/schema';

import type {
  ChipKind,
  Completion,
  Facet,
  Facets,
  Ingredient,
  IngredientGroup,
  Interpretation,
  MatchReason,
  Recipe,
  RecipeReading,
  RecipeSummary,
  RelatedRecipe,
  SearchChip,
  Step,
  StepSegment,
  Suggestion,
  SuggestionReasonCode,
  YieldKind
} from './types';

/**
 * Wire shapes to the app's own, at the store boundary and nowhere else.
 *
 * This is the frontend's half of the same rule the backend follows: a contract
 * is a wire format, not a domain model. Everything awkward about the wire — a
 * nullable id that is never actually null on a read, a flattened union, a date
 * as a string — is dealt with here once.
 */
type WireSummary = components['schemas']['RecipesGetAllRecipeSummary'];
type WireRecipe = components['schemas']['RecipesRecipeDetail'];
type WireGroup = components['schemas']['RecipesIngredientGroupContract'];
type WireIngredient = components['schemas']['RecipesIngredientContract'];
type WireStep = components['schemas']['RecipesStepContract'];
type WireSegment = components['schemas']['RecipesStepSegmentContract'];
type WireSuggestion = components['schemas']['SuggestionsGetAllSuggestion'];
type WireRelated = components['schemas']['RecipesGetRelatedRelatedRecipe'];
type WireShared = components['schemas']['RecipesGetSharedResponse'];
type WireInterpretation = components['schemas']['RecipesGetAllInterpretation'];
type WireChip = components['schemas']['RecipesGetAllAppliedInference'];
type WireFacets = components['schemas']['RecipesGetAllFacets'];
type WireFacet = components['schemas']['RecipesGetAllFacet'];
type WireReason = components['schemas']['RecipesGetAllMatchReason'];
type WireCompletion = components['schemas']['RecipesGetCompletionsCompletion'];

export const toSummary = (wire: WireSummary): RecipeSummary => ({
  id: wire.recipeId,
  householdId: wire.householdId,
  title: wire.title,
  imageId: wire.imageId ?? null,
  totalMinutes: wire.totalMinutes ?? null,
  yieldAmount: wire.yieldAmount,
  yieldKind: wire.yieldKind as YieldKind,
  yieldLabel: wire.yieldLabel ?? null,
  tags: wire.tags,
  cookCount: wire.cookCount,
  lastCookedAt: wire.lastCookedAt ?? null,
  updatedAt: wire.updatedAt,
  match: wire.ingredientMatch
    ? {
        matched: wire.ingredientMatch.matched,
        requested: wire.ingredientMatch.requested,
        missing: wire.ingredientMatch.missing
      }
    : null,
  matchReason: wire.matchReason ? toReason(wire.matchReason) : null
});

const reasonKinds = new Set<MatchReason['kind']>(['ingredient', 'tag', 'text', 'concept']);

/** A reason this client does not know how to word is no reason at all. */
const toReason = (wire: WireReason): MatchReason | null =>
  reasonKinds.has(wire.kind as MatchReason['kind'])
    ? { kind: wire.kind as MatchReason['kind'], term: wire.term ?? null }
    : null;

const chipKinds = new Set<ChipKind>([
  'time',
  'quick',
  'diet',
  'meal',
  'cuisine',
  'ingredient',
  'exclusion'
]);

const toChips = (wire: readonly WireChip[] | null | undefined): SearchChip[] =>
  (wire ?? [])
    .filter((chip) => chipKinds.has(chip.kind as ChipKind))
    .map((chip) => ({
      kind: chip.kind as ChipKind,
      value: chip.value,
      text: chip.text,
      start: chip.start,
      end: chip.end,
      word: chip.word ?? null
    }));

export const toInterpretation = (wire: WireInterpretation): Interpretation => ({
  freeText: wire.freeText,
  chips: toChips(wire.applied),
  correctedFrom: wire.correctedFrom ?? null,
  relaxed: toChips(wire.relaxed),
  conflict: toChips(wire.conflict)
});

const toFacet = (wire: WireFacet): Facet => ({
  value: wire.value,
  label: wire.label ?? null,
  count: wire.count
});

export const toFacets = (wire: WireFacets): Facets => ({
  tags: wire.tags.map(toFacet),
  times: wire.times.map(toFacet),
  cuisines: wire.cuisines.map(toFacet)
});

/** Null for a kind this client does not know, which it then does not offer. */
export const toCompletion = (wire: WireCompletion): Completion | null => {
  switch (wire.kind) {
    case 'recipe':
      return wire.recipeId
        ? {
            kind: 'recipe',
            label: wire.label,
            recipeId: wire.recipeId,
            imageId: wire.imageId ?? null,
            totalMinutes: wire.totalMinutes ?? null
          }
        : null;
    case 'ingredient':
      return { kind: 'ingredient', label: wire.label, recipeCount: wire.recipeCount ?? 0 };
    case 'tag':
      return wire.slug
        ? { kind: 'tag', label: wire.label, slug: wire.slug, recipeCount: wire.recipeCount ?? 0 }
        : null;
    case 'refinement':
      return wire.maxMinutes
        ? {
            kind: 'refinement',
            label: wire.label,
            recipeCount: wire.recipeCount ?? 0,
            maxMinutes: wire.maxMinutes
          }
        : null;
    default:
      return null;
  }
};

export const toSuggestion = (wire: WireSuggestion): Suggestion => ({
  id: wire.recipeId,
  title: wire.title,
  imageId: wire.imageId ?? null,
  totalMinutes: wire.totalMinutes ?? null,
  yieldAmount: wire.yieldAmount,
  yieldKind: wire.yieldKind as YieldKind,
  yieldLabel: wire.yieldLabel ?? null,
  tags: wire.tags,
  cookCount: wire.cookCount,
  lastCookedAt: wire.lastCookedAt ?? null,
  updatedAt: wire.updatedAt,
  // A suggestion is not a search result: nobody named an ingredient, so there
  // is nothing to report a match against.
  match: null,
  reason: wire.reason
    ? { code: wire.reason.code as SuggestionReasonCode, subject: wire.reason.subject ?? null }
    : null
});

export const toRelated = (wire: WireRelated): RelatedRecipe => ({
  id: wire.recipeId,
  title: wire.title,
  imageId: wire.imageId ?? null,
  totalMinutes: wire.totalMinutes ?? null,
  yieldAmount: wire.yieldAmount,
  yieldKind: wire.yieldKind as YieldKind,
  yieldLabel: wire.yieldLabel ?? null,
  tags: wire.tags,
  cookCount: wire.cookCount,
  lastCookedAt: wire.lastCookedAt ?? null,
  updatedAt: wire.updatedAt,
  match: null,
  reason: {
    kind: wire.reason.kind === 'kinds' ? 'kinds' : 'ingredients',
    shared: wire.reason.shared
  }
});

export const toRecipe = (wire: WireRecipe): Recipe => ({
  id: wire.recipeId,
  householdId: wire.householdId,
  title: wire.title,
  description: wire.description ?? null,
  language: wire.language,
  yieldAmount: wire.yieldAmount,
  yieldKind: wire.yieldKind,
  yieldLabel: wire.yieldLabel ?? null,
  prepMinutes: wire.prepMinutes ?? null,
  cookMinutes: wire.cookMinutes ?? null,
  totalMinutes: wire.totalMinutes ?? null,
  imageId: wire.imageId ?? null,
  groups: wire.groups.map(toGroup),
  steps: wire.steps.map(toStep),
  tags: wire.tags,
  sourceUrl: wire.origin?.sourceUrl ?? null,
  createdBy: wire.createdBy,
  createdAt: wire.createdAt,
  updatedAt: wire.updatedAt,
  version: wire.version
});

/**
 * A shared recipe, which is a reading and nothing more.
 *
 * The id it is given is the token out of the link, because on this page that is
 * genuinely what identifies the recipe — there is no other name for it here,
 * and it is what the photograph is fetched under.
 */
export const toSharedRecipe = (token: string, wire: WireShared): RecipeReading => ({
  id: token,
  title: wire.title,
  description: wire.description ?? null,
  language: wire.language,
  yieldAmount: wire.yieldAmount,
  yieldKind: wire.yieldKind,
  yieldLabel: wire.yieldLabel ?? null,
  prepMinutes: wire.prepMinutes ?? null,
  cookMinutes: wire.cookMinutes ?? null,
  totalMinutes: wire.totalMinutes ?? null,
  // The surface only asks whether there is a picture; where it lives is the
  // page's business, and on this page it lives under the token.
  imageId: wire.hasImage ? token : null,
  groups: wire.groups.map(toGroup),
  steps: wire.steps.map(toStep),
  tags: wire.tags,
  // Only the address travels: which library it came from and when it was
  // fetched are facts about somebody's setup, and are not on the wire here.
  sourceUrl: wire.sourceUrl ?? null,
  // A visitor cannot see when it last changed, and the surface does not draw
  // it — but the type asks, so it is the one thing here that is a placeholder.
  updatedAt: ''
});

const toGroup = (wire: WireGroup): IngredientGroup => ({
  id: wire.groupId ?? null,
  name: wire.name ?? null,
  ingredients: wire.ingredients.map(toIngredient)
});

const toIngredient = (wire: WireIngredient): Ingredient => ({
  // The wire allows an absent id because a *write* creates lines without one.
  // On a read the server has always assigned one.
  id: wire.ingredientId ?? '',
  quantity: { value: wire.quantity ?? null, unit: wire.unit ?? null },
  name: wire.name,
  note: wire.note ?? null
});

const toStep = (wire: WireStep): Step => ({
  id: wire.stepId ?? null,
  title: wire.title ?? null,
  segments: wire.segments.map(toSegment),
  uses: wire.uses ?? [],
  durationSeconds: wire.durationSeconds ?? null
});

/**
 * Narrows the flattened union the contract carries.
 *
 * The wire keeps it flat on purpose, so a generated client does not have to
 * narrow one; the narrowing happens here, once, and components get a real
 * discriminated union.
 */
const toSegment = (wire: WireSegment): StepSegment =>
  wire.type === 'ingredient'
    ? {
        kind: 'ingredient',
        ingredientId: wire.recipeIngredientId ?? '',
        name: wire.name ?? '',
        quantity: { value: wire.quantity ?? null, unit: wire.unit ?? null }
      }
    : { kind: 'text', text: wire.value ?? '' };

/** The app's shape back onto the wire, for a write. */
export const toWireGroups = (groups: readonly IngredientGroup[]): WireGroup[] =>
  groups.map((group) => ({
    groupId: group.id ?? undefined,
    name: group.name ?? undefined,
    ingredients: group.ingredients.map((one) => ({
      ingredientId: one.id || undefined,
      quantity: one.quantity.value ?? undefined,
      unit: one.quantity.unit ?? undefined,
      name: one.name,
      note: one.note ?? undefined
    }))
  }));

export const toWireSteps = (steps: readonly Step[]): WireStep[] =>
  steps.map((step) => ({
    stepId: step.id ?? undefined,
    title: step.title ?? undefined,
    durationSeconds: step.durationSeconds ?? undefined,
    uses: [...step.uses],
    segments: step.segments.map((segment) =>
      segment.kind === 'ingredient'
        ? { type: 'ingredient' as const, recipeIngredientId: segment.ingredientId }
        : { type: 'text' as const, value: segment.text }
    )
  }));
