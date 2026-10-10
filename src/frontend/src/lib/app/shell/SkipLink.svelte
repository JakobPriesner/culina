<script lang="ts">
  import { m } from '$shell/i18n';
</script>

<!-- First in the tab order and invisible until focused: without it, reaching
     the page content by keyboard means tabbing through the navigation on
     every single page. -->
<a class="skip" href="#content">{m['nav.skip']()}</a>

<style>
  .skip {
    position: absolute;
    inset-block-start: var(--space-2);
    inset-inline-start: var(--space-2);
    z-index: var(--z-overlay);
    padding: var(--space-2) var(--space-4);
    border-radius: var(--radius-md);
    background: var(--surface-overlay);
    color: var(--text);
    transform: translateY(-200%);
  }

  /* Shadowless while parked, or its blur bleeds into the top edge. */
  .skip:focus {
    box-shadow: var(--shadow-overlay);
    transform: none;
  }

  @media print {
    .skip {
      display: none !important;
    }
  }
</style>
