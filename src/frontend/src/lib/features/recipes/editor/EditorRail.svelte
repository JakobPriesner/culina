<script lang="ts">
  import SaveState, { type SaveTone } from './SaveState.svelte';

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

  /** Which section the observer has last seen, or none before it has run. */
  let seenSection = $state<string | null>(null);

  /**
   * Which section the rail marks.
   *
   * The first until the observer says otherwise: the page opens at the top, and
   * a rail that marks nothing for the first frame flickers.
   */
  const current = $derived(seenSection ?? sections[0]?.id ?? null);

  /**
   * Whether the bar has to say which recipe this is.
   *
   * On a phone it does not, while the title field is on screen — the name is
   * already there, in the field, in the editorial face, at four times the size.
   * It appears as soon as that field has scrolled away and the bar is the only
   * thing left that could say it.
   */
  const away = $derived(current !== null && current !== sections[0]?.id);

  /**
   * Watched rather than computed from the scroll position.
   *
   * A scroll handler measuring four elements on every frame is the version that
   * makes the page stutter on the one device that cannot afford it. The band is
   * the upper third of the viewport: a heading that has reached it is the one
   * being read, and the section it belongs to is the one to mark.
   */
  $effect(() => {
    const watched = sections
      .map((section) => document.getElementById(section.id))
      .filter((element): element is HTMLElement => element !== null);

    if (watched.length === 0) {
      return;
    }

    // A plain record rather than a Set, because nothing here is reactive: the
    // observer reports only what changed, so the ones it did not mention have
    // to be remembered from the call before.
    const seen: Record<string, boolean> = {};

    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          seen[entry.target.id] = entry.isIntersecting;
        }

        // The first in the recipe's own order, not the first the observer
        // happened to report: entries arrive in whatever order they changed.
        seenSection = sections.find((section) => seen[section.id])?.id ?? seenSection;
      },
      { rootMargin: '-15% 0px -70% 0px' }
    );

    for (const element of watched) {
      observer.observe(element);
    }

    return () => observer.disconnect();
  });

  /**
   * Goes to a section the way the browser would, and moves focus with it.
   *
   * The link keeps its `href`, so it can be opened in a new tab and read as a
   * link — but the jump is taken over here rather than left to the document,
   * because a hash in the address bar of an editor is a history entry per
   * glance at the ingredients, and because `scroll-behavior` is a property of
   * the whole page and this is the only screen that wants it.
   *
   * Focus moves to the heading, which is what the browser's own hash
   * navigation does and the reason it is worth reproducing: a keyboard user who
   * jumps to the steps must land in the steps, not back at the top of the form.
   */
  function jump(event: MouseEvent, id: string) {
    const target = document.getElementById(id);

    // No element yet, or a click the browser owns — a modified click is
    // somebody opening it somewhere else on purpose.
    if (!target || event.metaKey || event.ctrlKey || event.shiftKey || event.button !== 0) {
      return;
    }

    event.preventDefault();
    seenSection = id;

    const still = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;

    // Missing in jsdom, where moving the screen is not what is under test.
    target.scrollIntoView?.({ block: 'start', behavior: still ? 'auto' : 'smooth' });
    target.focus({ preventScroll: true });
  }
</script>

<aside class="rail">
  <!-- Already resolved by the page, which is the only place that knows what it
       is linking to. The rule cannot follow a route through a prop. -->
  <!-- eslint-disable-next-line svelte/no-navigation-without-resolve -->
  <a class="back" href={backHref}>
    <span class="arrow" aria-hidden="true">←</span>
    {backLabel}
  </a>

  <p class="title" class:untitled={!title} class:away>{title || untitled}</p>

  <div class="state">
    <SaveState {tone} text={status} />
  </div>

  <nav class="sections" aria-label={sectionsLabel}>
    {#each sections as section (section.id)}
      {@const selected = current === section.id}

      <!-- A fragment on the page that is already open, which the rule cannot
           tell apart from a route. `resolve()` is for routes, and there is
           nothing here to resolve. -->
      <!-- eslint-disable-next-line svelte/no-navigation-without-resolve -->
      <a
        class="section"
        class:selected
        href="#{section.id}"
        aria-current={selected ? 'true' : undefined}
        onclick={(event) => jump(event, section.id)}
      >
        <span class="label">{section.label}</span>
        {#if section.count !== undefined}
          <span class="count">{section.count}</span>
        {/if}
      </a>
    {/each}
  </nav>
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

  .sections {
    display: none;
  }

  /*
   * Wide enough for a column of its own: the bar unrolls into the rail the
   * settings screens already use, so an inner navigation looks like this app's
   * inner navigation wherever it appears.
   */
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

    .sections {
      display: flex;
      flex-direction: column;
      gap: var(--space-1);
      margin-top: var(--space-2);
    }

    /* The same pill the settings rail and the navbar use. */
    .section {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-3);
      min-height: var(--control-sm);
      padding-inline: var(--space-4);
      border-radius: var(--radius-full);
      color: var(--text-muted);
      font-size: var(--text-sm);
      font-weight: var(--weight-medium);
      text-decoration: none;
      transition:
        color var(--duration-fast) var(--ease-out),
        background-color var(--duration-fast) var(--ease-out);
    }

    .section:hover {
      background: var(--surface-hover);
      color: var(--text);
    }

    .section.selected {
      background: var(--surface-accent-subtle);
      color: var(--accent);
      font-weight: var(--weight-semibold);
    }

    .count {
      color: var(--text-subtle);
      font-variant-numeric: tabular-nums;
    }

    .section.selected .count {
      color: inherit;
    }
  }

  @media print {
    .rail {
      display: none;
    }
  }
</style>
