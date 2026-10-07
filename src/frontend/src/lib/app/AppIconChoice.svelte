<script lang="ts">
  import { appIcon } from './appIcon.svelte';
  import { appIconLinks, appIcons, type AppIcon } from './appIcons';
  import { m } from './i18n';

  /**
   * Every app icon at once, as the icons themselves; native radios give arrow-key movement and
   * announce the choice.
   */
  const names: Record<AppIcon, () => string> = {
    basil: m['appIcon.basil'],
    ink: m['appIcon.ink'],
    paper: m['appIcon.paper'],
    saffron: m['appIcon.saffron'],
    cocotte: m['appIcon.cocotte']
  };

  const current = $derived(appIcon.current);
</script>

<div class="icons">
  {#each appIcons as icon (icon)}
    <label class="icon" class:chosen={current === icon}>
      <input
        class="ds-clipped"
        type="radio"
        name="app-icon"
        value={icon}
        checked={current === icon}
        onchange={() => appIcon.choose(icon)}
      />
      <img src={appIconLinks(icon).svg} alt="" width="56" height="56" />
      <span>{names[icon]()}</span>
    </label>
  {/each}
</div>

<style>
  .icons {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  .icon {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--space-1);
    min-width: 4.5rem;
    padding: var(--space-2);
    border-radius: var(--radius-lg);
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    cursor: pointer;
    transition: color var(--duration-fast) var(--ease-out);
  }

  .icon:hover:not(.chosen) {
    color: var(--text);
  }

  img {
    display: block;
    width: 3.5rem;
    height: 3.5rem;
    border-radius: var(--radius-lg);
  }

  /* A ring as well as the bolder name, so colour is never the only signal. */
  .chosen {
    color: var(--text);
    font-weight: var(--weight-semibold);
  }

  .chosen img {
    outline: 2px solid var(--accent);
    outline-offset: 2px;
  }

  .icon:has(input:focus-visible) {
    outline: 2px solid var(--border-focus);
    outline-offset: 2px;
  }
</style>
