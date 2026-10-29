<script lang="ts">
  import { Chevron } from '$ds';
  import EdgeEffect from '$ds/display/EdgeEffect.svelte';
  import { resolve } from '$app/paths';
  import { m } from '$shell/i18n';

  interface Props {
    /** The recipe's name, shown beside the arrow on a phone once `condensed`. */
    title?: string;
    /** The page's own title has scrolled out of view. */
    condensed?: boolean;
  }

  let { title = '', condensed = false }: Props = $props();
</script>

<div class="bar" class:condensed>
  <div class="frost"><EdgeEffect edge="top" /></div>
  <a class="back" href={resolve('/(app)')} aria-label={m['recipe.back']()}>
    <span aria-hidden="true"><Chevron direction="back" /></span>
    <span class="back-label">{m['recipe.back']()}</span>
  </a>
  <!-- Visual repeat of the heading below, so it stays out of the accessibility tree. -->
  <span class="title" aria-hidden="true">{title}</span>
</div>

<style>
  .bar {
    margin-bottom: var(--space-6);
    font-size: var(--text-sm);
  }

  .back {
    color: var(--text-muted);
    text-decoration: none;
  }

  @media (hover: hover) {
    .back:hover {
      color: var(--text);
    }
  }

  .title,
  .frost {
    display: none;
  }

  /* Keep the explicit way back that preserves the library's search state, but
     let the arrow carry it on a phone. The link's aria-label remains the full
     name, so compact is only visual. The shell's header gives way on a recipe,
     so this bar sticks to the top instead and, once the title has scrolled
     away, carries it: an iOS toolbar, a glass circle over the photo, and the
     scroll edge effect behind it once the page runs underneath. */
  @media (width < 52rem) {
    .bar {
      position: sticky;
      top: 0;
      /* Above the photo's overlaid actions, which scroll beneath it. */
      z-index: calc(var(--z-sticky) + 1);
      display: flex;
      align-items: center;
      gap: var(--space-3);
      margin-bottom: var(--space-2);
      /* Full-bleed, so the scrolled content does not show at the sides. */
      margin-inline: calc(-1 * var(--layout-gutter-start)) calc(-1 * var(--layout-gutter-end));
      padding: calc(var(--space-2) + env(safe-area-inset-top, 0px)) var(--layout-gutter-end)
        var(--space-2) var(--layout-gutter-start);
      /* Clicks pass through the empty part to the photo beneath, as in the shell's header. */
      pointer-events: none;
    }

    .bar.condensed {
      pointer-events: auto;
    }

    .frost {
      display: block;
      opacity: 0;
      transition: opacity var(--duration-base) var(--ease-out);
    }

    .condensed .frost {
      opacity: 1;
    }

    .back {
      position: relative;
      flex: none;
      display: inline-grid;
      place-items: center;
      width: var(--control-sm);
      min-height: var(--control-sm);
      border-radius: var(--radius-full);
      background: var(--surface-nav-glass);
      backdrop-filter: var(--glass-filter);
      box-shadow: var(--shadow-glass);
      color: var(--text);
      font-size: var(--text-lg);
      pointer-events: auto;
    }

    .back-label {
      display: none;
    }

    .title {
      position: relative;
      display: block;
      min-width: 0;
      overflow: hidden;
      white-space: nowrap;
      text-overflow: ellipsis;
      font-family: var(--font-editorial);
      font-size: var(--text-lg);
      color: var(--text);
      opacity: 0;
      transition: opacity var(--duration-base) var(--ease-out);
    }

    .condensed .title {
      opacity: 1;
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .title,
    .frost {
      transition: none;
    }
  }

  /* Paper cannot be navigated. */
  @media print {
    .bar {
      display: none;
    }
  }
</style>
