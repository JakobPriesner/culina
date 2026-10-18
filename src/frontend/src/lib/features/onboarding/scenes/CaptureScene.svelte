<script lang="ts">
  import { SegmentedControl } from '$ds';
  import { sourceAppNames } from '$features/import/sourceApps';
  import { m } from '$shell/i18n';

  import { play, typing, type Beat } from './demo';
  import ImportDemo from './ImportDemo.svelte';
  import type { React } from './reaction';

  /**
   * The ways a recipe gets in, each played out: a link read into a recipe, a photographed page read the
   * same way, just a name that can already go on a day, or a whole collection from another app.
   */
  interface Props {
    onreact: React;
  }

  let { onreact }: Props = $props();

  type Way = 'link' | 'photo' | 'name' | 'import';

  let way = $state<Way>('link');
  let typed = $state('');
  let scanning = $state(false);
  let reading = $state(false);
  let card = $state<'none' | 'title' | 'full'>('none');
  let lines = $state(0);
  let planned = $state(false);

  const ingredients = [
    m['tour.sample.eggs'],
    m['tour.sample.tomatoes'],
    m['tour.sample.pepper'],
    m['tour.sample.onion']
  ];

  function filled(at: number): Beat[] {
    return [
      [
        at,
        () => {
          reading = false;
          scanning = false;
          card = 'full';
        }
      ],
      ...ingredients.map((_, i): Beat => [at + 160 * (i + 1), () => (lines = i + 1)])
    ];
  }

  function beatsFor(chosen: Way): Beat[] {
    if (chosen === 'link') {
      const address = typing(m['tour.capture.address'](), 250, (text) => (typed = text), 30);

      return [
        ...address.beats,
        [address.end + 250, () => (reading = true)],
        ...filled(address.end + 1200),
        [address.end + 2000, () => onreact('idea', m['tour.capture.link.said']())]
      ];
    }

    if (chosen === 'photo') {
      return [
        [100, () => (scanning = true)],
        ...filled(1700),
        [2500, () => onreact('idea', m['tour.capture.photo.said']())]
      ];
    }

    // Plays itself on arrival, in its own component.
    if (chosen === 'import') return [];

    const name = typing(m['tour.sample.title'](), 250, (text) => (typed = text), 70);

    return [
      ...name.beats,
      [name.end + 300, () => (card = 'title')],
      [name.end + 1000, () => (planned = true)],
      [name.end + 1300, () => onreact('celebrating', m['tour.capture.name.said']())]
    ];
  }

  // Every choice plays from the start; the first plays by itself.
  $effect(() => {
    const chosen = way;

    typed = '';
    scanning = false;
    reading = false;
    card = 'none';
    lines = 0;
    planned = false;

    return play(beatsFor(chosen));
  });
</script>

<div class="capture">
  <SegmentedControl
    label={m['tour.capture.ways']()}
    selected={way}
    onselect={(id) => {
      way = id as Way;
      // Olli's comment was on the last way; back to the question until this one plays out.
      onreact('reading', m['tour.capture.says']());
    }}
    segments={[
      { id: 'link', label: m['tour.capture.link']() },
      { id: 'photo', label: m['tour.capture.photo']() },
      { id: 'name', label: m['tour.capture.name']() },
      { id: 'import', label: m['tour.capture.import']() }
    ]}
  />

  <div class="demo" aria-hidden="true">
    {#if way === 'import'}
      <ImportDemo {onreact} />
    {:else if way === 'photo'}
      <div class="page" class:scanning>
        {#each { length: 7 }, row (row)}<span class="row" class:short={row % 3 === 2}></span>{/each}
      </div>
    {:else}
      <div class="field">
        <svg
          class="glyph"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.8"
          stroke-linecap="round"
          stroke-linejoin="round"
        >
          {#if way === 'link'}
            <path d="M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1" />
            <path d="M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1" />
          {:else}
            <path d="M4 20h4L19 9a2.1 2.1 0 0 0-3-3L5 17v3Z" />
          {/if}
        </svg>
        <span class="typed">{typed}<span class="caret"></span></span>
      </div>
    {/if}

    {#if way !== 'import'}
      <div class="card" class:reading class:shown={card !== 'none'}>
        {#if card !== 'none'}
          <p class="title">{m['tour.sample.title']()}</p>
          {#if card === 'full'}
            <p class="meta">{m['tour.sample.meta']()}</p>
            <ul class="lines">
              {#each ingredients.slice(0, lines) as line (line)}<li>{line()}</li>{/each}
            </ul>
          {:else}
            <p class="later">{m['tour.capture.later']()}</p>
            {#if planned}<span class="day">{m['tour.capture.planned']()}</span>{/if}
          {/if}
        {/if}
      </div>
    {/if}
  </div>

  <p class="caption">
    {way === 'link'
      ? m['tour.capture.link.what']()
      : way === 'photo'
        ? m['tour.capture.photo.what']()
        : way === 'name'
          ? m['tour.capture.name.what']()
          : m['tour.capture.import.what']({ apps: sourceAppNames() })}
  </p>
</div>

<style>
  .capture {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--space-4);
  }

  .demo {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--space-3);
    width: min(22rem, 100%);
    min-height: 13rem;
  }

  .field {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    width: 100%;
    min-height: var(--control-md);
    padding: 0 var(--space-3);
    border: 1px solid var(--border-strong);
    border-radius: var(--radius-full);
    background: var(--surface);
    font-size: var(--text-sm);
  }

  .glyph {
    flex: none;
    width: 1.1rem;
    height: 1.1rem;
    color: var(--text-muted);
  }

  .typed {
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
  }

  .caret {
    display: inline-block;
    width: 1px;
    height: 1.1em;
    margin-inline-start: 1px;
    vertical-align: text-bottom;
    background: var(--text);
    animation: blink 1s steps(1) infinite;
  }

  .page {
    position: relative;
    display: flex;
    flex-direction: column;
    gap: 0.45rem;
    width: 7.5rem;
    padding: var(--space-3);
    overflow: hidden;
    border: 1px solid var(--border);
    border-radius: var(--radius-sm);
    background: var(--mascot-paper);
    rotate: -3deg;
  }

  .row {
    height: 0.3rem;
    border-radius: var(--radius-full);
    background: var(--mascot-paper-line);
  }

  .row.short {
    width: 60%;
  }

  .page.scanning::after {
    content: '';
    position: absolute;
    inset-inline: 0;
    top: 0;
    height: 0.25rem;
    background: var(--accent);
    box-shadow: 0 0 var(--space-3) var(--accent);
    animation: scan 1.5s var(--ease-out) forwards;
  }

  .card {
    width: 100%;
    min-height: 8.5rem;
    padding: var(--space-3) var(--space-4);
    border: 1px dashed var(--border);
    border-radius: var(--radius-md);
    transition:
      background-color var(--duration-slow) var(--ease-out),
      border-color var(--duration-slow) var(--ease-out);
  }

  .card.shown {
    border-style: solid;
    background: var(--surface-raised);
    box-shadow: var(--shadow-card);
    animation: arrive var(--duration-slow) var(--ease-spatial);
  }

  .card.reading {
    background: linear-gradient(
      100deg,
      var(--skeleton-base) 30%,
      var(--skeleton-highlight) 50%,
      var(--skeleton-base) 70%
    );
    background-size: 300% 100%;
    animation: shimmer 1s linear infinite;
  }

  .title {
    margin: 0;
    font-family: var(--font-editorial);
    font-size: var(--text-lg);
  }

  .meta,
  .later {
    margin: 0 0 var(--space-2);
    color: var(--text-muted);
    font-size: var(--text-xs);
  }

  .lines {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-1);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .lines li {
    padding: 0.1rem var(--space-2);
    border-radius: var(--radius-full);
    background: var(--surface-accent-subtle);
    font-size: var(--text-xs);
    animation: arrive var(--duration-base) var(--ease-spatial);
  }

  .day {
    display: inline-block;
    padding: 0.1rem var(--space-2);
    border-radius: var(--radius-full);
    background: var(--accent);
    color: var(--accent-contrast);
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
    animation: arrive var(--duration-base) var(--ease-spatial);
  }

  .caption {
    max-width: 26rem;
    margin: 0;
    color: var(--text-muted);
    text-align: center;
    line-height: var(--leading-normal);
  }

  @keyframes arrive {
    from {
      opacity: 0;
      transform: scale(0.92);
    }
  }

  @keyframes scan {
    to {
      top: calc(100% - 0.25rem);
    }
  }

  @keyframes shimmer {
    to {
      background-position: -150% 0;
    }
  }

  @keyframes blink {
    50% {
      opacity: 0;
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .card.shown,
    .lines li,
    .day,
    .card.reading,
    .caret,
    .page.scanning::after {
      animation: none;
    }
  }
</style>
