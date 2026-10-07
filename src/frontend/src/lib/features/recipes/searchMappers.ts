import type { components } from '$api/generated/schema';

import type {
  ChipKind,
  Completion,
  Facet,
  Facets,
  Interpretation,
  MatchReason,
  SearchChip
} from './types';

type WireInterpretation = components['schemas']['RecipesGetAllInterpretation'];
type WireChip = components['schemas']['RecipesGetAllAppliedInference'];
type WireFacets = components['schemas']['RecipesGetAllFacets'];
type WireFacet = components['schemas']['RecipesGetAllFacet'];
type WireReason = components['schemas']['RecipesGetAllMatchReason'];
type WireCompletion = components['schemas']['RecipesGetCompletionsCompletion'];

const reasonKinds = new Set<MatchReason['kind']>(['ingredient', 'tag', 'text', 'concept']);

/** A reason this client does not know how to word is no reason at all. */
export const toReason = (wire: WireReason): MatchReason | null =>
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
