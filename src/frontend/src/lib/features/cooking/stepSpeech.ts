import type { Locale } from '$shell/i18n';
import { parseStep } from '$features/recipes/surface/stepMarkdown';
import type { RecipeReading } from '$features/recipes/types';

import { inlineText, type AmountOf, type NameOf } from './mediaPresentation';
import { asSentence, spokenText } from './spokenText';

type Step = RecipeReading['steps'][number];

/**
 * What the cook hears for a step: its words at the scaled amounts, each paragraph and list item a sentence of its own so the voice pauses between them.
 * The title is left out; it names the step on screen and repeats what the text says.
 */
export function stepSpeech(
  step: Step | undefined,
  amountFor: AmountOf,
  nameFor: NameOf,
  locale: Locale
): string {
  if (!step) {
    return '';
  }

  return parseStep(step.segments)
    .flatMap((block) =>
      block.kind === 'paragraph'
        ? [inlineText(block.children, amountFor, nameFor)]
        : block.items.map((item) => inlineText(item, amountFor, nameFor))
    )
    .map((sentence) => asSentence(spokenText(sentence, locale)))
    .filter(Boolean)
    .join(' ');
}
