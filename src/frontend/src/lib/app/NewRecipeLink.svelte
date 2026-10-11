<script lang="ts">
  import { resolve } from '$app/paths';
  import { m } from './i18n';
</script>

<!-- A plus, as a toolbar's add button is: the header is capped at the content width, and beside 15px tabs a label never fits. Its name is in the label and tooltip. -->
<a
  class="new-recipe"
  href={resolve('/(app)/recipes/new')}
  aria-label={m['recipes.empty.action']()}
  title={m['recipes.empty.action']()}
>
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true">
    <path d="M12 5v14M5 12h14" stroke-linecap="round" />
  </svg>
</a>

<style>
  .new-recipe {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
    /* As tall as the glass capsules beside it: a control and their inset. */
    min-width: calc(var(--control-sm) + var(--space-2));
    min-height: calc(var(--control-sm) + var(--space-2));
    padding: var(--space-2);
    border: 0;
    border-radius: var(--radius-full);
    background: var(--accent);
    color: var(--accent-contrast);
    box-shadow: var(--shadow-glass);
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
</style>
