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
    /* As tall as the glass capsules beside it: a control and their inset. */
    min-width: calc(var(--control-sm) + var(--space-2));
    min-height: calc(var(--control-sm) + var(--space-2));
    padding: var(--space-2);
    border: 0;
    border-radius: var(--radius-full);
    background: var(--accent);
    color: var(--accent-contrast);
    box-shadow: var(--shadow-glass);
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
  /* The label appears once there is room; the shape doesn't change (squaring off to `--radius-md` made it
     the odd one out among pills on wide screens). */
  @media (min-width: 80rem) {
    .new-recipe {
      padding-inline: var(--space-4);
    }
    span {
      display: inline;
    }
  }
</style>
