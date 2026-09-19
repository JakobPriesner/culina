<script lang="ts">
  import { resolve } from '$app/paths';
  import { m } from './i18n';
</script>

<a
  class="new-recipe"
  href={resolve('/(app)/recipes/new')}
  aria-label={m['recipes.empty.action']()}
  title={m['recipes.empty.action']()}
>
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true">
    <path d="M12 5v14M5 12h14" stroke-linecap="round" />
  </svg>
  <span>{m['recipes.empty.action']()}</span>
</a>

<style>
  .new-recipe {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
    gap: var(--space-2);
    min-width: var(--control-sm);
    min-height: var(--control-sm);
    padding: var(--space-2);
    border: 1px solid transparent;
    border-radius: var(--radius-full);
    background: var(--accent);
    color: var(--accent-contrast);
    box-shadow: var(--shadow-card);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    text-decoration: none;
    transition: background-color var(--duration-fast) var(--ease-out);
  }
  .new-recipe:hover {
    background: var(--accent-hover);
  }
  .new-recipe:active {
    background: var(--accent-active);
  }
  svg {
    width: var(--space-4);
    height: var(--space-4);
    flex-shrink: 0;
  }
  span {
    display: none;
  }
  /*
   * The label appears once there is room for it; the shape does not change
   * with it.
   *
   * It used to square off to `--radius-md` as it grew, which made it the one
   * thing in a header of pills — the brand's card and the navigation's — drawn
   * from a different kit, and only on wide screens, where all three are visible
   * at once and the difference is impossible to miss.
   */
  @media (min-width: 80rem) {
    .new-recipe {
      padding-inline: var(--space-4);
    }
    span {
      display: inline;
    }
  }
</style>
