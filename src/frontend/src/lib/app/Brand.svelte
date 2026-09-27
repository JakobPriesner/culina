<script lang="ts">
  import { m } from './i18n';
</script>

<span class="brand" role="img" aria-label={m['app.name']()}>
  <svg viewBox="0 0 40 40" fill="none" aria-hidden="true">
    <path
      d="M28 12.7C25.8 10.1 22.7 8.7 19.2 8.7C12.9 8.7 8.2 13.6 8.2 20C8.2 26.4 12.9 31.3 19.2 31.3C22.5 31.3 25.5 30 27.7 27.7"
    />
  </svg>
  <span class="wordmark" aria-hidden="true">culina<span class="dot">.</span></span>
</span>

<style>
  .brand {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
    color: var(--text);
    font-family: var(--font-editorial);
    font-size: 2rem;
    font-weight: var(--weight-regular);
    letter-spacing: -0.055em;
    line-height: 1;
  }
  svg {
    width: var(--control-sm);
    height: var(--control-sm);
    padding: var(--space-1);
    border-radius: var(--radius-md);
    background: var(--surface-accent-subtle);
    stroke: var(--accent);
    stroke-width: 5.2;
    stroke-linecap: round;
    stroke-linejoin: round;
  }
  /* A name, not a sentence: it never breaks, least of all before its dot. */
  .wordmark {
    white-space: nowrap;
  }
  .dot {
    color: var(--brand-accent);
  }
</style>
