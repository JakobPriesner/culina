import { formatQuantity } from '$features/recipes/formatQuantity';
import { quantityLabels } from '$features/recipes/quantityLabels';
import { preferences } from '$shell/preferences.svelte';

import { amountToBuy } from './amountToBuy';

/** An amount as it is read on the list, in the measuring system and language chosen; empty when there is none. */
export const amountOf = (quantity: number | null | undefined, unit: string | null | undefined) =>
  formatQuantity(
    amountToBuy({ value: quantity ?? null, unit: unit ?? null }, preferences.measurementSystem),
    preferences.locale,
    quantityLabels
  ).text;
