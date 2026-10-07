<script lang="ts">
  import RailSectionNav from './RailSectionNav.svelte';
  import SaveState, { type SaveTone } from './SaveState.svelte';
  import { createSectionWatch } from './sectionWatch.svelte';

  /**
   * Where you are in the recipe, and whether the work is safe.
   *
   * One component and two shapes, which is deliberate. On a wide screen it is a
   * column beside the form — the way out, what is being edited, whether it is
   * saved, and the parts of the recipe with their counts — and it follows the
   * page down, because a recipe with twelve steps is long and a way out you
   * have to scroll back up for is not one. On a phone the same four facts
   * become a bar across the top; the section links go, because five pills above
   * a form on a 360px screen cost more room than the scrolling they save.
   *
   * The bar is glass and rounded for the same reason the brand and the
   * navigation are: this app's fixed furniture floats over the page rather than
   * cutting a band out of it, and an editor that invented its own toolbar would
   * read as a different application's screen.
   */
  interface Props {
    /** Where "done" goes. Leaving the editor is never a save; it is a link. */
    backHref: string;
    backLabel: string;
    /** What is being edited. Empty until the recipe has a name. */
    title: string;
    /** Stands in for the title before there is one. */
    untitled: string;
    tone: SaveTone;
    status: string;
    /** Names the section nav, which is a list of links without a heading. */
    sectionsLabel: string;
    sections: readonly { id: string; label: string; count?: number }[];
  }

  let { backHref, backLabel, title, untitled, tone, status, sectionsLabel, sections }: Props =
    $props();

  const watch = createSectionWatch(() => sections);
</script>

<aside class="rail">
  <!-- Already resolved by the page, which is the only place that knows what it
       is linking to. The rule cannot follow a route through a prop. -->
  <!-- eslint-disable-next-line svelte/no-navigation-without-resolve -->
  <a class="back" href={backHref}>
    <span class="arrow" aria-hidden="true">←</span>
    {backLabel}
  </a>

  <p class="title" class:untitled={!title} class:away={watch.away}>{title || untitled}</p>

  <div class="state">
    <SaveState {tone} text={status} />
  </div>

  <RailSectionNav label={sectionsLabel} {sections} current={watch.current} onjump={watch.jump} />
</aside>

<style>
  /*
   * A floating bar on a phone, on the same glass and the same radius as the
   * navigation above it.
   */
  .rail {
    position: sticky;
    /* Clear of the app's own floating header, which is the offset every sticky
       thing in this app uses. */
    top: var(--space-24);
    z-index: var(--z-sticky);
    display: flex;
    align-items: center;
    gap: var(--space-2) var(--space-4);
    min-width: 0;
    padding: var(--space-2) var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-full);
    background: var(--surface-nav-glass);
    backdrop-filter: blur(16px);
  }

  .back {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
    flex: none;
    min-height: var(--control-sm);
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    text-decoration: none;
    transition: color var(--duration-fast) var(--ease-out);
  }

  .back:hover {
    color: var(--text);
  }

  /* Travels the way the link goes, which is the only thing an arrow on a back
     link is for. */
  .arrow {
    transition: transform var(--duration-fast) var(--ease-out);
  }

  .back:hover .arrow {
    transform: translateX(calc(var(--space-1) * -1));
  }

  /* On the bar this is context, not a heading: the form below it opens on the
     title field, and setting this at title size would print the name twice. */
  /*
   * Takes what is left and nothing more.
   *
   * A zero basis rather than `auto`: the way out and the save state are the two
   * things on this bar that must always be legible, so the name occupies the
   * room they leave and ellipsises inside it — rather than all three competing
   * and a 360px phone showing two ellipses and no information.
   */
  .title {
    display: none;
    flex: 1 1 0;
    min-width: 0;
    color: var(--text);
    font-family: var(--font-editorial);
    font-size: var(--text-base);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  /* Three things in a 360px bar is two ellipses and no information. The name
     takes its place once the field holding it has gone. */
  .title.away {
    display: block;
  }

  /* Held to the bar's far end whether or not the name is between them, so the
     one thing that changes on its own does not move when it changes. */
  .state {
    min-width: 0;
    margin-inline-start: auto;
  }

  .untitled {
    color: var(--text-subtle);
  }

  /* Wide enough for a column of its own: the bar unrolls into the rail the
     settings screens already use. */
  @media (min-width: 64rem) {
    .rail {
      display: flex;
      flex-direction: column;
      align-items: stretch;
      gap: var(--space-4);
      padding: 0;
      border: none;
      border-radius: 0;
      background: none;
      backdrop-filter: none;
    }

    .title {
      display: block;
      flex: 1 1 auto;
      white-space: normal;
      font-size: var(--text-xl);
      line-height: var(--leading-tight);
      /* Two lines of a long name, then an ellipsis. A rail is a fixed column
         and a recipe called after its grandmother can be very long. */
      display: -webkit-box;
      -webkit-box-orient: vertical;
      -webkit-line-clamp: 2;
      line-clamp: 2;
      overflow: hidden;
    }

    .state {
      margin-inline-start: 0;
    }
  }

  @media print {
    .rail {
      display: none;
    }
  }
</style>
