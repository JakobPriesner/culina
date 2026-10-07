<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * A non-modal panel on the browser's `popover` (light dismiss, top layer, Escape), placed in JS because CSS anchor positioning is not universal.
   * Not for decisions that must be answered; use `Sheet` or `Modal`.
   */
  interface Props {
    /** The control that opens it; receives the attributes that pair the two. */
    trigger: Snippet<[{ popovertarget: string }]>;
    children: Snippet;
    /** Which edge of the trigger it lines up with; flips when there is no room. */
    placement?: 'bottom-start' | 'bottom-end';
  }

  let { trigger, children, placement = 'bottom-start' }: Props = $props();

  const id = $props.id();
  let anchor = $state<HTMLDivElement>();
  let panel = $state<HTMLDivElement>();
  let open = $state(false);

  /** Gap between trigger and panel (`--space-1`, as a number). */
  const gap = 4;

  /** Minimum distance to the screen edge. */
  const edge = 16;

  /** Clamps into the range, pinned to its start when the range has no room. */
  const within = (value: number, least: number, most: number) =>
    Math.max(least, Math.min(value, Math.max(least, most)));

  /** Puts the panel under its trigger; the allowed height is read first because it decides the top edge, then the wanted one picks the side. */
  function place() {
    const from = anchor?.getBoundingClientRect();

    if (!panel || !from) {
      return;
    }

    const view = { width: window.innerWidth, height: window.innerHeight };

    // Cleared first, or the cap left by the last run squeezes this one for good.
    panel.style.maxHeight = '';

    const wanted = panel.getBoundingClientRect().height;
    const under = view.height - edge - (from.bottom + gap);
    const over = from.top - gap - edge;

    // Under the trigger unless it fits better above (short landscape screens).
    const above = wanted > under && over > under;

    // Screen left on the chosen side; the panel scrolls inside it.
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

    /**
     * Re-placed every frame while open: scroll and resize miss most trigger moves (chips added in this panel, a header that stops floating).
     * Only the trigger is compared; re-placing clears the height cap, and one full-height frame resets the panel's scroll.
     */
    let last = '';
    let frame = 0;

    const follow = () => {
      const from = anchor?.getBoundingClientRect();
      const now = `${from?.left} ${from?.top} ${from?.bottom} ${window.innerWidth} ${window.innerHeight}`;

      if (now !== last) {
        last = now;
        place();
      }

      frame = requestAnimationFrame(follow);
    };

    follow();

    return () => cancelAnimationFrame(frame);
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
    /* Viewport coordinates from `place()`; `inset: auto` overrides the browser's centring `inset: 0`. */
    position: fixed;
    inset: auto;
    margin: 0;
    padding: var(--space-2);
    border: 1px solid var(--border);
    border-radius: var(--radius-md);
    background: var(--surface-overlay);
    color: var(--text);
    box-shadow: var(--shadow-overlay);
    /* Against the screen, so moving the panel never changes its size. */
    max-width: calc(100dvw - 2 * var(--space-4));
    /* Only the axis `place()` manages; `overflow: auto` on both made over-wide content grow a horizontal bar instead of wrapping. */
    overflow-x: clip;
    overflow-y: auto;
    scrollbar-gutter: stable;
    overscroll-behavior: contain;
  }

  /* Opacity only: placement measures this box while opening, so geometry animation would skew it. */
  @supports (overlay: auto) and (transition-behavior: allow-discrete) {
    .panel {
      opacity: 0;
      transition:
        opacity var(--duration-fast) var(--ease-out),
        display var(--duration-fast) allow-discrete,
        overlay var(--duration-fast) allow-discrete;
    }

    .panel:popover-open {
      opacity: 1;
    }

    @starting-style {
      .panel:popover-open {
        opacity: 0;
      }
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .panel {
      transition: none;
    }
  }
</style>
