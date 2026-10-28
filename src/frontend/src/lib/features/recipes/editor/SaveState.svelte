<script lang="ts" module>
  /** Where the work is; `local` is not a failure and is never drawn as one. */
  export type SaveTone = 'idle' | 'saving' | 'saved' | 'local' | 'failed' | 'conflict';
</script>

<script lang="ts">
  /**
   * Whether the work is safe, for an editor with no Save button: always present at one size so nothing shifts when typing stops.
   * Colour is never the only carrier: the word says it too, and `role="status"` says it to a screen reader once typing pauses.
   */
  interface Props {
    tone: SaveTone;
    text: string;
  }

  let { tone, text }: Props = $props();
</script>

<p class="state {tone}" role="status" title={text}>
  <span class="dot" aria-hidden="true"></span>
  <span class="text">{text}</span>
</p>

<style>
  .state {
    position: relative;
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

  /* Somebody else's change is the only state that needs a decision, so it must read at a glance. */
  .conflict .text,
  .failed .text {
    white-space: normal;
  }

  /* On a phone the steady states are the dot alone, so the recipe's name keeps the rail; the word stays for a screen reader and as the tooltip. A state that needs a decision keeps its words. */
  @media (width < 64rem) {
    .idle .text,
    .saved .text,
    .saving .text {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip-path: inset(50%);
      white-space: nowrap;
    }
  }

  @keyframes breathe {
    50% {
      opacity: 0.35;
    }
  }

  /* A steady dot instead of a slower pulse; "saving" is carried by the word. */
  @media (prefers-reduced-motion: reduce) {
    .saving .dot {
      animation: none;
    }
  }
</style>
