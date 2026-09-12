<script lang="ts">
  import { m } from './i18n';
</script>

<span class="brand" role="img" aria-label={m['app.name']()}>
  <svg viewBox="0 0 40 40" fill="none" aria-hidden="true">
    <path d="M7 22h26M10 25a10 10 0 0 0 20 0M16 17c-4-4 4-5 0-9M24 17c-4-4 4-5 0-9" />
  </svg>
  <span aria-hidden="true">culina<span class="dot">.</span></span>
</span>

<style>
  .brand {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
    color: var(--text);
    font-size: var(--text-2xl);
    font-weight: var(--weight-semibold);
    letter-spacing: -0.065em;
    line-height: 1;
  }
  svg {
    width: var(--space-8);
    height: var(--space-8);
    stroke: var(--accent);
    stroke-width: 1.8;
    stroke-linecap: round;
  }
  .dot {
    color: var(--accent);
  }
</style>
