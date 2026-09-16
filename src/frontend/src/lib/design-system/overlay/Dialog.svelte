<script lang="ts">
  import type { Snippet } from 'svelte';

  import IconButton from '../actions/IconButton.svelte';
  import { lockScroll, unlockScroll } from './scrollLock';

  /**
   * The one modal surface. `Modal` and `Sheet` are this with different
   * geometry; no feature builds its own.
   *
   * Built on the native `<dialog>` because the browser already does the parts
   * that are laborious and easy to get subtly wrong: it traps focus, makes the
   * rest of the page inert, puts the dialog in the top layer above every
   * stacking context, closes on Escape, and restores focus to whatever opened
   * it. A hand-rolled version of that is a few hundred lines and is still worse
   * on a screen reader.
   *
   * What is left to do by hand: locking the page's scroll, labelling, and
   * keeping the caller's `open` in step with the browser's own closing.
   */
  interface Props {
    open: boolean;
    /** Announced as the dialog's name. Never optional: an unnamed dialog is a box. */
    title: string;
    children: Snippet;
    /** Actions, kept out of the scrolling body so they stay reachable. */
    footer?: Snippet;
    closeLabel: string;
    /** The visual shape. `sheet` rises from the bottom edge on a phone. */
    placement?: 'centre' | 'sheet';
    /** Hides the heading visually while still naming the dialog. */
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
    hideTitle = false,
    onclose
  }: Props = $props();

  let element = $state<HTMLDialogElement>();

  const id = $props.id();
  const titleId = `${id}-title`;

  $effect(() => {
    if (!element) {
      return;
    }

    if (open && !element.open) {
      element.showModal();
      lockScroll();

      return () => unlockScroll();
    }

    if (!open && element.open) {
      element.close();
    }

    return undefined;
  });

  /** The browser closed it — Escape, or the close method. Tell the caller. */
  function synchronise() {
    if (open) {
      dismiss();
    }
  }

  /**
   * Closed, and the caller told about it.
   *
   * Every way out goes through here. A caller that passes `open` as an
   * expression rather than a binding — `open={chosen !== null}` — only ever
   * learns that this closed from `onclose`, so a dismissal that skipped it
   * would leave that caller believing the dialog was still up, and the next
   * open would set state that was already set and change nothing on screen.
   */
  function dismiss() {
    open = false;
    onclose?.();
  }

  /**
   * A click on the backdrop lands on the dialog element itself, because the
   * backdrop is not a node. Anything inside stops at its own element.
   */
  function dismissOnBackdrop(event: MouseEvent) {
    if (event.target === element) {
      dismiss();
    }
  }
</script>

<dialog
  bind:this={element}
  class="dialog {placement}"
  aria-labelledby={titleId}
  onclose={synchronise}
  onclick={dismissOnBackdrop}
>
  <div class="panel">
    <header class="header">
      <h2 class="title" class:ds-clipped={hideTitle} id={titleId}>{title}</h2>

      <!-- The backdrop and Escape both dismiss, but neither is discoverable:
           a visible control is the way out that can be seen. -->
      <IconButton label={closeLabel} onclick={dismiss}>
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="m6 6 12 12M18 6 6 18" stroke-linecap="round" />
        </svg>
      </IconButton>
    </header>

    <div class="body">{@render children()}</div>

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
    scroll-padding-block: var(--space-2);
    overscroll-behavior: contain;
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

  .centre {
    width: min(32rem, calc(100vw - var(--space-8)));
    margin: auto;
  }

  .centre .panel {
    max-height: calc(100dvh - var(--space-16));
    border-radius: var(--radius-lg);
  }

  /*
   * One component, two shapes. On a phone a sheet rises from the bottom edge,
   * where a thumb already is; on a larger screen the same content is a centred
   * dialog, because a full-width strip along the bottom of a desktop window is
   * a long way from where the eye is.
   */
  .sheet {
    width: 100vw;
    max-width: 100vw;
    margin: auto auto 0;
  }

  .sheet .panel {
    max-height: 85dvh;
    border-start-start-radius: var(--radius-lg);
    border-start-end-radius: var(--radius-lg);
    padding-bottom: env(safe-area-inset-bottom, 0);
    padding-inline: env(safe-area-inset-left, 0px) env(safe-area-inset-right, 0px);
  }

  @media (min-width: 48rem) {
    .sheet {
      width: min(32rem, calc(100vw - var(--space-8)));
      max-width: none;
      margin: auto;
    }

    .sheet .panel {
      max-height: calc(100dvh - var(--space-16));
      border-radius: var(--radius-lg);
      padding-bottom: 0;
    }
  }

  /* Both shapes arrive from where they will be, so the movement explains the
     relationship rather than decorating it. */
  .dialog[open] .panel {
    animation: rise var(--duration-base) var(--ease-spatial);
  }

  @keyframes rise {
    from {
      opacity: 0;
      transform: translateY(var(--space-4));
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .dialog[open] .panel {
      animation: none;
    }
  }
</style>
