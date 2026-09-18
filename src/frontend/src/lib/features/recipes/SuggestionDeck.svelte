<script lang="ts">
  import { IconButton } from '$ds';

  import { m } from '$shell/i18n';
  import FeaturedRecipe from './FeaturedRecipe.svelte';
  import { reasonLineFor } from './suggestionReason';
  import type { Suggestion } from './types';

  /**
   * The shortlist, one on screen, walked with a finger.
   *
   * The panel used to hold the single best answer and offer two verbs: open it,
   * or stop suggesting it. The second is a permanent, written-down decision —
   * so there was no way to say the ordinary thing, "not tonight, what else is
   * there?". This is that way, and it writes nothing.
   *
   * Which is the whole point, and the reason this is a scroller rather than a
   * deck of cards thrown over the shoulder. The ranking has no non-click signal
   * on purpose: with two to eight people in a household, a recipe somebody
   * scrolled past means nothing at all. A swipe that quietly meant "never
   * again" would be inventing the one signal the backend refused to invent, and
   * would say the same thing as the Dismiss control two centimetres away — one
   * of them silently, which is the worse of the two.
   *
   * So it is a shortlist and not a deck: five ranked answers that stay put, walk
   * in both directions, and are the same five all evening — the server answers
   * by the day rather than the instant for exactly that reason. Swiping back to
   * the one you passed is the behaviour that makes this a list rather than a
   * feed.
   *
   * No carousel was written. `SimilarRecipes` already settled that question for
   * this app: a scroll-snap track is swipe, momentum, rubber-banding,
   * back-swipe, trackpads, keyboards and reduced motion, all of them native and
   * none of them ours to get wrong.
   */
  interface Props {
    /** Best first. One of them renders the page exactly as it was before. */
    items: readonly Suggestion[];
    /** Stops suggesting one. The only thing here that writes anything down. */
    ondismiss?: (recipeId: string) => void;
  }

  let { items, ondismiss }: Props = $props();

  let track = $state<HTMLUListElement>();
  let scrolled = $state(0);

  /** Below two there is nothing to walk, and a control row would be furniture. */
  const walkable = $derived(items.length > 1);

  /**
   * Which one is on screen.
   *
   * Clamped rather than corrected. Dismissing the last of five leaves the track
   * scrolled past the end of four; the browser puts that right itself and says
   * so with a scroll event, and until it does this reads one short instead of
   * off the end. An effect that scrolled it back would be a second thing moving
   * the track, racing the browser to move it the same way.
   */
  const at = $derived(Math.max(0, Math.min(items.length - 1, scrolled)));

  /**
   * Where the track has come to rest.
   *
   * Measured off the scroll position rather than remembered from the last
   * button press, because the finger, the trackpad, Tab and the buttons all
   * move it and only one of those is ours. A remembered index is a second
   * opinion about where the track is, and it is wrong the first time somebody
   * uses the gesture this whole component exists for.
   */
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

    // No `behavior`, deliberately: the track asks for smooth in CSS, and the
    // app-wide reduced-motion rule turns that into an instant jump. Passing
    // 'smooth' here would scroll smoothly past somebody who asked it not to.
    track.scrollTo({ left: Math.max(0, Math.min(items.length - 1, to)) * track.clientWidth });
  }
</script>

<div class="deck">
  <ul bind:this={track} class="track" class:walkable onscroll={follow}>
    {#each items as suggestion, position (suggestion.id)}
      <li>
        <FeaturedRecipe
          recipe={suggestion}
          reason={reasonLineFor(suggestion)}
          priority={position === 0}
          ondismiss={ondismiss ? () => ondismiss(suggestion.id) : undefined}
        />
      </li>
    {/each}
  </ul>

  {#if walkable}
    <!--
      Prev, next, and which of how many. Not dots: the set is ranked, so the
      position is a fact about the answer rather than decoration, and "2 of 5"
      says it where five circles only say "there are more".
    -->
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

  /* Only once there is somewhere to go. A single panel in a scroll container
     is a scrollbar's worth of nothing, and on a phone it is a rubber band that
     moves the one thing on the page for no reason. */
  .track.walkable {
    overflow-x: auto;
    scroll-snap-type: x mandatory;
    overscroll-behavior-x: contain;
    scrollbar-width: none;
  }

  .track.walkable::-webkit-scrollbar {
    display: none;
  }

  /* Snapping fights a keyboard walking the track: each Tab would be undone by
     the browser settling the scroll somewhere else. The same trade the
     cookbook shelf makes. */
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

  /* Paper has nothing to swipe, and printing five heroes to read one is five
     sheets of somebody's paper. */
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
