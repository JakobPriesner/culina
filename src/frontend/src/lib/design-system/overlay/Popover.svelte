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
   * For a decision that must be answered, use `Sheet` or `Modal` instead. A
   * popover that must not be dismissed is a modal wearing the wrong clothes.
   */
  interface Props {
    /** The control that opens it. Receives the attributes that pair the two. */
    trigger: Snippet<[{ popovertarget: string }]>;
    children: Snippet;
    /** Which side of the trigger it prefers. Flips if there is no room. */
    placement?: 'bottom-start' | 'bottom-end';
  }

  let { trigger, children, placement = 'bottom-start' }: Props = $props();

  const id = $props.id();
  let anchor: HTMLDivElement;
  let open = $state(false);
  let detached = $state(false);

  $effect(() => {
    if (!open) return;
    // Anchor fallbacks account for layout overflow, but an open panel can
    // follow its trigger off-screen when the document scrolls or reflows.
    const observer = new IntersectionObserver(([entry]) => {
      if (entry) detached = !entry.isIntersecting;
    });
    observer.observe(anchor);
    return () => observer.disconnect();
  });
</script>

<div class="anchor" bind:this={anchor} style:--anchor-name="--{id}">
  {@render trigger({ popovertarget: id })}

  <div
    {id}
    class="panel {placement}"
    class:detached
    popover="auto"
    ontoggle={(event) => {
      open = event.newState === 'open';
      if (!open) detached = false;
    }}
  >
    {@render children()}
  </div>
</div>

<style>
  .anchor {
    position: relative;
    display: inline-flex;
    anchor-name: var(--anchor-name);
  }

  .panel {
    margin: 0;
    padding: var(--space-2);
    border: 1px solid var(--border);
    border-radius: var(--radius-md);
    background: var(--surface-overlay);
    color: var(--text);
    box-shadow: var(--shadow-overlay);
    max-width: calc(100dvw - 2 * var(--space-4));
    /* The position area supplies the available space beside the anchor. A
       long panel must shrink and scroll when neither side fits its content. */
    max-height: stretch;
    overflow: auto;
    overscroll-behavior: contain;

    position-anchor: var(--anchor-name);
    position-area: block-end span-inline-end;
    margin-block-start: var(--space-1);
    /* Flips to the other side when there is no room below, rather than being
       clipped off the bottom of a phone. */
    position-try-fallbacks:
      flip-block,
      flip-inline,
      flip-block flip-inline,
      --popover-viewport;
  }

  .bottom-end {
    position-area: block-end span-inline-start;
  }

  .panel.detached {
    position-anchor: auto;
    position-area: none;
    inset: var(--space-4);
    margin: auto;
    width: max-content;
    height: max-content;
    max-height: calc(100dvh - 2 * var(--space-4));
  }

  /* Keep the choices reachable even when no anchored position fits. */
  @position-try --popover-viewport {
    position-area: none;
    inset: var(--space-4);
    margin: auto;
    width: max-content;
    height: max-content;
    max-height: calc(100dvh - 2 * var(--space-4));
  }

  /* Browsers without sized anchor areas get the same viewport fallback. */
  @supports not ((anchor-name: --a) and (max-height: stretch)) {
    .panel {
      position: fixed;
      position-area: none;
      inset: var(--space-4);
      margin: auto;
      width: max-content;
      height: max-content;
      max-height: calc(100dvh - 2 * var(--space-4));
    }
  }
</style>
