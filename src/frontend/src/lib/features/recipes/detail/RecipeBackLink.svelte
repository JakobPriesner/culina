<script lang="ts">
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
  <a class="back" href={resolve('/(app)')} aria-label={m['recipe.back']()}>
    <span aria-hidden="true">←</span>
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

  .back:hover {
    color: var(--text);
  }

  .title {
    display: none;
  }

  /* Keep the explicit way back that preserves the library's search state, but
     let the arrow carry it on a phone. The link's aria-label remains the full
     name, so compact is only visual. The shell's header gives way on a recipe,
     so this bar sticks to the top instead and, once the title has scrolled
     away, carries it. */
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
      background: var(--surface);
      box-shadow: 0 1px 0 var(--border);
      pointer-events: auto;
    }

    .back {
      flex: none;
      display: inline-grid;
      place-items: center;
      width: var(--control-sm);
      min-height: var(--control-sm);
      border: 1px solid var(--border);
      border-radius: var(--radius-full);
      background: var(--surface);
      font-size: var(--text-lg);
      pointer-events: auto;
    }

    .back-label {
      display: none;
    }

    .title {
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
    .title {
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
