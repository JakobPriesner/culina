<script lang="ts">
  import { sourceApps } from '$features/import/sourceApps';
  import { m } from '$shell/i18n';

  import { play } from './demo';
  import type { React } from './reaction';

  /**
   * A whole collection brought over from another app, played out: an app chosen, recipes ticked, and a
   * cookbook named after the app appearing. The apps are the import page's own list (`sourceApps`), so one
   * added there shows here too.
   */
  interface Props {
    onreact: React;
  }

  let { onreact }: Props = $props();

  const recipes = [m['tour.sample.title'], m['tour.sample.lentils'], m['tour.sample.bread']];
  const app = sourceApps[0];

  let chosen = $state(false);
  let ticked = $state(0);
  let pressed = $state(false);
  let shelf = $state(false);

  $effect(() =>
    play([
      [300, () => (chosen = true)],
      ...recipes.map((_, i): [number, () => void] => [800 + i * 380, () => (ticked = i + 1)]),
      [2100, () => (pressed = true)],
      [
        2500,
        () => {
          pressed = false;
          shelf = true;
        }
      ],
      [2900, () => onreact('celebrating', m['tour.capture.import.said']())]
    ])
  );
</script>

<div class="import">
  <div class="apps">
    {#each sourceApps as one, index (one.kind)}
      <span class="app" class:chosen={chosen && index === 0}>{one.name}</span>
    {/each}
  </div>

  {#if shelf}
    <div class="shelf">
      <span class="spine" aria-hidden="true"></span>
      <span class="spine" aria-hidden="true"></span>
      <span class="spine" aria-hidden="true"></span>
      <span class="shelf-words">
        <strong>{app.name}</strong>
        <span>{m['tour.capture.import.count']({ count: recipes.length })}</span>
      </span>
    </div>
  {:else}
    <ul class="recipes">
      {#each recipes as recipe, index (index)}
        <li class:ticked={index < ticked}><span class="box"></span>{recipe()}</li>
      {/each}
    </ul>
    <span class="bring" class:pressed>{m['import.library.bringOver']()}</span>
  {/if}
</div>

<style>
  .import {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--space-3);
    width: 100%;
  }

  .apps {
    display: flex;
    flex-wrap: wrap;
    justify-content: center;
    gap: var(--space-2);
  }

  .app {
    padding: var(--space-1) var(--space-3);
    border: 1px solid var(--border-strong);
    border-radius: var(--radius-full);
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    transition:
      background-color var(--duration-base) var(--ease-out),
      color var(--duration-base) var(--ease-out);
  }

  .app.chosen {
    border-color: var(--accent);
    background: var(--accent);
    color: var(--accent-contrast);
  }

  .recipes {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    width: 100%;
    margin: 0;
    padding: var(--space-2) var(--space-3);
    border: 1px solid var(--border);
    border-radius: var(--radius-md);
    background: var(--surface-raised);
    list-style: none;
  }

  .recipes li {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    padding: var(--space-1) 0;
    font-size: var(--text-sm);
  }

  .box {
    flex: none;
    width: 1rem;
    height: 1rem;
    border: 1.5px solid var(--border-strong);
    border-radius: var(--radius-sm);
    transition: background-color var(--duration-fast) var(--ease-out);
  }

  .ticked .box {
    border-color: var(--accent);
    background: var(--accent);
  }

  .bring {
    padding: var(--space-2) var(--space-4);
    border-radius: var(--radius-full);
    background: var(--accent);
    color: var(--accent-contrast);
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
    transition: scale var(--duration-fast) var(--ease-out);
  }

  .bring.pressed {
    scale: 0.94;
  }

  .shelf {
    display: flex;
    align-items: flex-end;
    gap: var(--space-1);
    width: 100%;
    padding: var(--space-3) var(--space-4);
    border: 1px solid var(--border-accent);
    border-radius: var(--radius-md);
    background: var(--surface-raised);
    box-shadow: var(--shadow-card);
    animation: arrive var(--duration-slow) var(--ease-spatial);
  }

  .spine {
    width: 0.6rem;
    height: 2.4rem;
    border-radius: var(--radius-sm);
    background: var(--accent);
  }

  .spine:nth-child(2) {
    height: 2rem;
    opacity: 0.7;
  }

  .spine:nth-child(3) {
    height: 2.2rem;
    opacity: 0.5;
  }

  .shelf-words {
    display: flex;
    flex-direction: column;
    margin-inline-start: var(--space-3);
  }

  .shelf-words strong {
    font-family: var(--font-editorial);
    font-size: var(--text-lg);
    font-weight: var(--weight-medium);
  }

  .shelf-words span {
    color: var(--text-muted);
    font-size: var(--text-xs);
  }

  @keyframes arrive {
    from {
      opacity: 0;
      transform: scale(0.92);
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .shelf {
      animation: none;
    }

    .app,
    .box,
    .bring {
      transition: none;
    }
  }
</style>
