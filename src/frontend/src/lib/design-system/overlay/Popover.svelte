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
</script>

<div class="anchor" style:--anchor-name="--{id}">
  {@render trigger({ popovertarget: id })}

  <div {id} class="panel {placement}" popover="auto">{@render children()}</div>
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

    position-anchor: var(--anchor-name);
    position-area: block-end span-inline-end;
    margin-block-start: var(--space-1);
    /* Flips to the other side when there is no room below, rather than being
       clipped off the bottom of a phone. */
    position-try-fallbacks: flip-block, flip-inline;
  }

  .bottom-end {
    position-area: block-end span-inline-start;
  }

  /* Anchor positioning is recent. Where it is missing the panel falls back to
     ordinary absolute placement under the trigger, which is correct in the
     common case and never invisible. */
  @supports not (anchor-name: --a) {
    .panel {
      position: absolute;
      inset-block-start: calc(100% + var(--space-1));
      inset-inline-start: 0;
    }

    .bottom-end {
      inset-inline: auto 0;
    }
  }
</style>
