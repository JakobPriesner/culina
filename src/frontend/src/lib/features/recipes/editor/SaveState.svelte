<script lang="ts" module>
  /**
   * Where the work is, in the order that matters.
   *
   * `local` is not a failure and is never drawn as one: the text is on this
   * device and will be sent, which is a sentence about where something is, not
   * about something going wrong.
   */
  export type SaveTone = 'idle' | 'saving' | 'saved' | 'local' | 'failed' | 'conflict';
</script>

<script lang="ts">
  /**
   * The only thing on this screen that says whether the work is safe.
   *
   * An editor with no Save button owes the reader this, and owes it
   * continuously — so the row is always here, at one size, in one place, and
   * changes only its dot and its word. The version that appeared and
   * disappeared moved everything under it by a line every time somebody stopped
   * typing, and was invisible exactly when it mattered.
   *
   * Colour is never the only carrier: the word beside the dot says the same
   * thing, and `role="status"` says it to a screen reader once the typing has
   * paused.
   */
  interface Props {
    tone: SaveTone;
    text: string;
  }

  let { tone, text }: Props = $props();
</script>

<p class="state {tone}" role="status">
  <span class="dot" aria-hidden="true"></span>
  <span class="text">{text}</span>
</p>

<style>
  .state {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    min-width: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .text {
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .dot {
    flex: none;
    width: var(--space-2);
    height: var(--space-2);
    border-radius: var(--radius-full);
    background: var(--border-strong);
    transition: background-color var(--duration-base) var(--ease-out);
  }

  .saving .dot {
    background: var(--accent);
    animation: breathe 1.2s var(--ease-out) infinite;
  }

  .saved .dot {
    background: var(--success);
  }

  .local .dot {
    background: var(--warning);
  }

  .failed .dot,
  .conflict .dot {
    background: var(--danger);
  }

  .failed,
  .conflict {
    color: var(--text-danger);
  }

  /* The one place the whole line has to be readable at a glance rather than
     scanned: somebody else's change is the only state that needs a decision. */
  .conflict .text,
  .failed .text {
    white-space: normal;
  }

  @keyframes breathe {
    50% {
      opacity: 0.35;
    }
  }

  /* A steady dot rather than a slower pulse: "saving" is carried by the word,
     and the animation is only there to make it legible out of the corner of
     an eye. */
  @media (prefers-reduced-motion: reduce) {
    .saving .dot {
      animation: none;
    }
  }
</style>
