<script lang="ts">
  import { resolve } from '$app/paths';
  import { m } from '$shell/i18n';

  import Brand from '../Brand.svelte';
</script>

<a class="brand" href={resolve('/(app)')} aria-label={m['app.name']()}><Brand /></a>

<style>
  /*
   * Fixed on every page: the only thing naming the self-hosted app. Borrows the navigation's hover
   * and pressed states, being in the same row of pills.
   */
  .brand {
    grid-column: 1;
    justify-self: start;
    /* Flex, not a line box, whose strut would make this pill taller than its neighbours. */
    display: flex;
    min-width: 0;
    max-width: 100%;
    text-decoration: none;
    padding: var(--space-1) var(--space-4) var(--space-1) var(--space-1);
    border-radius: var(--radius-full);
    background: var(--surface-nav-glass);
    backdrop-filter: blur(20px) saturate(180%);
    -webkit-backdrop-filter: blur(20px) saturate(180%);
    box-shadow: var(--shadow-glass);
    pointer-events: auto;
    transition: background-color var(--duration-fast) var(--ease-out);
  }

  .brand:hover {
    background: var(--surface-selected);
  }

  .brand:active {
    background: var(--surface-hover);
  }

  /* Concentric with the capsule around it. */
  .brand :global(svg) {
    border-radius: var(--radius-full);
  }

  /*
   * At 320px the wordmark (~155 of 288px) does not fit beside the tools (164px); the name stays in
   * the aria-label.
   */
  @media (width < 22rem) {
    .brand {
      padding: var(--space-1);
    }

    .brand :global(.wordmark) {
      display: none;
    }
  }
</style>
