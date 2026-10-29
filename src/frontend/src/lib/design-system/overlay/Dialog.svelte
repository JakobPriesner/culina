<script lang="ts">
  import type { Snippet } from 'svelte';

  import IconButton from '../actions/IconButton.svelte';
  import { lockScroll, unlockScroll } from './scrollLock';
  import { followed, releasesSheet } from './sheetDrag';

  /**
   * The one modal surface; `Modal` and `Sheet` are this with different geometry.
   * Built on native `<dialog>` for focus trap, inert page, top layer and Escape; scroll lock,
   * labelling and syncing `open` are manual.
   */
  interface Props {
    open: boolean;
    /** Announced as the dialog's name. */
    title: string;
    children: Snippet;
    footer?: Snippet;
    closeLabel: string;
    /** `stage` takes the whole phone screen and a large centred panel elsewhere: a moment, not a task (a welcome). */
    placement?: 'centre' | 'sheet' | 'stage';
    wide?: boolean;
    /** `top` holds a sheet near the top on wide screens, so a panel that grows (search results) grows downward instead of jumping. */
    anchor?: 'centre' | 'top';
    /**
     * `large` gives a phone's sheet its full height from the start (iOS's large detent), so results that arrive
     * never make it jump; for a sheet that searches. `fit` hugs what it holds.
     */
    size?: 'fit' | 'large';
    /** The panel takes the page's colour, so raised `ListSection` cards read on it as they do on a page. */
    grouped?: boolean;
    hideTitle?: boolean;
    onclose?: () => void;
  }

  let {
    open = $bindable(),
    title,
    children,
    footer,
    closeLabel,
    placement = 'centre',
    wide = false,
    anchor = 'centre',
    size = 'fit',
    grouped = false,
    hideTitle = false,
    onclose
  }: Props = $props();

  let element = $state<HTMLDialogElement>();
  let panel = $state<HTMLDivElement>();

  /**
   * Shown since it last finished closing; not reactive, only decides whether an exit is awaited.
   */
  let shown = false;

  let closing = $state(false);

  const id = $props.id();
  const titleId = `${id}-title`;

  // The body is built only while up or leaving, so unused sheets cost nothing; `.pre` keeps it for
  // the exit's first frame.
  $effect.pre(() => {
    if (open) {
      shown = true;
    } else if (shown) {
      closing = true;
    }
  });

  $effect(() => {
    if (!element) {
      return;
    }

    if (open && !element.open) {
      // A sheet last let go by a pull starts from where it rests, not where the finger left it.
      panel?.style.removeProperty('transform');
      element.showModal();
      lockScroll();

      return () => unlockScroll();
    }

    if (!open) {
      if (element.open) {
        element.close();
      }

      if (closing) {
        void finishExit(element);
      }
    }

    return undefined;
  });

  async function finishExit(dialog: HTMLDialogElement) {
    await Promise.allSettled(dialog.getAnimations?.().map((animation) => animation.finished) ?? []);

    if (!open) {
      shown = false;
    }

    closing = false;
  }

  function synchronise() {
    if (open) {
      dismiss();
    }
  }

  /**
   * Closed and the caller told; every way out goes through here since `open={expr}` callers only
   * learn via `onclose`.
   */
  function dismiss() {
    open = false;
    onclose?.();
  }

  /** Where the press that ends in the next click began; not state, nothing renders from it. */
  let pressedOnBackdrop = false;

  function pressBackdrop(event: PointerEvent) {
    pressedOnBackdrop = event.target === element;
  }

  /**
   * A backdrop click lands on the dialog element itself, as the backdrop is not a node.
   * Only a press that began there counts: selecting text in a field and letting go over the backdrop
   * also clicks the dialog (the common ancestor), and must not throw the form away.
   */
  function dismissOnBackdrop(event: MouseEvent) {
    const pressed = pressedOnBackdrop;

    pressedOnBackdrop = false;

    if (pressed && event.target === element) {
      dismiss();
    }
  }

  /** A pull on a phone's sheet by its top, under way; not state, the panel's transform is set directly as Vaul does. */
  let pull: { pointer: number; start: number; last: number; at: number; velocity: number } | null =
    null;

  /** Only a phone's sheet is pulled: elsewhere the panel is a centred dialog with nothing to pull it down to. */
  const pullable = () =>
    placement === 'sheet' && window.matchMedia?.('(width < 48rem)').matches === true;

  function grab(event: PointerEvent) {
    if (!panel || !event.isPrimary || event.button !== 0 || !pullable()) return;
    if (event.target instanceof Element && event.target.closest('button, a, input')) return;

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
      dismiss();
    } else {
      panel.style.removeProperty('transform');
    }
  }
</script>

<dialog
  class:wide
  class:top={anchor === 'top'}
  class:large={size === 'large'}
  class:grouped
  bind:this={element}
  class="dialog {placement}"
  aria-labelledby={titleId}
  aria-hidden={open ? undefined : true}
  inert={!open}
  onclose={synchronise}
  onpointerdown={pressBackdrop}
  onclick={dismissOnBackdrop}
>
  <div class="panel" bind:this={panel}>
    <!-- A sheet is pulled down by its top, grabber and title, never by its content, which scrolls. The close button
         stays the way out that can be seen and reached by keyboard. -->
    <div
      class="top-edge"
      role="presentation"
      onpointerdown={grab}
      onpointermove={follow}
      onpointerup={release}
      onpointercancel={release}
    >
      {#if placement === 'sheet'}<div class="grabber" aria-hidden="true"></div>{/if}

      <header class="header">
        <h2 class="title" class:ds-clipped={hideTitle} id={titleId}>{title}</h2>

        <IconButton label={closeLabel} onclick={dismiss}>
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="m6 6 12 12M18 6 6 18" stroke-linecap="round" />
          </svg>
        </IconButton>
      </header>
    </div>

    <div class="body">
      {#if open || closing}
        {@render children()}
      {/if}
    </div>

    {#if footer}
      <footer class="footer">{@render footer()}</footer>
    {/if}
  </div>
</dialog>

<style>
  .dialog {
    padding: 0;
    border: none;
    background: transparent;
    color: var(--text);
    max-width: none;
    max-height: none;
  }

  .dialog::backdrop {
    background: var(--scrim);
  }

  .panel {
    display: flex;
    flex-direction: column;
    background: var(--surface-overlay);
    --field-surface: var(--field-surface-inset);
    box-shadow: var(--shadow-overlay);
    overflow: hidden;
  }

  .header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    flex-shrink: 0;
    gap: var(--space-4);
    padding: var(--space-4) var(--space-4) var(--space-2) var(--space-6);
  }

  .title {
    font-size: var(--text-xl);
  }

  .body {
    padding: var(--space-2) var(--space-6) var(--space-6);
    min-height: 0;
    overflow-y: auto;
    /* The gutter stops the body sliding sideways when its content outgrows the dialog. */
    scrollbar-gutter: stable;
    scroll-padding-block: var(--space-2);
    overscroll-behavior: contain;
  }

  /*
   * A body whose content manages its own height stops being the scroller, so a pinned search field
   * doesn't produce two scrollbars.
   */
  .body:has(> :global([data-fills-dialog])) {
    display: flex;
    flex-direction: column;
    overflow-y: hidden;
    scrollbar-gutter: auto;
  }

  .body > :global([data-fills-dialog]) {
    min-height: 0;
    flex: 1;
  }

  /* On a landscape phone the pinned parts would leave the list no height, so the whole body scrolls instead. */
  @media (max-height: 30rem) {
    .body:has(> :global([data-fills-dialog])) {
      display: block;
      overflow-y: auto;
      scrollbar-gutter: stable;
    }

    .body > :global([data-fills-dialog]) {
      min-height: auto;
    }
  }

  .footer {
    display: flex;
    flex-shrink: 0;
    flex-wrap: wrap;
    justify-content: flex-end;
    gap: var(--space-3);
    padding: var(--space-4) var(--space-6);
    border-top: 1px solid var(--border);
  }

  /* On a phone, in either orientation, the footer is a strip, not a third of the dialog. */
  @media (max-width: 30rem), (max-height: 30rem) {
    .footer {
      padding: var(--space-3) var(--space-4);
    }
  }

  .centre {
    width: min(32rem, calc(100vw - var(--space-8)));
    margin: auto;
  }

  .centre .panel {
    max-height: calc(100dvh - var(--space-16));
    border-radius: var(--radius-xl);
  }

  .sheet {
    width: 100vw;
    max-width: 100vw;
    margin: auto auto 0;
  }

  .sheet .panel {
    max-height: 85dvh;
    border-start-start-radius: var(--radius-xl);
    border-start-end-radius: var(--radius-xl);
    padding-bottom: env(safe-area-inset-bottom, 0);
    padding-inline: env(safe-area-inset-left, 0px) env(safe-area-inset-right, 0px);
    /* Settles back from a pull that did not let it go; a pull in progress follows the finger with no lag. */
    transition: transform var(--duration-enter) var(--ease-spatial);
  }

  .sheet .panel:global(.pulled) {
    transition: none;
  }

  /* The pull starts on the sheet's top, so the browser must not take that drag for a scroll of the page. */
  .sheet .top-edge {
    touch-action: none;
    user-select: none;
  }

  /* The sheet's handle, as iOS and Material draw it: says "this pulls down" before anyone tries. */
  .grabber {
    inline-size: 2.25rem;
    block-size: 0.3125rem;
    margin: var(--space-2) auto 0;
    border-radius: var(--radius-full);
    background: var(--border-strong);
  }

  .sheet .header {
    padding-block-start: var(--space-2);
  }

  /* iOS's large detent: the whole height but a strip of the page above it, so it still reads as a sheet over it. */
  .sheet.large .panel {
    block-size: calc(100dvh - max(env(safe-area-inset-top, 0px), var(--space-3)) - var(--space-3));
    max-height: none;
  }

  .large .body {
    flex: 1;
  }

  .grouped .panel {
    background: var(--surface);
  }

  /*
   * A phone's sheet floats inset from the screen's edges with every corner rounded, concentric with the screen's own,
   * so the page shows around it, as an iOS sheet does. At its large detent it is flush and full width again, as iOS's
   * is once expanded: the whole height, the task in focus.
   */
  @media (width < 48rem) {
    .sheet:not(.large) {
      width: calc(100vw - 2 * var(--screen-inset));
      margin-block-end: var(--screen-inset);
    }

    .sheet:not(.large) .panel {
      border-radius: calc(var(--radius-screen) - var(--screen-inset));
      /* The home indicator's strip, less the gap the sheet already keeps below it. */
      padding-bottom: max(0px, calc(env(safe-area-inset-bottom, 0px) - var(--screen-inset)));
      padding-inline: max(0px, calc(env(safe-area-inset-left, 0px) - var(--screen-inset)))
        max(0px, calc(env(safe-area-inset-right, 0px) - var(--screen-inset)));
    }
  }

  /* A phone shows a scrollbar only while scrolling; a reserved gutter only narrows the sheet. */
  @media (width < 48rem) {
    .sheet .body {
      scrollbar-gutter: auto;
      scrollbar-width: none;
    }

    .sheet .body::-webkit-scrollbar {
      display: none;
    }
  }

  .stage {
    width: 100vw;
    max-width: 100vw;
    height: 100dvh;
    margin: 0;
  }

  .stage .panel {
    height: 100%;
    padding: env(safe-area-inset-top, 0) env(safe-area-inset-right, 0)
      env(safe-area-inset-bottom, 0) env(safe-area-inset-left, 0);
  }

  .stage .body {
    flex: 1;
  }

  @media (min-width: 48rem) and (min-height: 36rem) {
    .stage {
      width: min(56rem, calc(100vw - var(--space-16)));
      height: min(46rem, calc(100dvh - var(--space-16)));
      margin: auto;
    }

    .stage .panel {
      border-radius: var(--radius-xl);
      padding: 0;
    }
  }

  .centre.wide {
    width: min(64rem, calc(100vw - var(--space-8)));
  }

  @media (min-width: 48rem) {
    .sheet {
      width: min(32rem, calc(100vw - var(--space-8)));
      max-width: none;
      margin: auto;
    }

    /* Like Spotlight: a fixed top edge, growing down. */
    .sheet.top {
      margin-block: 12dvh auto;
    }

    .sheet .panel {
      max-height: calc(100dvh - var(--space-16));
      border-radius: var(--radius-xl);
      padding-bottom: 0;
    }

    /* A dialog here, with nothing to pull: no handle, and its height is what it holds again. */
    .grabber {
      display: none;
    }

    .sheet .header {
      padding-block-start: var(--space-4);
    }

    .sheet.large .panel {
      block-size: auto;
    }
    .sheet.top .panel {
      max-height: calc(88dvh - var(--space-8));
    }
  }

  /*
   * Keep native focus/closing semantics; the surface stays in the top layer just long enough to
   * paint its exit.
   */
  @supports (overlay: auto) and (transition-behavior: allow-discrete) {
    /*
     * A dialog on a wide screen appears where it stands, a fade and a slight grow, as a macOS sheet or alert does:
     * rising into place read as something swiped up from below that was never there.
     */
    .dialog {
      --dialog-travel: 0px;
      --dialog-scale: 0.97;
      opacity: 0;
      transform: translateY(var(--dialog-travel)) scale(var(--dialog-scale));
      transition:
        opacity var(--duration-exit) var(--ease-in),
        transform var(--duration-exit) var(--ease-in),
        display var(--duration-exit) allow-discrete,
        overlay var(--duration-exit) allow-discrete;
    }

    .dialog[open] {
      opacity: 1;
      transform: none;
      transition-duration: var(--duration-enter);
      transition-timing-function: var(--ease-spatial);
    }

    .dialog::backdrop {
      opacity: 0;
      transition:
        opacity var(--duration-exit) var(--ease-out),
        display var(--duration-exit) allow-discrete,
        overlay var(--duration-exit) allow-discrete;
    }

    .dialog[open]::backdrop {
      opacity: 1;
      transition-duration: var(--duration-enter);
    }

    @starting-style {
      .dialog[open] {
        opacity: 0;
        transform: translateY(var(--dialog-travel)) scale(var(--dialog-scale));
      }

      .dialog[open]::backdrop {
        opacity: 0;
      }
    }

    /*
     * A phone's sheet slides in from below the screen and back out, solid all the way, as an iOS sheet does: it
     * comes from the edge it is pulled back down to. Only the dimming behind it fades.
     */
    @media (width < 48rem) {
      .sheet {
        /* Past the gap an inset sheet keeps, so none of it shows before it rises. */
        --dialog-travel: calc(100% + var(--screen-inset));
        --dialog-scale: 1;
        opacity: 1;
      }

      @starting-style {
        .sheet[open] {
          opacity: 1;
        }
      }
    }
  }

  @supports not (overlay: auto) {
    .dialog[open] .panel {
      animation: rise var(--duration-enter) var(--ease-spatial);
    }
  }

  @keyframes rise {
    from {
      opacity: 0;
      transform: translateY(var(--space-4));
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .dialog,
    .dialog::backdrop {
      transition: none;
      transform: none;
    }

    .dialog[open] .panel {
      animation: none;
    }
  }
</style>
