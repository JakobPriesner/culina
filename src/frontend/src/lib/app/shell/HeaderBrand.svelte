<script lang="ts">
  import { resolve } from '$app/paths';
  import { m } from '$shell/i18n';

  import Brand from '../Brand.svelte';
</script>

<a class="brand" href={resolve('/(app)')} aria-label={m['app.name']()}><Brand /></a>

<style>
  /*
   * Stays, and stays on every page.
   *
   * It is the way home and it is the only thing on screen that says which app
   * this is — which matters more here than in most places, because Culina is
   * self-hosted and lives at whatever address somebody gave it. The slot could
   * carry the page's own title once the title has scrolled away instead, and
   * that is worth building one day; it is not worth replacing the only fixed
   * point in the app with.
   *
   * What it did not have was the behaviour of the link it is: no hover, no
   * pressed state, nothing to tell a pointer that this is a control rather than
   * a logo printed in the corner. It borrows the navigation's own, because it
   * sits in the same row of pills and does the same kind of thing.
   */
  .brand {
    grid-column: 1;
    justify-self: start;
    /* A flex box, not a line of text: a line box would add its strut to the
       mark's height and leave this pill taller than the ones beside it. */
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

  /* Concentric with the pill around it, the way nested glass is drawn: a
     rounded square inside a capsule is two shapes that disagree. */
  .brand :global(svg) {
    border-radius: var(--radius-full);
  }

  /*
   * The narrowest phones keep the mark and let the word go.
   *
   * The wordmark needs about 155 of the 288 pixels a 320px screen leaves
   * between its gutters, and the tools beside it take 164 — so something has
   * to give, and squeezing the word only clips it. The mark alone in a circle
   * is still the brand, still the way home, and drawn the same as every other
   * pill in the row; the name is in its label for anyone who cannot see it.
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
