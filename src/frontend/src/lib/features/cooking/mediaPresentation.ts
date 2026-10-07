import { parseStep, type Inline } from '$features/recipes/surface/stepMarkdown';
import type { Quantity, RecipeReading } from '$features/recipes/types';
import { m } from '$shell/i18n';

import type { KitchenTimer } from './timerState';

/** How much of a step the lock screen has room for. */
const excerptLength = 180;

type AmountOf = (of: { readonly quantity: Quantity }) => { readonly text: string };

/**
 * The timer the remote should talk about: the one on the step being cooked,
 * then the one that was just paused from the remote, then the next to ring,
 * then the earliest step's. Finished timers are never offered.
 */
export function selectTimer(
  timers: readonly KitchenTimer[],
  currentStep: number | undefined,
  pausedStep: number | null,
  now: number
): KitchenTimer | undefined {
  const eligible = timers.filter((timer) =>
    timer.pausedRemaining !== undefined ? timer.pausedRemaining > 0 : timer.endsAt > now
  );

  return (
    eligible.find((timer) => timer.stepIndex === currentStep) ??
    eligible.find(
      (timer) => timer.stepIndex === pausedStep && timer.pausedRemaining !== undefined
    ) ??
    eligible
      .filter((timer) => timer.pausedRemaining === undefined)
      .sort((a, b) => a.endsAt - b.endsAt)[0] ??
    [...eligible].sort((a, b) => a.stepIndex - b.stepIndex)[0]
  );
}

function inlineText(nodes: readonly Inline[], amountFor: AmountOf): string {
  return nodes
    .map((node) => {
      if (node.kind === 'ingredient') {
        return [amountFor(node).text, node.name].filter(Boolean).join(' ');
      }

      return 'children' in node ? inlineText(node.children, amountFor) : node.text;
    })
    .join('');
}

/** A step as one line of plain text, with ingredient amounts at the current scale. */
export function stepExcerpt(
  step: RecipeReading['steps'][number] | undefined,
  amountFor: AmountOf
): string {
  if (!step) {
    return '';
  }

  return parseStep(step.segments)
    .map((block) =>
      block.kind === 'paragraph'
        ? inlineText(block.children, amountFor)
        : block.items.map((item) => inlineText(item, amountFor)).join(' ')
    )
    .join(' ')
    .replace(/\s+/g, ' ')
    .trim()
    .slice(0, excerptLength);
}

/** `Simmer · 1:05 left`, or nothing without a timer. */
export function timerLine(timer: KitchenTimer | undefined, now: number): string {
  if (!timer) {
    return '';
  }

  const remaining = timer.pausedRemaining ?? Math.max(0, Math.ceil((timer.endsAt - now) / 1000));
  const time = {
    minutes: Math.floor(remaining / 60),
    seconds: String(remaining % 60).padStart(2, '0')
  };
  const status =
    timer.pausedRemaining !== undefined
      ? m['cooking.timer.paused'](time)
      : m['cooking.timer.running'](time);

  return `${timer.label} · ${status}`;
}

/** What the OS shows for the session: title, artist and album, without artwork. */
export function describeStep(
  recipe: RecipeReading,
  index: number,
  excerpt: string,
  timerText: string
): { title: string; artist: string; album: string; progress: string } {
  const step = recipe.steps[index];
  const progress = m['cooking.stepOf']({ current: index + 1, total: recipe.steps.length });

  return {
    title: `${recipe.title} · ${progress}${timerText ? ` · ${timerText}` : ''}`,
    artist: [step?.title, excerpt].filter(Boolean).join(' · '),
    album: m['cooking.nowCooking']({ title: recipe.title }),
    progress
  };
}
