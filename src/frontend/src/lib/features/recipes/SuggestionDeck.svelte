<script lang="ts">
  import { untrack } from 'svelte';

  import { IconButton } from '$ds';

  import { m } from '$shell/i18n';
  import FeaturedRecipe from './FeaturedRecipe.svelte';
  import { inheritedFrom } from './recipeMeta';
  import { reasonLinesFor } from './suggestionReason';
  import type { Suggestion } from './types';

  /**
   * Ranked shortlist walked by scrolling, one on screen. Walking writes nothing: a swipe must never mean "never again".
   * A native scroll-snap track (as SimilarRecipes), not a hand-built carousel.
   */
  interface Props {
    items: readonly Suggestion[];
    ondismiss?: (recipeId: string) => void;
    /** Households this one inherits recipes from, by id, with names. */
    inherited?: Readonly<Record<string, string>>;
    /** Asks for the next few; given only while more exist, safe to call while a request runs. */
    onmore?: () => void;
  }

  let { items, ondismiss, inherited = {}, onmore }: Props = $props();

  let track = $state<HTMLUListElement>();
  let scrolled = $state(0);

  const reasons = $derived(reasonLinesFor(items));

  const walkable = $derived(items.length > 1);

  /**
   * The index on screen, clamped rather than corrected: after dismissing the last one the browser fixes the scroll itself,
   * and an effect would race it.
   */
  const at = $derived(Math.max(0, Math.min(items.length - 1, scrolled)));

  // Only after walking there; a single answer is already its own last one.
  $effect(() => {
    if (onmore && walkable && at === items.length - 1) {
      untrack(onmore);
    }
  });

  /** Where the track rests, measured from scroll position: finger, trackpad, Tab and buttons all move it. */
  function follow() {
    if (!track || track.clientWidth === 0) {
      return;
    }

    scrolled = Math.round(track.scrollLeft / track.clientWidth);
  }

  function go(to: number) {
    if (!track) {
      return;
    }

    // No `behavior`: CSS asks for smooth and reduced motion makes it instant; 'smooth' here would override that.
    track.scrollTo({ left: Math.max(0, Math.min(items.length - 1, to)) * track.clientWidth });
  }
</script>

<div class="deck">
  <ul bind:this={track} class="track" class:walkable onscroll={follow}>
    {#each items as suggestion, position (suggestion.id)}
      <li>
        <FeaturedRecipe
          recipe={suggestion}
          reason={reasons[position]}
          priority={position === 0}
          ondismiss={ondismiss ? () => ondismiss(suggestion.id) : undefined}
          from={inheritedFrom(suggestion, inherited)}
        />
      </li>
    {/each}
  </ul>

  {#if walkable}
    <!-- Prev/next and "2 of 5", not dots: the set is ranked, so position is information. -->
    <div class="walk">
      <IconButton
        label={m['suggestions.deck.previous']()}
        size="sm"
        disabled={at === 0}
        onclick={() => go(at - 1)}
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
          <path d="M14.5 5 8 12l6.5 7" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
      </IconButton>

      <p class="position">{m['suggestions.deck.position']({ at: at + 1, of: items.length })}</p>

      <IconButton
        label={m['suggestions.deck.next']()}
        size="sm"
        disabled={at >= items.length - 1}
        onclick={() => go(at + 1)}
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
          <path d="M9.5 5 16 12l-6.5 7" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
      </IconButton>
    </div>
  {/if}
</div>

<style>
  .deck {
    margin-bottom: var(--space-8);
  }

  .track {
    display: grid;
    grid-auto-flow: column;
    grid-auto-columns: 100%;
    margin: 0;
    padding: 0;
    list-style: none;
    scroll-behavior: smooth;
  }

  /* Only with somewhere to go: a lone panel would be a pointless scrollbar and rubber band. */
  .track.walkable {
    overflow-x: auto;
    scroll-snap-type: x mandatory;
    overscroll-behavior-x: contain;
    scrollbar-width: none;
  }

  .track.walkable::-webkit-scrollbar {
    display: none;
  }

  /* Snapping fights keyboard walking: each Tab would be undone by the browser re-settling. */
  .track:focus-within {
    scroll-snap-type: none;
  }

  .track > li {
    min-width: 0;
    scroll-snap-align: start;
  }

  .walk {
    display: flex;
    align-items: center;
    justify-content: flex-end;
    gap: var(--space-2);
    margin-top: var(--space-3);
  }

  .position {
    color: var(--text-muted);
    font-size: var(--text-xs);
    font-variant-numeric: tabular-nums;
  }

  /* A phone's library should start sooner than a desktop's. */
  @media (width < 48rem) {
    .deck {
      margin-bottom: var(--space-6);
    }

    .walk {
      margin-top: var(--space-2);
    }
  }

  /* Paper has nothing to swipe. */
  @media print {
    .track {
      grid-auto-flow: row;
      grid-auto-columns: auto;
    }

    .track > li:not(:first-child),
    .walk {
      display: none;
    }
  }
</style>
