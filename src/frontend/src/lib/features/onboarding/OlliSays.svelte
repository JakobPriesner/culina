<script lang="ts">
  import type { Snippet } from 'svelte';

  import Olli from '$shell/olli/Olli.svelte';
  import type { Pose } from '$shell/olli/poses';

  /**
   * Olli saying something: the pot, and a speech bubble pointing at it. Olli stays hidden from screen
   * readers (see `Olli`); the bubble's words are the content, announced when they change if `live`.
   */
  interface Props {
    pose: Pose;
    size?: 'sm' | 'md' | 'lg';
    /** `stacked` puts the bubble under Olli, for a narrow column. */
    layout?: 'beside' | 'stacked';
    live?: boolean;
    children: Snippet;
  }

  let { pose, size = 'md', layout = 'beside', live = false, children }: Props = $props();
</script>

<div class="says {layout} {size}">
  <Olli {pose} {size} />
  <div class="bubble" aria-live={live ? 'polite' : undefined}>
    {@render children()}
  </div>
</div>

<style>
  .says {
    --tail: 0.6rem;
    display: flex;
    align-items: center;
    gap: calc(var(--tail) + var(--space-2));
  }

  .stacked {
    flex-direction: column;
    gap: calc(var(--tail) + var(--space-1));
  }

  .bubble {
    position: relative;
    min-width: 0;
    padding: var(--space-3) var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
    box-shadow: var(--shadow-card);
    color: var(--text);
    line-height: var(--leading-normal);
    transform-origin: left center;
    animation: speak var(--duration-slow) var(--ease-spatial);
  }

  .stacked .bubble {
    transform-origin: center top;
    text-align: center;
  }

  /* The tail: a square turned on its corner, half of it showing, with the bubble's own border. */
  .bubble::before {
    content: '';
    position: absolute;
    top: calc(50% - var(--tail) / 2);
    inset-inline-start: calc(var(--tail) / -2 - 1px);
    width: var(--tail);
    height: var(--tail);
    border-inline-start: 1px solid var(--border);
    border-bottom: 1px solid var(--border);
    background: inherit;
    transform: rotate(45deg);
  }

  .stacked .bubble::before {
    top: calc(var(--tail) / -2 - 1px);
    inset-inline-start: calc(50% - var(--tail) / 2);
    border-bottom: none;
    border-top: 1px solid var(--border);
  }

  .sm .bubble {
    padding: var(--space-2) var(--space-3);
    font-size: var(--text-sm);
  }

  .lg .bubble {
    font-size: var(--text-lg);
  }

  .bubble :global(p) {
    margin: 0;
  }

  .bubble :global(p + p) {
    margin-top: var(--space-1);
  }

  @keyframes speak {
    from {
      opacity: 0;
      transform: scale(0.94);
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .bubble {
      animation: none;
    }
  }
</style>
