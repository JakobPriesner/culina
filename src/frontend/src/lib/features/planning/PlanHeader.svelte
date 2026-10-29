<script lang="ts">
  import { SegmentedControl } from '$ds';

  import { m } from '$shell/i18n';

  import MonthPicker from './MonthPicker.svelte';
  import PlanNav from './PlanNav.svelte';
  import type { PlanView } from './planView.svelte';

  /**
   * The plan's head: what is on screen (title, and its dates, which open the month), how it is looked at, and the way
   * through time. On a phone upright that is two rows, the title with Tage | Woche and then the way through time beside
   * the dates it moves, so the first day starts high. From 30rem the way through time rides in the header bar (`nav`) and
   * the head is one row; from 70rem (the list beside the week) the dates sit under the title.
   */
  interface Props {
    title: string;
    /** The days on screen, worded. */
    range: string;
    /** First and last day on screen. */
    from: string;
    to: string;
    locale: string;
    view: PlanView;
    onview: (view: PlanView) => void;
    /** Draws back, today and forward here. */
    nav?: boolean;
    atToday: boolean;
    onstep: (direction: -1 | 1) => void;
    ontoday: () => void;
    onjump: (date: string) => void;
  }

  let {
    title,
    range,
    from,
    to,
    locale,
    view,
    onview,
    nav = true,
    atToday,
    onstep,
    ontoday,
    onjump
  }: Props = $props();
</script>

<header class="head">
  <h1 class="title">{title}</h1>

  <div class="range">
    <MonthPicker {range} {from} {to} {locale} onpick={onjump} />
  </div>

  <div class="view">
    <SegmentedControl
      label={m['plan.view.label']()}
      selected={view}
      segments={[
        { id: 'days', label: m['plan.view.days']() },
        { id: 'week', label: m['plan.view.week']() }
      ]}
      onselect={(id) => onview(id === 'week' ? 'week' : 'days')}
    />
  </div>

  {#if nav}
    <div class="nav">
      <PlanNav {view} {atToday} {onstep} {ontoday} />
    </div>
  {/if}
</header>

<style>
  /* A phone: title and Tage | Woche, then back, today and forward with the dates; each row wraps when it must. */
  .head {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    margin-bottom: var(--space-6);
  }

  .title {
    order: 1;
    margin: 0;
    margin-inline-end: auto;
    font-family: var(--font-editorial);
    font-size: var(--text-2xl);
    font-weight: var(--weight-regular);
    line-height: var(--leading-tight);
  }

  .view {
    order: 2;
  }

  .nav {
    order: 3;
  }

  /* The dates take what back, today and forward leave on their row, so the two stay together whatever the range says. */
  .range {
    order: 4;
    flex: 1 1 0;
    min-width: 0;
  }

  .range :global(.anchor) {
    max-width: 100%;
  }

  /* The segments are as wide as their words: "Tage" and "Woche" each get a full thumb's width. */
  .view :global(.segment) {
    min-width: 4rem;
    padding-inline: var(--space-3);
  }

  /* 320px: Tage | Woche and back, today, forward share a row, under the title. */
  @media (max-width: 23rem) {
    .view :global(.segment) {
      min-width: 0;
      padding-inline: var(--space-2);
    }
  }

  /* A phone on its side or a tablet: one row, the title and its dates on the left, Tage | Woche at the right. */
  @media (min-width: 30rem) {
    .title {
      order: 1;
      margin-inline-end: 0;
    }

    .range {
      order: 2;
      flex: 0 1 auto;
      margin-inline-end: auto;
    }

    .view {
      order: 3;
    }

    .nav {
      order: 4;
    }
  }

  /* Beside the list: the title over its dates on the left, the controls at the right. */
  @media (min-width: 70rem) {
    .head {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto auto;
      grid-template-areas:
        'title view nav'
        'range view nav';
      align-items: end;
      gap: 0 var(--space-2);
      margin-bottom: var(--space-6);
    }

    .title {
      grid-area: title;
      justify-self: start;
    }

    .range {
      grid-area: range;
      justify-self: start;
      margin-inline-end: 0;
    }

    .view {
      grid-area: view;
      align-self: end;
    }

    .nav {
      grid-area: nav;
      align-self: end;
    }
  }
</style>
