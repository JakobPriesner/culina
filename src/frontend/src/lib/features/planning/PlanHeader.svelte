<script lang="ts">
  import { SegmentedControl } from '$ds';

  import { m } from '$shell/i18n';

  import MonthPicker from './MonthPicker.svelte';
  import PlanNav from './PlanNav.svelte';
  import type { PlanView } from './planView.svelte';

  /**
   * The plan's title row: what is on screen (title, and its dates, which open the month), how it is looked at, and the
   * way through time. On a phone the way through time rides in the header bar instead, so it stays in reach (`nav`).
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
  <div class="heading">
    <h1 class="title">{title}</h1>
    <MonthPicker {range} {from} {to} {locale} onpick={onjump} />
  </div>

  <div class="controls">
    <SegmentedControl
      label={m['plan.view.label']()}
      selected={view}
      segments={[
        { id: 'days', label: m['plan.view.days']() },
        { id: 'week', label: m['plan.view.week']() }
      ]}
      onselect={(id) => onview(id === 'week' ? 'week' : 'days')}
    />
    {#if nav}<PlanNav {view} {atToday} {onstep} {ontoday} />{/if}
  </div>
</header>

<style>
  .head {
    display: flex;
    flex-wrap: wrap;
    align-items: flex-end;
    justify-content: space-between;
    gap: var(--space-3) var(--space-4);
    margin-bottom: var(--space-6);
  }

  .heading {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    min-width: 0;
  }

  .title {
    margin: 0;
    font-family: var(--font-editorial);
    font-size: var(--text-2xl);
    font-weight: var(--weight-regular);
    line-height: var(--leading-tight);
  }

  /* At 320px back, today and forward drop under the segments rather than past the edge. */
  .controls {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    min-width: 0;
  }

  /* The segments are as wide as their words: "Tage" and "Woche" each get a full thumb's width. */
  .controls :global(.segment) {
    min-width: 4rem;
    padding-inline: var(--space-3);
  }
</style>
