<script lang="ts">
  import RailSectionNav from './RailSectionNav.svelte';
  import SaveState, { type SaveTone } from './SaveState.svelte';
  import { createSectionWatch } from './sectionWatch.svelte';

  /**
   * Where you are in the recipe and whether the work is safe: a sticky column on wide screens, a
   * glass bar on a phone (section links dropped).
   */
  interface Props {
    backHref: string;
    backLabel: string;
    title: string;
    untitled: string;
    tone: SaveTone;
    status: string;
    sectionsLabel: string;
    sections: readonly { id: string; label: string; count?: number }[];
  }

  let { backHref, backLabel, title, untitled, tone, status, sectionsLabel, sections }: Props =
    $props();

  const watch = createSectionWatch(() => sections);
</script>

<aside class="rail">
  <!-- Already resolved by the page; the lint rule cannot follow a route through a prop. -->
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
  .rail {
    position: sticky;
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

  .arrow {
    transition: transform var(--duration-fast) var(--ease-out);
  }

  .back:hover .arrow {
    transform: translateX(calc(var(--space-1) * -1));
  }

  /*
   * Takes what is left: a zero basis so the way out and save state stay legible and the name
   * ellipsises.
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

  .title.away {
    display: block;
  }

  .state {
    min-width: 0;
    margin-inline-start: auto;
  }

  .untitled {
    color: var(--text-subtle);
  }

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
      /* Two lines of a long name, then an ellipsis. */
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
