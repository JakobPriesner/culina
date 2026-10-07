<script lang="ts">
  import type { Snippet } from 'svelte';

  import IconButton from '../actions/IconButton.svelte';
  import { lockScroll, unlockScroll } from './scrollLock';

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
    placement?: 'centre' | 'sheet';
    wide?: boolean;
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
    hideTitle = false,
    onclose
  }: Props = $props();

  let element = $state<HTMLDialogElement>();

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

  /** A backdrop click lands on the dialog element itself, as the backdrop is not a node. */
  function dismissOnBackdrop(event: MouseEvent) {
    if (event.target === element) {
      dismiss();
    }
  }
</script>

<dialog
  class:wide
  bind:this={element}
  class="dialog {placement}"
  aria-labelledby={titleId}
  onclose={synchronise}
  onclick={dismissOnBackdrop}
>
  <div class="panel">
    <header class="header">
      <h2 class="title" class:ds-clipped={hideTitle} id={titleId}>{title}</h2>

      <IconButton label={closeLabel} onclick={dismiss}>
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="m6 6 12 12M18 6 6 18" stroke-linecap="round" />
        </svg>
      </IconButton>
    </header>

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

  .centre.wide {
    width: min(64rem, calc(100vw - var(--space-8)));
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

  /*
   * Keep native focus/closing semantics; the surface stays in the top layer just long enough to
   * paint its exit.
   */
  @supports (overlay: auto) and (transition-behavior: allow-discrete) {
    .dialog {
      --dialog-travel: var(--space-2);
      --dialog-scale: 0.985;
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

    @media (width < 48rem) {
      .sheet {
        --dialog-travel: var(--space-8);
        --dialog-scale: 1;
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
