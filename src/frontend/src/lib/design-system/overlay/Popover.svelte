<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * A small panel attached to the control that opened it.
   *
   * Not modal: the page stays usable, and clicking anywhere else closes it.
   * That behaviour — light dismiss, top layer, Escape, and the pairing between
   * trigger and panel — is the browser's `popover`, so none of it is
   * reimplemented here.
   *
   * Where it lands is not the browser's. That was CSS anchor positioning, which
   * today means Chromium and Safari 26 — and the stylesheet's own fallback for
   * everybody else put the panel in the middle of the screen, which is the one
   * place a menu must never be: it reads as a dialog, it covers what it was
   * opened from, and nothing on it says which control it belongs to. Nobody
   * developing in Chrome would ever see it.
   *
   * So the arithmetic is done here, once, for every browser. It is less code
   * than it replaced: `@position-try`, the `position-area` pairs, the
   * `@supports` fallback and the observer that existed to notice the anchor had
   * scrolled out from under an anchored panel are all gone.
   *
   * For a decision that must be answered, use `Sheet` or `Modal` instead. A
   * popover that must not be dismissed is a modal wearing the wrong clothes.
   */
  interface Props {
    /** The control that opens it. Receives the attributes that pair the two. */
    trigger: Snippet<[{ popovertarget: string }]>;
    children: Snippet;
    /** Which edge of the trigger it lines up with. Flips if there is no room. */
    placement?: 'bottom-start' | 'bottom-end';
  }

  let { trigger, children, placement = 'bottom-start' }: Props = $props();

  const id = $props.id();
  let anchor = $state<HTMLDivElement>();
  let panel = $state<HTMLDivElement>();
  let open = $state(false);

  /** The breath between a trigger and its panel. `--space-1`, as a number. */
  const gap = 4;

  /** The least the panel leaves between itself and the edge of the screen. */
  const edge = 16;

  /** Inside the range, and pinned to its start when the range has no room. */
  const within = (value: number, least: number, most: number) =>
    Math.max(least, Math.min(value, Math.max(least, most)));

  /**
   * Puts the panel under its trigger.
   *
   * Two reads, in this order, because the second depends on the first: the
   * height it is allowed decides where its top edge goes, and the height it
   * wants decides which side of the trigger it is allowed that height on.
   */
  function place() {
    const from = anchor?.getBoundingClientRect();

    if (!panel || !from) {
      return;
    }

    const view = { width: window.innerWidth, height: window.innerHeight };

    // Cleared first, or the cap left behind by the last run decides this one —
    // and a panel that has been squeezed once stays squeezed for the rest of
    // its life.
    panel.style.maxHeight = '';

    const wanted = panel.getBoundingClientRect().height;
    const under = view.height - edge - (from.bottom + gap);
    const over = from.top - gap - edge;

    // Under the trigger, unless it will not fit there and fits better over it.
    // On a short landscape screen with the trigger low down, that is the
    // difference between a list and a sliver of one.
    const above = wanted > under && over > under;

    // What is left of the screen on the chosen side. The panel scrolls inside
    // this rather than running off the bottom of it.
    panel.style.maxHeight = `${Math.max(0, above ? over : under)}px`;

    const { width, height } = panel.getBoundingClientRect();
    const start = placement === 'bottom-end' ? from.right - width : from.left;
    const top = above ? from.top - gap - height : from.bottom + gap;

    panel.style.left = `${within(start, edge, view.width - edge - width)}px`;
    panel.style.top = `${within(top, edge, view.height - edge - height)}px`;
  }

  $effect(() => {
    if (!open) {
      return;
    }

    place();

    /**
     * Anything that moved the trigger, except the panel reading itself.
     *
     * A panel taller than the room it was given scrolls inside itself, and that
     * scroll is captured here like any other. Re-placing on it clears the
     * height cap to measure what the panel wants — which for one frame makes it
     * its full height, and the browser clamps its scroll position back to the
     * top. The list jumps to the beginning every time somebody reads down it.
     */
    const again = (event: Event) => {
      if (!(event.target instanceof Node) || !panel?.contains(event.target)) {
        place();
      }
    };

    // Captured, so a trigger inside something that scrolls on its own carries
    // its panel with it, and not only the page does.
    window.addEventListener('scroll', again, { capture: true, passive: true });
    window.addEventListener('resize', again);

    return () => {
      window.removeEventListener('scroll', again, { capture: true });
      window.removeEventListener('resize', again);
    };
  });
</script>

<div class="anchor" bind:this={anchor}>
  {@render trigger({ popovertarget: id })}

  <div
    {id}
    bind:this={panel}
    class="panel"
    popover="auto"
    ontoggle={(event) => {
      open = event.newState === 'open';
    }}
  >
    {@render children()}
  </div>
</div>

<style>
  .anchor {
    display: inline-flex;
  }

  .panel {
    /*
     * Placed in viewport coordinates by `place()`. `inset: auto` because the
     * browser's own rule for a popover is `inset: 0` with `margin: auto`, which
     * is what centres one — and an `inset` still set on the other two edges
     * would fight the two being written here.
     */
    position: fixed;
    inset: auto;
    margin: 0;
    padding: var(--space-2);
    border: 1px solid var(--border);
    border-radius: var(--radius-md);
    background: var(--surface-overlay);
    color: var(--text);
    box-shadow: var(--shadow-overlay);
    /* Against the screen rather than against where it ends up, so that moving
       the panel can never change its size and one measurement stays true. */
    max-width: calc(100dvw - 2 * var(--space-4));
    /*
     * Only the axis `place()` actually manages. The height is capped there, in
     * viewport pixels; the width is capped above, in CSS. `overflow: auto` on
     * both axes meant content wider than the cap grew a horizontal bar under
     * the panel instead of wrapping.
     */
    overflow-x: clip;
    overflow-y: auto;
    overscroll-behavior: contain;
  }
</style>
