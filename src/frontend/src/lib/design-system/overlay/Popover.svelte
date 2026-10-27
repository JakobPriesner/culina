<script lang="ts">
  import { MediaQuery } from 'svelte/reactivity';
  import type { Snippet } from 'svelte';

  import { followed, releasesSheet } from './sheetDrag';

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
    /** On a phone, a sheet along the bottom edge instead: for a long panel opened from the top of the screen, out of the thumb's reach. */
    sheetOnPhone?: boolean;
  }

  let { trigger, children, placement = 'bottom-start', sheetOnPhone = false }: Props = $props();

  const phone = new MediaQuery('(width < 40rem)', false);

  const asSheet = $derived(sheetOnPhone && phone.current);

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

    // A sheet is placed by its CSS; what the last placement left inline would pin it beside the trigger.
    if (asSheet) {
      panel.style.left = panel.style.top = panel.style.maxHeight = '';
      return;
    }

    const view = { width: window.innerWidth, height: window.innerHeight };

    // Cleared first, or the cap left by the last run squeezes this one for good.
    panel.style.maxHeight = '';

    // Layout sizes, not the box on screen: the panel grows in from a slight scale while opening.
    const wanted = panel.offsetHeight;
    const under = view.height - edge - (from.bottom + gap);
    const over = from.top - gap - edge;

    // Under the trigger unless it fits better above (short landscape screens).
    const above = wanted > under && over > under;

    // Screen left on the chosen side; the panel scrolls inside it.
    panel.style.maxHeight = `${Math.max(0, above ? over : under)}px`;

    const width = panel.offsetWidth;
    const height = panel.offsetHeight;
    const start = placement === 'bottom-end' ? from.right - width : from.left;
    const top = above ? from.top - gap - height : from.bottom + gap;

    // It grows out of the corner nearest its trigger, so it reads as coming from that button.
    panel.style.transformOrigin = `${placement === 'bottom-end' ? 'right' : 'left'} ${above ? 'bottom' : 'top'}`;

    panel.style.left = `${within(start, edge, view.width - edge - width)}px`;
    panel.style.top = `${within(top, edge, view.height - edge - height)}px`;
  }

  /** A pull on the sheet's handle, under way; not state, the panel's transform is set directly (see `Dialog`). */
  let pull: { pointer: number; start: number; last: number; at: number; velocity: number } | null =
    null;

  function grab(event: PointerEvent) {
    if (!panel || !event.isPrimary || event.button !== 0) return;

    pull = {
      pointer: event.pointerId,
      start: event.clientY,
      last: event.clientY,
      at: event.timeStamp,
      velocity: 0
    };
    (event.currentTarget as Element).setPointerCapture?.(event.pointerId);
    panel.classList.add('pulled');
  }

  function follow(event: PointerEvent) {
    if (!pull || !panel || event.pointerId !== pull.pointer) return;

    const elapsed = event.timeStamp - pull.at;

    if (elapsed > 0) pull.velocity = (event.clientY - pull.last) / elapsed;
    pull.last = event.clientY;
    pull.at = event.timeStamp;
    panel.style.transform = `translateY(${followed(event.clientY - pull.start)}px)`;
  }

  function release(event: PointerEvent) {
    if (!pull || !panel || event.pointerId !== pull.pointer) return;

    const offset = pull.last - pull.start;
    const goes =
      event.type === 'pointerup' && releasesSheet(offset, panel.offsetHeight, pull.velocity);

    pull = null;
    panel.classList.remove('pulled');

    if (goes) {
      panel.hidePopover();
    } else {
      panel.style.removeProperty('transform');
    }
  }

  /**
   * Whether the press under way began on the dimmed page while the sheet was up. The browser closes the popover on
   * it, but the click that follows still reaches whatever lies under the dimming; that click is the dismissal, so it
   * is swallowed. The trigger is left alone: its own click is what closes it.
   */
  let pressedOutside = false;

  function pressOutside(event: PointerEvent) {
    pressedOutside =
      asSheet &&
      open &&
      event.target instanceof Node &&
      anchor !== undefined &&
      !anchor.contains(event.target);
  }

  function swallowClick(event: MouseEvent) {
    if (!pressedOutside) return;

    pressedOutside = false;
    event.preventDefault();
    event.stopPropagation();
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

<svelte:window onpointerdowncapture={pressOutside} onclickcapture={swallowClick} />

<div class="anchor" bind:this={anchor}>
  {@render trigger({ popovertarget: id })}

  <div
    {id}
    bind:this={panel}
    class="panel"
    class:sheet={asSheet}
    popover="auto"
    ontoggle={(event) => {
      open = event.newState === 'open';

      // A sheet last let go by a pull opens from where it rests.
      if (open) panel?.style.removeProperty('transform');

      // The dismissing click has come by now (it shares the press's task); a press with none must not eat a later one.
      if (!open) setTimeout(() => (pressedOutside = false));
    }}
  >
    {#if asSheet}
      <!-- The handle, and where a pull starts: the rows below are pressed, not dragged. -->
      <div
        class="grip"
        role="presentation"
        onpointerdown={grab}
        onpointermove={follow}
        onpointerup={release}
        onpointercancel={release}
      >
        <div class="grabber"></div>
      </div>
    {/if}
    <div class="scroller">{@render children()}</div>
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
    border: 1px solid var(--border);
    /* Concentric with its rows: lg minus this padding is the rows' md. */
    border-radius: var(--radius-lg);
    background: var(--surface-overlay);
    color: var(--text);
    box-shadow: var(--shadow-overlay);
    /* Against the screen, so moving the panel never changes its size. */
    max-width: calc(100dvw - 2 * var(--space-4));
    /*
     * The panel draws the rounded edge and clips to it; the scroller inside scrolls. Scrolling the panel itself let a
     * browser with classic scrollbars paint the reserved gutter as a square strip over the right-hand corners and border.
     */
    overflow: clip;
  }

  /* Along the bottom edge, full width, clear of the home indicator; it scrolls inside when long. */
  .panel.sheet {
    inset: auto 0 0;
    width: 100%;
    max-width: none;
    max-height: 85dvh;
    border-width: 0;
    border-radius: var(--radius-xl) var(--radius-xl) 0 0;
    padding-bottom: env(safe-area-inset-bottom, 0px);
  }

  /* Dimmed behind, as a modal sheet is: the page is not what is being worked on while it is up. */
  .panel.sheet::backdrop {
    background: var(--scrim);
  }

  /* The handle's strip, tall enough to take a thumb; the browser must not take the pull for a scroll. */
  .grip {
    display: flex;
    flex: none;
    justify-content: center;
    padding-block: var(--space-2) var(--space-1);
    touch-action: none;
    user-select: none;
    cursor: grab;
  }

  /* A phone shows a scrollbar only while scrolling; a reserved gutter only narrows the rows. */
  .sheet .scroller {
    scrollbar-gutter: auto;
    scrollbar-width: none;
  }

  .grabber {
    inline-size: 2.25rem;
    block-size: 0.3125rem;
    border-radius: var(--radius-full);
    background: var(--border-strong);
  }

  /* Set only while open: an author `display` would beat the browser's `display: none` for a closed popover. */
  .panel:popover-open {
    display: flex;
    flex-direction: column;
  }

  .scroller {
    min-height: 0;
    padding: var(--space-2);
    /* Only the axis `place()` manages; `overflow: auto` on both made over-wide content grow a horizontal bar instead of wrapping. */
    overflow-x: clip;
    overflow-y: auto;
    scrollbar-gutter: stable;
    overscroll-behavior: contain;
  }

  /*
   * By the trigger it fades in and grows slightly out of its corner, as a menu does on macOS; `place()` measures
   * layout sizes, so the scale never skews where it lands. A phone's sheet slides up from below the screen instead,
   * solid all the way, as `Dialog`'s does, while its dimming fades.
   */
  @supports (overlay: auto) and (transition-behavior: allow-discrete) {
    .panel {
      opacity: 0;
      transform: scale(0.96);
      transition:
        opacity var(--duration-fast) var(--ease-out),
        transform var(--duration-fast) var(--ease-out),
        display var(--duration-fast) allow-discrete,
        overlay var(--duration-fast) allow-discrete;
    }

    .panel:popover-open {
      opacity: 1;
      transform: none;
    }

    .panel.sheet {
      opacity: 1;
      transform: translateY(100%);
      transition:
        transform var(--duration-exit) var(--ease-in),
        display var(--duration-exit) allow-discrete,
        overlay var(--duration-exit) allow-discrete;
    }

    .panel.sheet:popover-open {
      transform: none;
      transition-duration: var(--duration-enter);
      transition-timing-function: var(--ease-spatial);
    }

    .panel.sheet:global(.pulled) {
      transition: none;
    }

    .panel.sheet::backdrop {
      opacity: 0;
      transition:
        opacity var(--duration-exit) var(--ease-out),
        display var(--duration-exit) allow-discrete,
        overlay var(--duration-exit) allow-discrete;
    }

    .panel.sheet:popover-open::backdrop {
      opacity: 1;
      transition-duration: var(--duration-enter);
    }

    @starting-style {
      .panel:popover-open {
        opacity: 0;
        transform: scale(0.96);
      }

      .panel.sheet:popover-open {
        opacity: 1;
        transform: translateY(100%);
      }

      .panel.sheet:popover-open::backdrop {
        opacity: 0;
      }
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .panel,
    .panel::backdrop {
      transition: none;
    }
  }
</style>
