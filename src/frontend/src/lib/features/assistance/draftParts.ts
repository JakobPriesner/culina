import { m } from '$shell/i18n';
import type { Recipe } from '$features/recipes/types';

import { offers, type Accepted, type Draft } from './draftToRecipe';

export interface DraftPart {
  key: keyof Accepted;
  label: string;
  before: string;
  after: string;
}

const minutes = (prep: number | null | undefined, cook: number | null | undefined): string =>
  [prep, cook]
    .filter((value): value is number => value != null)
    .map((value) => m['assist.minutes']({ count: value }))
    .join(' + ');

const yieldText = (amount: number, label: string | null | undefined): string =>
  `${amount} ${label ?? ''}`.trim();

const ingredientCount = (groups: readonly { ingredients: readonly unknown[] }[]): number =>
  groups.flatMap((group) => group.ingredients).length;

/** Review rows, only for parts the draft has: a part it left out must not offer to replace content with nothing. */
export function draftParts(draft: Draft | null, current: Recipe): DraftPart[] {
  if (!draft) {
    return [];
  }

  const available = offers(draft);

  const parts: DraftPart[] = [
    {
      key: 'title',
      label: m['assist.part.title'](),
      before: current.title,
      after: draft.title ?? ''
    },
    {
      key: 'description',
      label: m['assist.part.description'](),
      before: current.description ?? '',
      after: draft.description ?? ''
    },
    {
      key: 'yield',
      label: m['assist.part.yield'](),
      before: yieldText(current.yieldAmount, current.yieldLabel),
      after: yieldText(draft.yieldAmount ?? current.yieldAmount, draft.yieldLabel)
    },
    {
      key: 'times',
      label: m['assist.part.times'](),
      before: minutes(current.prepMinutes, current.cookMinutes),
      after: minutes(draft.prepMinutes, draft.cookMinutes)
    },
    {
      key: 'ingredients',
      label: m['assist.part.ingredients'](),
      before: m['assist.lines']({ count: ingredientCount(current.groups) }),
      after: m['assist.lines']({ count: ingredientCount(draft.groups) })
    },
    {
      key: 'steps',
      label: m['assist.part.steps'](),
      before: m['assist.stepCount']({ count: current.steps.length }),
      after: m['assist.stepCount']({ count: draft.steps.length })
    }
  ];

  return parts.filter((part) => available[part.key]);
}
