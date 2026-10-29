/**
 * The iOS app follows the iPhone's text size (Dynamic Type), as every iOS app is expected to. A web view does not by
 * itself: its rem stays 16px whatever the setting. WebKit exposes the setting in one place, the `-apple-system-body`
 * font, so a probe set in it is measured and the root font size scaled by its ratio to the default body (17pt). Every
 * `--text-*` token is in rem, so the whole scale keeps its proportions, and the shell's `crowded` layout takes over
 * at the largest sizes. Measured again whenever the app returns, which is where the setting is changed.
 */

/** Body text at the default size ("Large"), what `--text-base` already is. */
const defaultBody = 17;

/** From the smallest setting (14pt) to the largest accessibility size (53pt), so a bad reading cannot run away. */
const smallest = 14 / defaultBody;
const largest = 53 / defaultBody;

/** How much larger than the default the person reads, from the body size WebKit reports. */
export function textScale(bodyPixels: number): number {
  if (!Number.isFinite(bodyPixels) || bodyPixels <= 0) {
    return 1;
  }

  return Math.min(largest, Math.max(smallest, bodyPixels / defaultBody));
}

/** Sets the root font size from the system's body text; returns the scale it applied. */
export function applyDynamicType(document: Document): number {
  const probe = document.createElement('span');

  probe.style.font = '-apple-system-body';
  probe.style.position = 'absolute';
  probe.style.visibility = 'hidden';
  probe.textContent = 'x';
  document.body.append(probe);

  const scale = textScale(parseFloat(getComputedStyle(probe).fontSize));

  probe.remove();
  document.documentElement.style.fontSize = scale === 1 ? '' : `${scale * 100}%`;

  return scale;
}
