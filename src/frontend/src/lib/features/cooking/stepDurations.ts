/*
 * The waits a step names in its words ("4 Min. braten", "1½ Std. garen"), for a step whose author set no
 * timer: cook mode offers each as a timer instead of leaving the cook to set one on a phone with wet
 * hands. A guess, so only offered, never stored. Minutes and hours only; a range counts from its start,
 * since checking early is safe and late is not.
 */

const vulgar: Record<string, number> = { '½': 0.5, '¼': 0.25, '¾': 0.75, '⅓': 1 / 3, '⅔': 2 / 3 };

const number = String.raw`\d+(?:[.,]\d+)?(?:\s*[½¼¾⅓⅔]|\s+\d\/\d|\d?⁄\d)?|[½¼¾⅓⅔]|\d\/\d`;

const minutes = String.raw`min(?:uten?|s|\.)?|minutes?`;
const hours = String.raw`std(?:\.|unden?)?|stunden?|hours?|hrs?\.?|h`;

const pattern = new RegExp(
  String.raw`(?<![\w.,⁄/])(${number})(?:\s*(?:[-–—]|bis|to)\s*(?:${number}))?\s*(${minutes}|${hours})(?![\p{L}\d-])`,
  'giu'
);

/** "1½", "1 1/2", "1,5", and "11⁄2" as typeset by recipe sites: a fraction slash takes one digit each side. */
function amount(written: string): number {
  const text = written.trim();
  const slash = /^(\d*?)(\d)⁄(\d)$/.exec(text);

  if (slash) {
    return Number(slash[1] || 0) + Number(slash[2]) / Number(slash[3]);
  }

  const mixed = /^(\d+)\s+(\d)\/(\d)$/.exec(text) ?? /^()(\d)\/(\d)$/.exec(text);

  if (mixed) {
    return Number(mixed[1] || 0) + Number(mixed[2]) / Number(mixed[3]);
  }

  const whole = /^(\d+(?:[.,]\d+)?)?\s*([½¼¾⅓⅔])?$/.exec(text);

  return whole ? Number((whole[1] ?? '0').replace(',', '.')) + (vulgar[whole[2] ?? ''] ?? 0) : NaN;
}

/** Every wait in the text, in seconds, in the order written and once each. */
export function durationsIn(text: string): number[] {
  const found: number[] = [];

  for (const match of text.matchAll(pattern)) {
    const [, start = '', unit = ''] = match;
    const value = amount(start);
    const seconds = Math.round(value * (/^m/i.test(unit) ? 60 : 3600));

    if (seconds >= 60 && seconds <= 24 * 3600 && !found.includes(seconds)) {
      found.push(seconds);
    }
  }

  return found;
}
