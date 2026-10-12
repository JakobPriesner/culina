import { m } from '$shell/i18n';

import type { LookalikeReason } from './types';

/** Why a merge is suggested, in the reader's words. */
export const reasonWords: Record<LookalikeReason, () => string> = {
  spelling: m['ingredients.reason.spelling'],
  same_food: m['ingredients.reason.sameFood'],
  typo: m['ingredients.reason.typo']
};
